using System.Net.Http.Json;
using System.Text.Json;
using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

public class BaiduOcrService : IBaiduOcrService
{
    private readonly HttpClient _httpClient;
    private readonly IAppSettingsService _settingsService;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public BaiduOcrService(HttpClient httpClient, IAppSettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<string> RecognizeAsync(string filePath)
    {
        var token = await GetAccessTokenAsync();
        var bytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(bytes);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        var url = ext == ".pdf"
            ? $"https://aip.baidubce.com/rest/2.0/ocr/v1/doc_analysis?access_token={token}"
            : $"https://aip.baidubce.com/rest/2.0/ocr/v1/general?language_type=JAP&access_token={token}";

        var parameters = new Dictionary<string, string>
        {
            ["image"] = base64,
            ["language_type"] = "JAP"
        };

        var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var texts = json.TryGetProperty("words_result", out var results)
            ? string.Join("\n", results.EnumerateArray()
                .Select(r => r.TryGetProperty("words", out var w) ? w.GetString() ?? "" : ""))
            : "";

        return texts;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
            return _accessToken;

        var settings = _settingsService.Settings.BaiduOcr;
        var url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={settings.ApiKey}&client_secret={settings.SecretKey}";

        var response = await _httpClient.PostAsync(url, null);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        _accessToken = json.GetProperty("access_token").GetString();
        var expiresIn = json.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 300);

        return _accessToken!;
    }
}
