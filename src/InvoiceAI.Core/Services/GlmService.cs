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

    public GlmService(HttpClient httpClient, IAppSettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<GlmInvoiceResponse> ProcessInvoiceAsync(string ocrText)
    {
        var settings = _settingsService.Settings.Glm;
        var (apiKey, endpoint, model, maxTokens) = settings.GetActiveConfig();

        var requestBody = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = InvoicePrompt.SystemPrompt },
                new { role = "user", content = InvoicePrompt.BuildUserMessage(ocrText) }
            },
            temperature = 0.1,
            max_tokens = maxTokens
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        request.Content = JsonContent.Create(requestBody);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var response = await _httpClient.SendAsync(request, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Save raw GLM response for debugging
        var logDir = Path.Combine(Path.GetTempPath(), "InvoiceAI");
        Directory.CreateDirectory(logDir);
        await File.WriteAllTextAsync(
            Path.Combine(logDir, "glm_raw_response.json"),
            json.GetRawText());

        var messageEl = json.GetProperty("choices")[0].GetProperty("message");

        // GLM-4.7 reasoning model may put thinking in "reasoning_content", actual answer in "content"
        var content = messageEl.TryGetProperty("content", out var contentEl)
            ? contentEl.GetString() ?? ""
            : "";

        // If content is empty but reasoning_content has content, try that (fallback)
        if (string.IsNullOrWhiteSpace(content) &&
            messageEl.TryGetProperty("reasoning_content", out var reasoningEl))
        {
            content = reasoningEl.GetString() ?? "";
        }

        // Extract JSON from content — handle markdown code blocks, mixed text, etc.
        var jsonStr = ExtractJson(content);
        if (jsonStr != null)
        {
            return JsonSerializer.Deserialize<GlmInvoiceResponse>(jsonStr,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new GlmInvoiceResponse { Description = "解析失败", InvoiceType = "NonQualified" };
        }

        // Save failed content for diagnosis
        await File.WriteAllTextAsync(
            Path.Combine(logDir, "glm_parse_failed.txt"),
            $"Content:\n{content}\n\nRaw:\n{json.GetRawText()}");

        return new GlmInvoiceResponse
        {
            Description = "GLM 返回格式异常",
            InvoiceType = "NonQualified",
            MissingFields = ["1", "2", "3", "4", "5", "6"]
        };
    }

    /// <summary>
    /// Extracts the first valid JSON object from text, handling markdown code blocks and mixed content.
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
                if (candidate.StartsWith('{')) return candidate;
            }
        }

        // Find outermost { ... } with brace matching
        var jsonStart = text.IndexOf('{');
        if (jsonStart < 0) return null;

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

        return null;
    }
}
