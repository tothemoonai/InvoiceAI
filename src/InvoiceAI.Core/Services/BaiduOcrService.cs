using System.Net.Http.Json;
using System.Text.Json;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public class BaiduOcrService : IBaiduOcrService
{
    private readonly HttpClient _httpClient;
    private readonly IAppSettingsService _settingsService;
    private static string LogDir => Helpers.LogHelper.LogDir;

    public BaiduOcrService(HttpClient httpClient, IAppSettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<string> RecognizeAsync(string filePath)
    {
        var effectiveKeys = await _settingsService.GetEffectiveApiKeysAsync();

        if (string.IsNullOrWhiteSpace(effectiveKeys.OcrToken) || string.IsNullOrWhiteSpace(effectiveKeys.OcrEndpoint))
            throw new InvalidOperationException("请先在设置中配置 PaddleOCR Token 和端点地址，或登录以使用云端配置");

        var bytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(bytes);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        var payload = new Dictionary<string, object?>
        {
            ["file"] = base64,
            ["fileType"] = ext == ".pdf" ? 0 : 1,
            ["useDocOrientationClassify"] = true,
            ["useDocUnwarping"] = true,
            ["visualize"] = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, effectiveKeys.OcrEndpoint);
        request.Headers.TryAddWithoutValidation("Authorization", $"token {effectiveKeys.OcrToken}");
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("PaddleOCR Token 无效或已过期");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("PaddleOCR 配额已用尽或无权访问");
        if ((int)response.StatusCode == 429)
            throw new InvalidOperationException("PaddleOCR 请求过于频繁，请稍后再试");
        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = json.TryGetProperty("errorMsg", out var msg) ? msg.GetString() : response.ReasonPhrase;
            throw new InvalidOperationException($"PaddleOCR 请求失败 ({(int)response.StatusCode}): {errorMsg}");
        }

        // Extract markdown text from response: result.layoutParsingResults[].markdown.text
        if (!json.TryGetProperty("result", out var resultEl))
            throw new InvalidOperationException("PaddleOCR 返回结果格式异常：缺少 result 字段");

        if (!resultEl.TryGetProperty("layoutParsingResults", out var pages))
            throw new InvalidOperationException("PaddleOCR 返回结果格式异常：缺少 layoutParsingResults 字段");

        var texts = new List<string>();
        foreach (var page in pages.EnumerateArray())
        {
            if (page.TryGetProperty("markdown", out var md) &&
                md.TryGetProperty("text", out var textEl))
            {
                var text = textEl.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    texts.Add(text);
            }
        }

        var ocrText = string.Join("\n\n", texts);

        // Save OCR result to MD file
        var ocrDir = Path.Combine(LogDir, "ocr");
        Directory.CreateDirectory(ocrDir);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var mdPath = Path.Combine(ocrDir, $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmss}.md");
        await File.WriteAllTextAsync(mdPath, ocrText);

        return ocrText;
    }
}
