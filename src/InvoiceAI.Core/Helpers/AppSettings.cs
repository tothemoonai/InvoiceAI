namespace InvoiceAI.Core.Helpers;

public class AppSettings
{
    public BaiduOcrSettings BaiduOcr { get; set; } = new();
    public GlmSettings Glm { get; set; } = new();
    public string Language { get; set; } = "zh";
    
    // 主题模式 ("Auto" | "Light" | "Dark")
    public string ThemeMode { get; set; } = "Auto";
    
    public bool AutoSaveAfterExport { get; set; } = false;
    public string ExportPath { get; set; } = string.Empty;
    public string InvoiceArchivePath { get; set; } = string.Empty;
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
    public string Model { get; set; } = string.Empty; // 保留用于兼容性
    public int ZhipuSelectedModelIndex { get; set; } = 0;

    // NVIDIA NIM
    public string NvidiaApiKey { get; set; } = string.Empty;
    public string NvidiaEndpoint { get; set; } = "https://integrate.api.nvidia.com/v1/chat/completions";
    public string NvidiaModel { get; set; } = string.Empty; // 保留用于兼容性
    public int NvidiaSelectedModelIndex { get; set; } = 0;

    // Cerebras
    public string CerebrasApiKey { get; set; } = string.Empty;
    public string CerebrasEndpoint { get; set; } = "https://api.cerebras.ai/v1/chat/completions";
    public string CerebrasModel { get; set; } = string.Empty; // 保留用于兼容性
    public int CerebrasSelectedModelIndex { get; set; } = 0;

    // Google
    public string GoogleApiKey { get; set; } = string.Empty;
    public string GoogleEndpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
    public string GoogleModel { get; set; } = string.Empty; // 保留用于兼容性
    public int GoogleSelectedModelIndex { get; set; } = 0;

    // List of providers verified by user through Settings page
    public List<string> VerifiedProviders { get; set; } = new();

    // Model lists per provider
    public static readonly (string Id, string Name)[] ZhipuModels = [
        ("glm-4.7", "GLM-4.7"),
        ("glm-4-plus", "GLM-4-Plus"),
    ];
    public static readonly (string Id, string Name)[] NvidiaModels = [
        ("deepseek-ai/deepseek-v3.1-terminus", "DeepSeek-V3.1-Terminus"),
        ("deepseek-ai/deepseek-r1", "DeepSeek-R1"),
        ("stepfun-ai/step-3.5-flash", "Step-3.5-Flash"),
        ("moonshotai/kimi-k2-instruct", "Kimi-K2-Instruct"),
        ("qwen/qwen3-coder-480b-a35b-instruct", "Qwen3-Coder-480B-A35B-Instruct"),
    ];
    public static readonly (string Id, string Name)[] CerebrasModels = [
        ("qwen-3-235b-a22b-instruct-2507", "Qwen-3-235B"),
    ];
    public static readonly (string Id, string Name)[] GoogleModels = [
        ("gemma-4-31b-it", "Gemma-4-31B-IT"),
        ("gemini-3.1-flash-lite-preview", "Gemini-3.1-Flash-Lite"),
    ];

    public (string Id, string Name)[] GetModelsForProvider() => Provider switch
    {
        "nvidia" => NvidiaModels,
        "cerebras" => CerebrasModels,
        "google" => GoogleModels,
        _ => ZhipuModels
    };

    public int GetSelectedModelIndex() => Provider switch
    {
        "nvidia" => NvidiaSelectedModelIndex,
        "cerebras" => CerebrasSelectedModelIndex,
        "google" => GoogleSelectedModelIndex,
        _ => ZhipuSelectedModelIndex
    };

    public void SetSelectedModelIndex(int index)
    {
        switch (Provider)
        {
            case "nvidia": NvidiaSelectedModelIndex = index; break;
            case "cerebras": CerebrasSelectedModelIndex = index; break;
            case "google": GoogleSelectedModelIndex = index; break;
            default: ZhipuSelectedModelIndex = index; break;
        }
    }

    // 新的配置方法：返回 API密钥、端点、所有模型、选中的索引、max_tokens
    public (string ApiKey, string Endpoint, (string Id, string Name)[] Models, int SelectedIndex, int MaxTokens) GetActiveConfigWithModels() =>
        Provider switch
        {
            "nvidia" => (NvidiaApiKey, NvidiaEndpoint, NvidiaModels, NvidiaSelectedModelIndex, 32768),
            "cerebras" => (CerebrasApiKey, CerebrasEndpoint, CerebrasModels, CerebrasSelectedModelIndex, 32768),
            "google" => (GoogleApiKey, GoogleEndpoint, GoogleModels, GoogleSelectedModelIndex, 32768),
            _ => (ApiKey, Endpoint, ZhipuModels, ZhipuSelectedModelIndex, 100000)
        };

    // 兼容性：保留旧的 GetActiveConfig
    public (string ApiKey, string Endpoint, string Model, int MaxTokens) GetActiveConfig() =>
        Provider switch
        {
            "nvidia" => (NvidiaApiKey, NvidiaEndpoint, NvidiaModel, 32768),
            "cerebras" => (CerebrasApiKey, CerebrasEndpoint, CerebrasModel, 32768),
            "google" => (GoogleApiKey, GoogleEndpoint, GoogleModel, 32768),
            _ => (ApiKey, Endpoint, Model, 100000)
        };
}