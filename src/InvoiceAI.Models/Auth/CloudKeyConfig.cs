namespace InvoiceAI.Models.Auth;

public class CloudKeyConfig
{
    // OCR
    public string OcrToken { get; set; } = string.Empty;
    public string OcrEndpoint { get; set; } = string.Empty;

    // GLM - Support all providers
    public string? ZhipuApiKey { get; set; }
    public string? ZhipuEndpoint { get; set; }
    public string? ZhipuModel { get; set; }

    public string? NvidiaApiKey { get; set; }
    public string? NvidiaEndpoint { get; set; }
    public string? NvidiaModel { get; set; }

    public string? CerebrasApiKey { get; set; }
    public string? CerebrasEndpoint { get; set; }
    public string? CerebrasModel { get; set; }

    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}
