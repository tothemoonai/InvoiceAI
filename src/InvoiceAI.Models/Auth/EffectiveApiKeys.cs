namespace InvoiceAI.Models.Auth;

public class EffectiveApiKeys
{
    public string OcrToken { get; set; } = string.Empty;
    public string OcrEndpoint { get; set; } = string.Empty;
    public string GlmApiKey { get; set; } = string.Empty;
    public string GlmEndpoint { get; set; } = string.Empty;
    public string GlmModel { get; set; } = string.Empty;
    public string GlmProvider { get; set; } = "zhipu";
    public string Source { get; set; } = "local"; // "cloud" or "local"
    public int KeyVersion { get; set; } = 1;
}
