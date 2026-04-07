namespace InvoiceAI.Core.Helpers;

public class AppSettings
{
    public BaiduOcrSettings BaiduOcr { get; set; } = new();
    public GlmSettings Glm { get; set; } = new();
    public string Language { get; set; } = "zh";
    public List<string> Categories { get; set; } =
    [
        "電気・ガス", "食料品", "オフィス用品",
        "交通費", "通信費", "接待費", "その他"
    ];
}

public class BaiduOcrSettings
{
    public string Token { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
}

public class GlmSettings
{
    public string Provider { get; set; } = "zhipu";

    // Zhipu (智谱)
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    public string Model { get; set; } = "glm-4.7";

    // NVIDIA NIM
    public string NvidiaApiKey { get; set; } = string.Empty;
    public string NvidiaEndpoint { get; set; } = "https://integrate.api.nvidia.com/v1/chat/completions";
    public string NvidiaModel { get; set; } = "z-ai/glm4.7";

    public (string ApiKey, string Endpoint, string Model, int MaxTokens) GetActiveConfig() =>
        Provider == "nvidia"
            ? (NvidiaApiKey, NvidiaEndpoint, NvidiaModel, 32768)
            : (ApiKey, Endpoint, Model, 100000);
}