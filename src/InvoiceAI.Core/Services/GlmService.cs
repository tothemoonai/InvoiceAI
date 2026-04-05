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

        var requestBody = new
        {
            model = settings.Model,
            messages = new object[]
            {
                new { role = "system", content = InvoicePrompt.SystemPrompt },
                new { role = "user", content = InvoicePrompt.BuildUserMessage(ocrText) }
            },
            temperature = 0.1,
            max_tokens = 2000
        };

        var response = await _httpClient.PostAsJsonAsync(settings.Endpoint, requestBody);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;

        var jsonStart = content.IndexOf('{');
        var jsonEnd = content.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = content[jsonStart..(jsonEnd + 1)];
            return JsonSerializer.Deserialize<GlmInvoiceResponse>(jsonStr,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new GlmInvoiceResponse { Description = "解析失败", InvoiceType = "NonQualified" };
        }

        return new GlmInvoiceResponse
        {
            Description = "GLM 返回格式异常",
            InvoiceType = "NonQualified",
            MissingFields = ["1", "2", "3", "4", "5", "6"]
        };
    }
}
