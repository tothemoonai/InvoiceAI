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
        var authState = _authService != null ? await _authService.GetAuthStateAsync() : null;

        // Try cloud keys first if authenticated
        if (authState?.IsAuthenticated == true &&
            authState.CloudKeysAvailable &&
            _cloudKeyService != null)
        {
            try
            {
                var cloudKeys = await _cloudKeyService.GetCachedCloudKeysAsync();
                LogHelper.Log($"[CloudKeys] Cached={cloudKeys != null}, Valid={cloudKeys != null && _cloudKeyService.IsCloudKeyValid(cloudKeys)}");

                if (cloudKeys != null && _cloudKeyService.IsCloudKeyValid(cloudKeys))
                {
                    // Find which provider has keys in cloud config (priority: zhipu > nvidia > cerebras > google)
                    var provider = !string.IsNullOrEmpty(cloudKeys.ZhipuApiKey) ? "zhipu"
                                 : !string.IsNullOrEmpty(cloudKeys.NvidiaApiKey) ? "nvidia"
                                 : !string.IsNullOrEmpty(cloudKeys.CerebrasApiKey) ? "cerebras"
                                 : !string.IsNullOrEmpty(cloudKeys.GoogleApiKey) ? "google"
                                 : Settings.Glm.Provider; // Fallback to local settings provider

                    LogHelper.Log($"[CloudKeys] Selected provider={provider}, Zhipu={!string.IsNullOrEmpty(cloudKeys.ZhipuApiKey)}, Nvidia={!string.IsNullOrEmpty(cloudKeys.NvidiaApiKey)}, Cerebras={!string.IsNullOrEmpty(cloudKeys.CerebrasApiKey)}, Google={!string.IsNullOrEmpty(cloudKeys.GoogleApiKey)}");

                    // Use cloud keys for the provider
                    var cloudKeyConfig = GetCloudKeysForProvider(cloudKeys, provider);
                    if (cloudKeyConfig.HasValue)
                    {
                        var (cloudApiKey, cloudEndpoint, cloudModel) = cloudKeyConfig.Value;
                        LogHelper.Log($"[CloudKeys] Using cloud: provider={provider}, endpoint={cloudEndpoint}, model={cloudModel}");
                        return new EffectiveApiKeys
                        {
                            OcrToken = cloudKeys.OcrToken,
                            OcrEndpoint = cloudKeys.OcrEndpoint,
                            GlmApiKey = cloudApiKey,
                            GlmEndpoint = cloudEndpoint,
                            GlmModel = cloudModel,
                            GlmProvider = provider,
                            Source = "cloud",
                            KeyVersion = cloudKeys.Version
                        };
                    }
                    else
                    {
                        LogHelper.Log($"[CloudKeys] GetCloudKeysForProvider returned null for provider={provider}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log($"[CloudKeys] Error fetching cloud keys: {ex.Message}");
            }
        }
        else
        {
            LogHelper.Log($"[CloudKeys] Skipping cloud: Auth={authState?.IsAuthenticated}, KeysAvail={authState?.CloudKeysAvailable}, Service={_cloudKeyService != null}");
        }

        // Fallback to local configuration
        var (localApiKey, localEndpoint, localModel, _) = Settings.Glm.GetActiveConfig();
        LogHelper.Log($"[CloudKeys] Using local: provider={Settings.Glm.Provider}");
        return new EffectiveApiKeys
        {
            OcrToken = Settings.BaiduOcr.Token,
            OcrEndpoint = Settings.BaiduOcr.Endpoint,
            GlmApiKey = localApiKey,
            GlmEndpoint = localEndpoint,
            GlmModel = localModel,
            GlmProvider = Settings.Glm.Provider,
            Source = "local",
            KeyVersion = 1
        };
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