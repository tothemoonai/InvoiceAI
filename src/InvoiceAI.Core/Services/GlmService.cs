using System.Net.Http.Json;
using System.Text.Json;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Prompts;
using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public class GlmService : IGlmService
{
    private readonly HttpClient _httpClient;
    private readonly IAppSettingsService _settingsService;

    private static string LogDir => Helpers.LogHelper.LogDir;

    public GlmService(HttpClient httpClient, IAppSettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    private Dictionary<string, object> BuildRequestBody(string userMessage, int maxTokens, string provider)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = _settingsService.Settings.Glm.GetActiveConfig().Model,
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

    public async Task<GlmInvoiceResponse> ProcessInvoiceAsync(string ocrText)
    {
        const int maxRetries = 3;
        for (int attempt = 0; ; attempt++)
        {
            var settings = _settingsService.Settings.Glm;
            var (apiKey, endpoint, model, maxTokens) = settings.GetActiveConfig();
            var provider = settings.Provider;

            var requestBody = BuildRequestBody(
                InvoicePrompt.BuildUserMessage(ocrText), maxTokens, provider);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
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

        // Always use batch processing path to properly handle multi-invoice responses
        // (even for single files that may contain multiple invoices like multi-page PDFs)
        try
        {
            var batchResults = await ProcessBatchCoreAsync(ocrTexts);

            // If batch returned fewer results than inputs, process missing ones individually
            if (batchResults.Count < ocrTexts.Length)
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, "import_error.log"),
                    $"[{DateTime.Now:HH:mm:ss}] GLM batch returned {batchResults.Count}/{ocrTexts.Length} results, processing remaining individually\n");
                for (int i = batchResults.Count; i < ocrTexts.Length; i++)
                {
                    try
                    {
                        var singleResults = await ProcessInvoiceAsyncInternal(ocrTexts[i]);
                        batchResults.AddRange(singleResults);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(Path.Combine(LogDir, "import_error.log"),
                            $"[{DateTime.Now:HH:mm:ss}] Individual fallback #{i} failed: {ex.Message}\n");
                    }
                }
            }

            return batchResults;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(LogDir);
            await File.WriteAllTextAsync(Path.Combine(LogDir, "glm_batch_error.txt"),
                $"Batch failed, falling back to individual calls.\n{ex}");
            // Process individually with per-item error handling
            var results = new List<GlmInvoiceResponse>();
            for (int i = 0; i < ocrTexts.Length; i++)
            {
                try
                {
                    var singleResults = await ProcessInvoiceAsyncInternal(ocrTexts[i]);
                    results.AddRange(singleResults);
                    if (i < ocrTexts.Length - 1) await Task.Delay(1000);
                }
                catch (Exception itemEx)
                {
                    File.AppendAllText(Path.Combine(LogDir, "import_error.log"),
                        $"[{DateTime.Now:HH:mm:ss}] Fallback item #{i} failed: {itemEx.Message}\n");
                }
            }
            return results;
        }
    }

    private async Task<List<GlmInvoiceResponse>> ProcessInvoiceAsyncInternal(string ocrText)
    {
        const int maxRetries = 3;
        for (int attempt = 0; ; attempt++)
        {
            var settings = _settingsService.Settings.Glm;
            var (apiKey, endpoint, model, maxTokens) = settings.GetActiveConfig();
            var provider = settings.Provider;

            var requestBody = BuildRequestBody(
                InvoicePrompt.BuildUserMessage(ocrText), maxTokens, provider);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
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

            return ParseResponse(json);
        }
    }

    private async Task<List<GlmInvoiceResponse>> ProcessBatchCoreAsync(string[] ocrTexts)
    {
        const int maxRetries = 3;
        for (int attempt = 0; ; attempt++)
        {
            var settings = _settingsService.Settings.Glm;
            var (apiKey, endpoint, model, maxTokens) = settings.GetActiveConfig();
            var provider = settings.Provider;

            var requestBody = BuildRequestBody(
                InvoicePrompt.BuildBatchUserMessage(ocrTexts), maxTokens, provider);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            var jsonPayload = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(Math.Max(5, ocrTexts.Length * 3)));
            var response = await _httpClient.SendAsync(request, cts.Token);

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
