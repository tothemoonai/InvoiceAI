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
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    public string Model { get; set; } = "glm-4-flash";
}