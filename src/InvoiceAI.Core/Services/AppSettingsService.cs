using System.Text.Json;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public class AppSettingsService : IAppSettingsService
{
    // 配置文件放在用户 AppData 目录下，不受编译/清理影响
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InvoiceAI", "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAuthService? _authService;
    private readonly ICloudKeyService? _cloudKeyService;

    public AppSettingsService(IAuthService? authService = null, ICloudKeyService? cloudKeyService = null)
    {
        _authService = authService;
        _cloudKeyService = cloudKeyService;
    }

    public AppSettings Settings { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (File.Exists(SettingsPath))
        {
            var json = await File.ReadAllTextAsync(SettingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
        }
        else
        {
            // 首次运行：创建默认配置文件
            Settings = new AppSettings();
            await SaveAsync();
            LogHelper.Log($"已创建默认配置文件: {SettingsPath}");
        }
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json);
    }

    public async Task<EffectiveApiKeys> GetEffectiveApiKeysAsync()
    {
        // ── Step 1: Resolve OCR independently (local first, then cloud) ──
        string ocrToken, ocrEndpoint;

        if (!string.IsNullOrEmpty(Settings.BaiduOcr.Token) && !string.IsNullOrEmpty(Settings.BaiduOcr.Endpoint))
        {
            ocrToken = Settings.BaiduOcr.Token;
            ocrEndpoint = Settings.BaiduOcr.Endpoint;
            LogHelper.Log($"[OCR] Using local: endpoint={ocrEndpoint}");
        }
        else
        {
            var cloudKeys = await GetCloudKeysAsync();
            ocrToken = cloudKeys?.OcrToken ?? Settings.BaiduOcr.Token;
            ocrEndpoint = cloudKeys?.OcrEndpoint ?? Settings.BaiduOcr.Endpoint;
            LogHelper.Log($"[OCR] Using cloud: endpoint={ocrEndpoint}");
        }

        // ── Step 2: Resolve LLM independently (local first, then cloud) ──
        // "Local LLM" = any provider has a non-empty API key
        var hasLocalLlm = !string.IsNullOrEmpty(Settings.Glm.ApiKey)
                       || !string.IsNullOrEmpty(Settings.Glm.NvidiaApiKey)
                       || !string.IsNullOrEmpty(Settings.Glm.CerebrasApiKey)
                       || !string.IsNullOrEmpty(Settings.Glm.GoogleApiKey);

        if (hasLocalLlm)
        {
            var (localApiKey, localEndpoint, localModel, _) = Settings.Glm.GetActiveConfig();
            LogHelper.Log($"[LLM] Using local: provider={Settings.Glm.Provider}");
            return new EffectiveApiKeys
            {
                OcrToken = ocrToken,
                OcrEndpoint = ocrEndpoint,
                GlmApiKey = localApiKey,
                GlmEndpoint = localEndpoint,
                GlmModel = localModel,
                GlmProvider = Settings.Glm.Provider,
                Source = "local",
                KeyVersion = 1
            };
        }

        // No local LLM keys → try cloud
        var cloudKeysForLlm = await GetCloudKeysAsync();
        if (cloudKeysForLlm != null)
        {
            var provider = !string.IsNullOrEmpty(cloudKeysForLlm.GoogleApiKey) ? "google"
                         : !string.IsNullOrEmpty(cloudKeysForLlm.ZhipuApiKey) ? "zhipu"
                         : !string.IsNullOrEmpty(cloudKeysForLlm.NvidiaApiKey) ? "nvidia"
                         : !string.IsNullOrEmpty(cloudKeysForLlm.CerebrasApiKey) ? "cerebras"
                         : Settings.Glm.Provider;

            var cloudKeyConfig = GetCloudKeysForProvider(cloudKeysForLlm, provider);
            if (cloudKeyConfig.HasValue)
            {
                var (cloudApiKey, cloudEndpoint, cloudModel) = cloudKeyConfig.Value;
                LogHelper.Log($"[LLM] Using cloud: provider={provider}, model={cloudModel}");
                return new EffectiveApiKeys
                {
                    OcrToken = ocrToken,
                    OcrEndpoint = ocrEndpoint,
                    GlmApiKey = cloudApiKey,
                    GlmEndpoint = cloudEndpoint,
                    GlmModel = cloudModel,
                    GlmProvider = provider,
                    Source = "cloud",
                    KeyVersion = cloudKeysForLlm.Version
                };
            }
        }

        // Final fallback: local config even if incomplete
        var (fbApiKey, fbEndpoint, fbModel, _) = Settings.Glm.GetActiveConfig();
        LogHelper.Log($"[LLM] Fallback to local (incomplete): provider={Settings.Glm.Provider}");
        return new EffectiveApiKeys
        {
            OcrToken = ocrToken,
            OcrEndpoint = ocrEndpoint,
            GlmApiKey = fbApiKey,
            GlmEndpoint = fbEndpoint,
            GlmModel = fbModel,
            GlmProvider = Settings.Glm.Provider,
            Source = "local",
            KeyVersion = 1
        };
    }

    private async Task<CloudKeyConfig?> GetCloudKeysAsync()
    {
        var authState = _authService != null ? await _authService.GetAuthStateAsync() : null;
        if (authState?.IsAuthenticated != true || !authState.CloudKeysAvailable || _cloudKeyService == null)
        {
            LogHelper.Log($"[Cloud] Skipping: Auth={authState?.IsAuthenticated}, KeysAvail={authState?.CloudKeysAvailable}, Service={_cloudKeyService != null}");
            return null;
        }

        try
        {
            var cloudKeys = await _cloudKeyService.GetCachedCloudKeysAsync();
            if (cloudKeys != null && _cloudKeyService.IsCloudKeyValid(cloudKeys))
                return cloudKeys;

            LogHelper.Log($"[Cloud] Keys not available: Cached={cloudKeys != null}");
        }
        catch (Exception ex)
        {
            LogHelper.Log($"[Cloud] Error: {ex.Message}");
        }
        return null;
    }

    private (string ApiKey, string Endpoint, string Model)? GetCloudKeysForProvider(CloudKeyConfig config, string provider)
    {
        return provider switch
        {
            "nvidia" when !string.IsNullOrEmpty(config.NvidiaApiKey) && !string.IsNullOrEmpty(config.NvidiaEndpoint) && !string.IsNullOrEmpty(config.NvidiaModel)
                => (config.NvidiaApiKey!, config.NvidiaEndpoint!, config.NvidiaModel!),
            "cerebras" when !string.IsNullOrEmpty(config.CerebrasApiKey) && !string.IsNullOrEmpty(config.CerebrasEndpoint) && !string.IsNullOrEmpty(config.CerebrasModel)
                => (config.CerebrasApiKey!, config.CerebrasEndpoint!, config.CerebrasModel!),
            "google" when !string.IsNullOrEmpty(config.GoogleApiKey) && !string.IsNullOrEmpty(config.GoogleEndpoint) && !string.IsNullOrEmpty(config.GoogleModel)
                => (config.GoogleApiKey!, config.GoogleEndpoint!, config.GoogleModel!),
            "zhipu" when !string.IsNullOrEmpty(config.ZhipuApiKey) && !string.IsNullOrEmpty(config.ZhipuEndpoint) && !string.IsNullOrEmpty(config.ZhipuModel)
                => (config.ZhipuApiKey!, config.ZhipuEndpoint!, config.ZhipuModel!),
            _ => null
        };
    }
}