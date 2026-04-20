using System.Net.Http.Json;
using System.Text.Json;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Prompts;
using InvoiceAI.Models;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public class GlmService : IGlmService
{
    private readonly HttpClient _httpClient;
    private readonly IAppSettingsService _settingsService;

    private static string LogDir => Helpers.LogHelper.LogDir;

    public event EventHandler<string>? StatusChanged;

    public GlmService(HttpClient httpClient, IAppSettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);

    private static string GetProviderDisplayName(string provider) => provider switch
    {
        "zhipu" => "智谱",
        "nvidia" => "NVIDIA",
        "cerebras" => "Cerebras",
        "google" => "Google",
        _ => provider
    };

    /// <summary>
    /// Normalize model ID for OpenAI-compatible endpoints.
    /// Google's native API uses "models/" prefix, but the OpenAI-compatible endpoint does not.
    /// </summary>
    private static string NormalizeModelId(string modelId, string provider)
    {
        if (provider == "google" && modelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            return modelId["models/".Length..];
        return modelId;
    }

    private Dictionary<string, object> BuildRequestBody(string userMessage, int maxTokens, string provider, string model)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = new object[]
            {
                new { role = "system", content = InvoicePrompt.SystemPrompt },
                new { role = "user", content = userMessage }
            },
            ["max_tokens"] = maxTokens
        };

        // Only Zhipu supports thinking parameter; temperature must be 1.0 with thinking
        if (provider == "zhipu")
        {
            body["temperature"] = 1.0;
            body["thinking"] = new { type = "disabled" };
        }
        else
        {
            body["temperature"] = 0.1;
        }

        return body;
    }

    /// <summary>
    /// 使用模型循环尝试处理单个 OCR 文本
    /// 从用户选择的模型开始，依次尝试后续所有模型
    /// </summary>
    private async Task<List<GlmInvoiceResponse>> ProcessWithModelFallbackAsync(
        string[] ocrTexts,
        string apiKey,
        string endpoint,
        (string Id, string Name)[] models,
        int startIndex,
        int baseMaxTokens,
        string provider)
    {
        // 根据批处理大小动态调整 max_tokens
        var maxTokens = ocrTexts.Length > 1
            ? Math.Max(baseMaxTokens, ocrTexts.Length * 16000)  // 批处理时增加 token 限制
            : baseMaxTokens;

        // 从用户选择的模型开始，依次尝试
        var providerName = GetProviderDisplayName(provider);
        for (int modelIdx = startIndex; modelIdx < models.Length; modelIdx++)
        {
            var model = models[modelIdx];
            try
            {
                if (modelIdx > startIndex)
                {
                    OnStatusChanged($"⚠️ [{providerName}] 模型 {models[modelIdx - 1].Name} 失败，切换到 {model.Name} ({modelIdx + 1}/{models.Length})...");
                }
                else
                {
                    OnStatusChanged($"🤖 [{providerName}] 使用模型 {model.Name} ({modelIdx + 1}/{models.Length})...");
                }

                return await CallGlmApiAsync(ocrTexts, apiKey, endpoint, model.Id, maxTokens, provider);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, "import_error.log"),
                    $"[{DateTime.Now:HH:mm:ss}] 模型 {model.Name} 失败: {ex.Message}\n");

                // 如果还有更多模型可尝试，继续循环
                if (modelIdx < models.Length - 1)
                {
                    continue;
                }
                else
                {
                    // 最后一个模型也失败了
                    throw new Exception($"该提供商所有模型均失败: {models[startIndex].Name} → ... → {model.Name}");
                }
            }
        }

        throw new Exception("没有可用的模型");
    }

    /// <summary>
    /// 调用 GLM API 的核心方法
    /// </summary>
    private async Task<List<GlmInvoiceResponse>> CallGlmApiAsync(
        string[] ocrTexts,
        string apiKey,
        string endpoint,
        string model,
        int maxTokens,
        string provider)
    {
        const int maxRetries = 3;
        for (int attempt = 0; ; attempt++)
        {
            var userMessage = ocrTexts.Length == 1
                ? InvoicePrompt.BuildUserMessage(ocrTexts[0])
                : InvoicePrompt.BuildBatchUserMessage(ocrTexts);

            var requestBody = BuildRequestBody(userMessage, maxTokens, provider, model);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            var jsonPayload = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            // 批处理超时时间更长
            var timeout = ocrTexts.Length > 1 ? TimeSpan.FromMinutes(Math.Max(5, ocrTexts.Length * 3)) : TimeSpan.FromMinutes(3);
            using var cts = new CancellationTokenSource(timeout);
            var response = await _httpClient.SendAsync(request, cts.Token);

            if ((int)response.StatusCode == 429 && attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                await Task.Delay(delay, cts.Token);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Save raw GLM response for debugging
            Directory.CreateDirectory(LogDir);
            var responseFile = ocrTexts.Length > 1
                ? "glm_batch_raw_response.json"
                : "glm_raw_response.json";
            await File.WriteAllTextAsync(
                Path.Combine(LogDir, responseFile),
                json.GetRawText());

            return ParseResponse(json);
        }
    }

    public async Task<GlmInvoiceResponse> ProcessInvoiceAsync(string ocrText)
    {
        const int maxRetries = 3;
        for (int attempt = 0; ; attempt++)
        {
            var effectiveKeys = await _settingsService.GetEffectiveApiKeysAsync();
            var maxTokens = effectiveKeys.Source == "cloud" && effectiveKeys.GlmProvider == "zhipu" ? 100000 : 32768;

            var requestBody = BuildRequestBody(
                InvoicePrompt.BuildUserMessage(ocrText), maxTokens, effectiveKeys.GlmProvider, NormalizeModelId(effectiveKeys.GlmModel, effectiveKeys.GlmProvider));

            using var request = new HttpRequestMessage(HttpMethod.Post, effectiveKeys.GlmEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {effectiveKeys.GlmApiKey}");
            var jsonPayload = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var response = await _httpClient.SendAsync(request, cts.Token);

            if ((int)response.StatusCode == 429 && attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, "import_error.log"),
                    $"[{DateTime.Now:HH:mm:ss}] 429 rate limited, retry {attempt + 1}/{maxRetries} after {delay.TotalSeconds}s\n");
                await Task.Delay(delay, cts.Token);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Save raw GLM response for debugging
            Directory.CreateDirectory(LogDir);
            await File.WriteAllTextAsync(
                Path.Combine(LogDir, "glm_raw_response.json"),
                json.GetRawText());

            var results = ParseResponse(json);
            // For single-file processing, return the first invoice (maintains backward compatibility)
            // The full array is saved in glm_raw_response.json for debugging
            return results.Count > 0 ? results[0] : new GlmInvoiceResponse { Description = "解析失败", InvoiceType = "NonQualified" };
        }
    }

    public async Task<List<GlmInvoiceResponse>> ProcessBatchAsync(string[] ocrTexts)
    {
        if (ocrTexts.Length == 0) return [];

        // 优先使用云端配置
        var effectiveKeys = await _settingsService.GetEffectiveApiKeysAsync();
        string provider;
        string apiKey;
        string endpoint;
        string model;
        int maxTokens;

        if (effectiveKeys.Source == "cloud")
        {
            // 使用云端配置（单模型模式）
            provider = effectiveKeys.GlmProvider;
            apiKey = effectiveKeys.GlmApiKey;
            endpoint = effectiveKeys.GlmEndpoint;
            model = NormalizeModelId(effectiveKeys.GlmModel, provider);
            maxTokens = provider == "zhipu" ? 100000 : 32768;

            // 云端配置使用单模型
            var models = new[] { (model, model) };
            try
            {
                return await ProcessWithModelFallbackAsync(ocrTexts, apiKey, endpoint, models, 0, maxTokens, provider);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(LogDir);
                await File.WriteAllTextAsync(Path.Combine(LogDir, "glm_provider_error.txt"),
                    $"Cloud provider {provider} failed: {ex}");
                throw;
            }
        }
        else
        {
            // 使用本地配置（多模型模式）
            var (localApiKey, localEndpoint, models, selectedIndex, localMaxTokens) = _settingsService.Settings.Glm.GetActiveConfigWithModels();
            provider = _settingsService.Settings.Glm.Provider;
            apiKey = localApiKey;
            endpoint = localEndpoint;
            maxTokens = localMaxTokens;

            try
            {
                return await ProcessWithModelFallbackAsync(ocrTexts, apiKey, endpoint, models, selectedIndex, maxTokens, provider);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(LogDir);
                await File.WriteAllTextAsync(Path.Combine(LogDir, "glm_provider_error.txt"),
                    $"Local provider {provider} all models failed: {ex}");
                throw;
            }
        }
    }

    private async Task<List<GlmInvoiceResponse>> ProcessInvoiceAsyncInternal(string ocrText)
    {
        const int maxRetries = 3;
        for (int attempt = 0; ; attempt++)
        {
            var effectiveKeys = await _settingsService.GetEffectiveApiKeysAsync();
            var maxTokens = effectiveKeys.Source == "cloud" && effectiveKeys.GlmProvider == "zhipu" ? 100000 : 32768;

            var requestBody = BuildRequestBody(
                InvoicePrompt.BuildUserMessage(ocrText), maxTokens, effectiveKeys.GlmProvider, effectiveKeys.GlmModel);

            using var request = new HttpRequestMessage(HttpMethod.Post, effectiveKeys.GlmEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {effectiveKeys.GlmApiKey}");
            var jsonPayload = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Directory.CreateDirectory(LogDir);
                await File.WriteAllTextAsync(Path.Combine(LogDir, "glm_api_error.json"), errorContent);
            }

            if ((int)response.StatusCode == 429 && attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, "import_error.log"),
                    $"[{DateTime.Now:HH:mm:ss}] 429 rate limited, retry {attempt + 1}/{maxRetries} after {delay.TotalSeconds}s\n");
                await Task.Delay(delay, cts.Token);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Save raw GLM response for debugging
            Directory.CreateDirectory(LogDir);
            await File.WriteAllTextAsync(
                Path.Combine(LogDir, "glm_raw_response.json"),
                json.GetRawText());

            return ParseResponse(json);
        }
    }

    private async Task<List<GlmInvoiceResponse>> ProcessBatchCoreAsync(string[] ocrTexts)
    {
        const int maxRetries = 3;
        for (int attempt = 0; ; attempt++)
        {
            var effectiveKeys = await _settingsService.GetEffectiveApiKeysAsync();
            var maxTokens = effectiveKeys.Source == "cloud" && effectiveKeys.GlmProvider == "zhipu" ? 100000 : 32768;

            var requestBody = BuildRequestBody(
                InvoicePrompt.BuildBatchUserMessage(ocrTexts), maxTokens, effectiveKeys.GlmProvider, effectiveKeys.GlmModel);

            using var request = new HttpRequestMessage(HttpMethod.Post, effectiveKeys.GlmEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {effectiveKeys.GlmApiKey}");
            var jsonPayload = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(Math.Max(5, ocrTexts.Length * 3)));
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Directory.CreateDirectory(LogDir);
                await File.WriteAllTextAsync(Path.Combine(LogDir, "glm_batch_api_error.json"), errorContent);
            }

            if ((int)response.StatusCode == 429 && attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 3);
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, "import_error.log"),
                    $"[{DateTime.Now:HH:mm:ss}] Batch 429 rate limited, retry {attempt + 1}/{maxRetries} after {delay.TotalSeconds}s\n");
                await Task.Delay(delay, cts.Token);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Save raw response
            Directory.CreateDirectory(LogDir);
            await File.WriteAllTextAsync(Path.Combine(LogDir, "glm_batch_raw_response.json"), json.GetRawText());

            var messageEl = json.GetProperty("choices")[0].GetProperty("message");
            var content = ExtractContent(messageEl);

            var jsonStr = ExtractJson(content);
            if (jsonStr == null)
            {
                await File.WriteAllTextAsync(Path.Combine(LogDir, "glm_batch_parse_failed.txt"),
                    $"Content:\n{content}\n\nRaw:\n{json.GetRawText()}");
                return [];
            }

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Extract token usage from batch response
            int promptTokens = 0, completionTokens = 0, totalTokens = 0;
            if (json.TryGetProperty("usage", out var usage))
            {
                promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                completionTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                totalTokens = usage.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;
            }

            if (jsonStr.TrimStart().StartsWith('['))
            {
                var arr = JsonSerializer.Deserialize<List<GlmInvoiceResponse>>(jsonStr, opts);
                if (arr != null && arr.Count > 0)
                {
                    // Distribute tokens across results
                    foreach (var item in arr)
                    {
                        item.PromptTokens = promptTokens / arr.Count;
                        item.CompletionTokens = completionTokens / arr.Count;
                        item.TotalTokens = totalTokens / arr.Count;
                    }
                    return arr;
                }
            }

            var single = JsonSerializer.Deserialize<GlmInvoiceResponse>(jsonStr, opts);
            if (single != null)
            {
                single.PromptTokens = promptTokens;
                single.CompletionTokens = completionTokens;
                single.TotalTokens = totalTokens;
                return [single];
            }

            return [];
        }
    }

    /// <summary>
    /// Parse GLM response into a list of invoice responses.
    /// Handles both single object and array responses.
    /// </summary>
    private static List<GlmInvoiceResponse> ParseResponse(JsonElement json)
    {
        var messageEl = json.GetProperty("choices")[0].GetProperty("message");
        var content = ExtractContent(messageEl);

        var jsonStr = ExtractJson(content);
        if (jsonStr == null)
        {
            Directory.CreateDirectory(LogDir);
            File.WriteAllText(
                Path.Combine(LogDir, "glm_parse_failed.txt"),
                $"Content:\n{content}\n\nRaw:\n{json.GetRawText()}");
            return [new GlmInvoiceResponse { Description = "LLM 返回格式异常", InvoiceType = "NonQualified", MissingFields = ["1", "2", "3", "4", "5", "6"] }];
        }

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Extract token usage
        int promptTokens = 0, completionTokens = 0, totalTokens = 0;
        if (json.TryGetProperty("usage", out var usage))
        {
            promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            completionTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
            totalTokens = usage.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;
        }

        // Try to parse as array first (for multi-invoice support)
        if (jsonStr.TrimStart().StartsWith('['))
        {
            var arr = JsonSerializer.Deserialize<List<GlmInvoiceResponse>>(jsonStr, opts);
            if (arr != null && arr.Count > 0)
            {
                foreach (var item in arr)
                {
                    item.PromptTokens = promptTokens / arr.Count;
                    item.CompletionTokens = completionTokens / arr.Count;
                    item.TotalTokens = totalTokens / arr.Count;
                }
                return arr;
            }
        }

        // Fallback: parse as single object
        var single = JsonSerializer.Deserialize<GlmInvoiceResponse>(jsonStr, opts);
        if (single != null)
        {
            single.PromptTokens = promptTokens;
            single.CompletionTokens = completionTokens;
            single.TotalTokens = totalTokens;
            return [single];
        }

        return [new GlmInvoiceResponse { Description = "解析失败", InvoiceType = "NonQualified" }];
    }

    [Obsolete("Use ParseResponse instead")]
    private static GlmInvoiceResponse ParseSingleResponse(JsonElement json)
    {
        var results = ParseResponse(json);
        return results.Count > 0 ? results[0] : new GlmInvoiceResponse { Description = "解析失败", InvoiceType = "NonQualified" };
    }

    private static string ExtractContent(JsonElement messageEl)
    {
        var content = messageEl.TryGetProperty("content", out var contentEl)
            ? contentEl.GetString() ?? "" : "";

        // If content is empty but reasoning_content has content, try that (fallback for Zhipu)
        if (string.IsNullOrWhiteSpace(content) &&
            messageEl.TryGetProperty("reasoning_content", out var reasoningEl))
        {
            content = reasoningEl.GetString() ?? "";
        }

        return content;
    }

    /// <summary>
    /// Extracts JSON from text, prioritizing arrays for multi-invoice support.
    /// </summary>
    private static string? ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Try to find ```json ... ``` block first
        var mdStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (mdStart >= 0)
        {
            var codeStart = text.IndexOf('\n', mdStart) + 1;
            var codeEnd = text.IndexOf("```", codeStart);
            if (codeEnd > codeStart)
                return text[codeStart..codeEnd].Trim();
        }

        // Try to find ``` ... ``` block
        mdStart = text.IndexOf("```");
        if (mdStart >= 0)
        {
            var codeStart = text.IndexOf('\n', mdStart) + 1;
            var codeEnd = text.IndexOf("```", codeStart);
            if (codeEnd > codeStart)
            {
                var candidate = text[codeStart..codeEnd].Trim();
                if (candidate.StartsWith('{') || candidate.StartsWith('[')) return candidate;
            }
        }

        // Priority: find array [...] first (for multi-invoice support)
        var arrayStart = text.IndexOf('[');
        if (arrayStart >= 0)
        {
            var depth = 0;
            for (var i = arrayStart; i < text.Length; i++)
            {
                if (text[i] == '[') depth++;
                else if (text[i] == ']') depth--;
                if (depth == 0) return text[arrayStart..(i + 1)];
            }
        }

        // Fallback: find single object { ... }
        var jsonStart = text.IndexOf('{');
        if (jsonStart < 0) return null;

        {
            var depth = 0;
            for (var i = jsonStart; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}') depth--;
                if (depth == 0) return text[jsonStart..(i + 1)];
            }

            // Fallback: last } as end
            var jsonEnd = text.LastIndexOf('}');
            if (jsonEnd > jsonStart) return text[jsonStart..(jsonEnd + 1)];
        }

        return null;
    }
}
