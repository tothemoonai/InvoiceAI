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
                if (cloudKeys != null && _cloudKeyService.IsCloudKeyValid(cloudKeys))
                {
                    // Find which provider has keys in cloud config (priority: zhipu > nvidia > cerebras)
                    var provider = !string.IsNullOrEmpty(cloudKeys.ZhipuApiKey) ? "zhipu"
                                 : !string.IsNullOrEmpty(cloudKeys.NvidiaApiKey) ? "nvidia"
                                 : !string.IsNullOrEmpty(cloudKeys.CerebrasApiKey) ? "cerebras"
                                 : Settings.Glm.Provider; // Fallback to local settings provider

                    // Use cloud keys for the provider
                    var cloudKeyConfig = GetCloudKeysForProvider(cloudKeys, provider);
                    if (cloudKeyConfig.HasValue)
                    {
                        var (cloudApiKey, cloudEndpoint, cloudModel) = cloudKeyConfig.Value;
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
                }
            }
            catch
            {
                // Fall through to local keys on any error
            }
        }

        // Fallback to local configuration
        var (localApiKey, localEndpoint, localModel, _) = Settings.Glm.GetActiveConfig();
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
            "nvidia" when !string.IsNullOrEmpty(config.NvidiaApiKey) => (config.NvidiaApiKey!, config.NvidiaEndpoint!, config.NvidiaModel!),
            "cerebras" when !string.IsNullOrEmpty(config.CerebrasApiKey) => (config.CerebrasApiKey!, config.CerebrasEndpoint!, config.CerebrasModel!),
            "zhipu" when !string.IsNullOrEmpty(config.ZhipuApiKey) => (config.ZhipuApiKey!, config.ZhipuEndpoint!, config.ZhipuModel!),
            _ => null
        };
    }
}