using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Services;

namespace InvoiceAI.Core.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly IBaiduOcrService? _ocrService;
    private readonly IGlmService? _glmService;
    private readonly HttpClient? _httpClient;
    private readonly IProviderFallbackManager? _fallbackManager;

    private string _previousGlmProvider = "zhipu";

    public SettingsViewModel(
        IAppSettingsService settingsService,
        IBaiduOcrService? ocrService = null,
        IGlmService? glmService = null,
        HttpClient? httpClient = null,
        IProviderFallbackManager? fallbackManager = null)
    {
        _settingsService = settingsService;
        _ocrService = ocrService;
        _glmService = glmService;
        _httpClient = httpClient;
        _fallbackManager = fallbackManager;
        var s = _settingsService.Settings;
        _baiduToken = s.BaiduOcr.Token;
        _baiduEndpoint = s.BaiduOcr.Endpoint;
        _glmProvider = s.Glm.Provider;
        _previousGlmProvider = s.Glm.Provider;
        _autoSaveAfterExport = s.AutoSaveAfterExport;
        _exportPath = s.ExportPath;
        _invoiceArchivePath = s.InvoiceArchivePath;
        if (s.Glm.Provider == "nvidia")
        {
            _glmApiKey = s.Glm.NvidiaApiKey;
            _glmEndpoint = s.Glm.NvidiaEndpoint;
            _glmModel = s.Glm.NvidiaModel;
        }
        else if (s.Glm.Provider == "cerebras")
        {
            _glmApiKey = s.Glm.CerebrasApiKey;
            _glmEndpoint = s.Glm.CerebrasEndpoint;
            _glmModel = s.Glm.CerebrasModel;
        }
        else if (s.Glm.Provider == "google")
        {
            _glmApiKey = s.Glm.GoogleApiKey;
            _glmEndpoint = s.Glm.GoogleEndpoint;
            _glmModel = s.Glm.GoogleModel;
        }
        else
        {
            _glmApiKey = s.Glm.ApiKey;
            _glmEndpoint = s.Glm.Endpoint;
            _glmModel = s.Glm.Model;
        }
        _selectedLanguage = s.Language;
        _categories = new ObservableCollection<string>(s.Categories);
        RefreshModelList(s);
    }

    [ObservableProperty] private string _baiduToken = string.Empty;
    [ObservableProperty] private string _baiduEndpoint = string.Empty;
    [ObservableProperty] private string _glmProvider = "zhipu";
    [ObservableProperty] private string _glmApiKey = string.Empty;
    [ObservableProperty] private string _glmEndpoint = string.Empty;
    [ObservableProperty] private string _glmModel = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _availableModels = [];
    [ObservableProperty] private int _selectedModelIndex = -1;
    [ObservableProperty] private bool _autoSaveAfterExport;
    [ObservableProperty] private string _exportPath = string.Empty;
    [ObservableProperty] private string _invoiceArchivePath = string.Empty;

    partial void OnGlmProviderChanged(string value)
    {
        if (value == _previousGlmProvider) return;
        var s = _settingsService.Settings;
        // Save current UI fields to old provider's storage
        switch (_previousGlmProvider)
        {
            case "nvidia":
                s.Glm.NvidiaApiKey = GlmApiKey;
                s.Glm.NvidiaEndpoint = GlmEndpoint;
                s.Glm.NvidiaModel = GlmModel;
                break;
            case "cerebras":
                s.Glm.CerebrasApiKey = GlmApiKey;
                s.Glm.CerebrasEndpoint = GlmEndpoint;
                s.Glm.CerebrasModel = GlmModel;
                break;
            case "google":
                s.Glm.GoogleApiKey = GlmApiKey;
                s.Glm.GoogleEndpoint = GlmEndpoint;
                s.Glm.GoogleModel = GlmModel;
                break;
            default:
                s.Glm.ApiKey = GlmApiKey;
                s.Glm.Endpoint = GlmEndpoint;
                s.Glm.Model = GlmModel;
                break;
        }
        // Update active provider in memory so GlmService picks it up immediately
        s.Glm.Provider = value;
        // Load new provider's values into UI
        switch (value)
        {
            case "nvidia":
                GlmApiKey = s.Glm.NvidiaApiKey;
                GlmEndpoint = s.Glm.NvidiaEndpoint;
                GlmModel = s.Glm.NvidiaModel;
                break;
            case "cerebras":
                GlmApiKey = s.Glm.CerebrasApiKey;
                GlmEndpoint = s.Glm.CerebrasEndpoint;
                GlmModel = s.Glm.CerebrasModel;
                break;
            case "google":
                GlmApiKey = s.Glm.GoogleApiKey;
                GlmEndpoint = s.Glm.GoogleEndpoint;
                GlmModel = s.Glm.GoogleModel;
                break;
            default:
                GlmApiKey = s.Glm.ApiKey;
                GlmEndpoint = s.Glm.Endpoint;
                GlmModel = s.Glm.Model;
                break;
        }
        _previousGlmProvider = value;
        RefreshModelList(_settingsService.Settings);
    }
    [ObservableProperty] private string _selectedLanguage = "zh";
    [ObservableProperty] private string _themeMode = "Auto";
    [ObservableProperty] private ObservableCollection<string> _categories = [];
    [ObservableProperty] private string _newCategory = string.Empty;
    [ObservableProperty] private string _testResult = string.Empty;

    public void ReloadFromSettings()
    {
        var s = _settingsService.Settings;
        BaiduToken = s.BaiduOcr.Token;
        BaiduEndpoint = s.BaiduOcr.Endpoint;
        GlmProvider = s.Glm.Provider;
        _previousGlmProvider = s.Glm.Provider;
        LoadGlmFieldsForProvider(s);
        RefreshModelList(s);
        SelectedLanguage = s.Language;
        ThemeMode = s.ThemeMode;
        AutoSaveAfterExport = s.AutoSaveAfterExport;
        ExportPath = s.ExportPath;
        InvoiceArchivePath = s.InvoiceArchivePath;
        Categories.Clear();
        foreach (var cat in s.Categories) Categories.Add(cat);
    }

    private void LoadGlmFieldsForProvider(AppSettings s)
    {
        switch (s.Glm.Provider)
        {
            case "nvidia":
                GlmApiKey = s.Glm.NvidiaApiKey;
                GlmEndpoint = s.Glm.NvidiaEndpoint;
                GlmModel = s.Glm.NvidiaModel;
                break;
            case "cerebras":
                GlmApiKey = s.Glm.CerebrasApiKey;
                GlmEndpoint = s.Glm.CerebrasEndpoint;
                GlmModel = s.Glm.CerebrasModel;
                break;
            case "google":
                GlmApiKey = s.Glm.GoogleApiKey;
                GlmEndpoint = s.Glm.GoogleEndpoint;
                GlmModel = s.Glm.GoogleModel;
                break;
            default:
                GlmApiKey = s.Glm.ApiKey;
                GlmEndpoint = s.Glm.Endpoint;
                GlmModel = s.Glm.Model;
                break;
        }
    }

    private void RefreshModelList(AppSettings s)
    {
        var models = s.Glm.GetModelsForProvider();
        AvailableModels = new ObservableCollection<string>(models.Select(m => m.Name));

        // 使用新的索引系统
        var savedIndex = s.Glm.GetSelectedModelIndex();
        SelectedModelIndex = savedIndex >= 0 && savedIndex < models.Length ? savedIndex : 0;

        // 更新 GlmModel 以匹配选择（用于兼容性）
        if (SelectedModelIndex >= 0 && SelectedModelIndex < models.Length)
            GlmModel = models[SelectedModelIndex].Id;
    }

    partial void OnSelectedModelIndexChanged(int value)
    {
        if (value < 0) return;
        var models = _settingsService.Settings.Glm.GetModelsForProvider();
        if (value < models.Length)
            GlmModel = models[value].Id;
    }

    // Provider helpers
    public bool IsZhipuProvider
    {
        get => GlmProvider == "zhipu";
        set
        {
            if (!value) return;
            GlmProvider = "zhipu";
            OnPropertyChanged(nameof(IsNvidiaProvider));
            OnPropertyChanged(nameof(IsCerebrasProvider));
            OnPropertyChanged(nameof(IsGoogleProvider));
        }
    }

    public bool IsNvidiaProvider
    {
        get => GlmProvider == "nvidia";
        set
        {
            if (!value) return;
            GlmProvider = "nvidia";
            OnPropertyChanged(nameof(IsZhipuProvider));
            OnPropertyChanged(nameof(IsCerebrasProvider));
            OnPropertyChanged(nameof(IsGoogleProvider));
        }
    }

    public bool IsCerebrasProvider
    {
        get => GlmProvider == "cerebras";
        set
        {
            if (!value) return;
            GlmProvider = "cerebras";
            OnPropertyChanged(nameof(IsZhipuProvider));
            OnPropertyChanged(nameof(IsNvidiaProvider));
            OnPropertyChanged(nameof(IsGoogleProvider));
        }
    }

    public bool IsGoogleProvider
    {
        get => GlmProvider == "google";
        set
        {
            if (!value) return;
            GlmProvider = "google";
            OnPropertyChanged(nameof(IsZhipuProvider));
            OnPropertyChanged(nameof(IsNvidiaProvider));
            OnPropertyChanged(nameof(IsCerebrasProvider));
        }
    }

    // Language helpers
    public bool IsChineseLanguage
    {
        get => SelectedLanguage == "zh";
        set
        {
            if (SelectedLanguage == "zh") return;
            SelectedLanguage = "zh";
            OnPropertyChanged(nameof(IsJapaneseLanguage));
        }
    }

    public bool IsJapaneseLanguage
    {
        get => SelectedLanguage == "ja";
        set
        {
            if (SelectedLanguage == "ja") return;
            SelectedLanguage = "ja";
            OnPropertyChanged(nameof(IsChineseLanguage));
        }
    }

    // Theme helpers
    public bool IsAutoTheme
    {
        get => ThemeMode == "Auto";
        set
        {
            if (value)
            {
                ThemeMode = "Auto";
                ApplyTheme();
            }
        }
    }

    public bool IsLightTheme
    {
        get => ThemeMode == "Light";
        set
        {
            if (value)
            {
                ThemeMode = "Light";
                ApplyTheme();
            }
        }
    }

    public bool IsDarkTheme
    {
        get => ThemeMode == "Dark";
        set
        {
            if (value)
            {
                ThemeMode = "Dark";
                ApplyTheme();
            }
        }
    }

    private void ApplyTheme()
    {
        // Theme application is handled by the App layer via event
        ThemeChanged?.Invoke(this, ThemeMode);
    }

    /// <summary>Event raised when the theme mode changes. Consumed by the App layer.</summary>
    public static event EventHandler<string>? ThemeChanged;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _settingsService.Settings;
        s.BaiduOcr.Token = BaiduToken;
        s.BaiduOcr.Endpoint = BaiduEndpoint;
        s.Glm.Provider = GlmProvider;
        // Save UI fields to active provider
        switch (GlmProvider)
        {
            case "nvidia":
                s.Glm.NvidiaApiKey = GlmApiKey;
                s.Glm.NvidiaEndpoint = GlmEndpoint;
                s.Glm.NvidiaModel = GlmModel;
                s.Glm.NvidiaSelectedModelIndex = SelectedModelIndex;
                break;
            case "cerebras":
                s.Glm.CerebrasApiKey = GlmApiKey;
                s.Glm.CerebrasEndpoint = GlmEndpoint;
                s.Glm.CerebrasModel = GlmModel;
                s.Glm.CerebrasSelectedModelIndex = SelectedModelIndex;
                break;
            case "google":
                s.Glm.GoogleApiKey = GlmApiKey;
                s.Glm.GoogleEndpoint = GlmEndpoint;
                s.Glm.GoogleModel = GlmModel;
                s.Glm.GoogleSelectedModelIndex = SelectedModelIndex;
                break;
            default:
                s.Glm.ApiKey = GlmApiKey;
                s.Glm.Endpoint = GlmEndpoint;
                s.Glm.Model = GlmModel;
                s.Glm.ZhipuSelectedModelIndex = SelectedModelIndex;
                break;
        }
        s.Language = SelectedLanguage;
        s.ThemeMode = ThemeMode;
        s.AutoSaveAfterExport = AutoSaveAfterExport;
        s.ExportPath = ExportPath;
        s.InvoiceArchivePath = InvoiceArchivePath;
        s.Categories = Categories.ToList();
        await _settingsService.SaveAsync();
        TestResult = "设置已保存";
    }

    [RelayCommand]
    private async Task TestBaiduConnectionAsync()
    {
        if (_httpClient == null)
        {
            TestResult = "HTTP 客户端不可用";
            return;
        }

        if (string.IsNullOrWhiteSpace(BaiduToken) || string.IsNullOrWhiteSpace(BaiduEndpoint))
        {
            TestResult = "请填写 PaddleOCR Token 和端点地址";
            return;
        }

        try
        {
            TestResult = "正在测试 PaddleOCR 连接...";

            using var request = new HttpRequestMessage(HttpMethod.Post, BaiduEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"token {BaiduToken}");
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                TestResult = "PaddleOCR 连接成功";
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                TestResult = "PaddleOCR Token 无效或已过期";
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                TestResult = "PaddleOCR 无权限或配额用尽";
            else if ((int)response.StatusCode == 400)
                TestResult = "PaddleOCR 连接成功（端点可达，Token 有效）";
            else
                TestResult = $"PaddleOCR 连接失败: HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            TestResult = $"PaddleOCR 连接异常: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TestGlmConnectionAsync()
    {
        if (_httpClient == null)
        {
            TestResult = "HTTP 客户端不可用";
            return;
        }

        if (string.IsNullOrWhiteSpace(GlmApiKey))
        {
            TestResult = $"请填写 API Key";
            return;
        }

        try
        {
            var providerName = GlmProvider switch
            {
                "nvidia" => "NVIDIA NIM",
                "cerebras" => "Cerebras",
                "google" => "Google",
                _ => "智谱 (Zhipu)"
            };
            TestResult = $"正在测试 {providerName} 连接...";
            var settings = _settingsService.Settings;
            // Save current UI fields to active provider before testing
            switch (GlmProvider)
            {
                case "nvidia":
                    settings.Glm.NvidiaApiKey = GlmApiKey;
                    settings.Glm.NvidiaEndpoint = GlmEndpoint;
                    settings.Glm.NvidiaModel = GlmModel;
                    break;
                case "cerebras":
                    settings.Glm.CerebrasApiKey = GlmApiKey;
                    settings.Glm.CerebrasEndpoint = GlmEndpoint;
                    settings.Glm.CerebrasModel = GlmModel;
                    break;
                case "google":
                    settings.Glm.GoogleApiKey = GlmApiKey;
                    settings.Glm.GoogleEndpoint = GlmEndpoint;
                    settings.Glm.GoogleModel = GlmModel;
                    break;
                default:
                    settings.Glm.ApiKey = GlmApiKey;
                    settings.Glm.Endpoint = GlmEndpoint;
                    settings.Glm.Model = GlmModel;
                    break;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, GlmEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {GlmApiKey}");
            request.Content = new StringContent(
                $"{{\"model\":\"{GlmModel}\",\"messages\":[{{\"role\":\"user\",\"content\":\"hi\"}}]}}",
                System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                TestResult = $"{providerName} 连接成功";
                // Mark provider as verified when connection succeeds
                if (_fallbackManager != null)
                {
                    await _fallbackManager.MarkProviderVerifiedAsync(GlmProvider);
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                TestResult = $"{providerName} API Key 无效";
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                TestResult = $"{providerName} 无权限或配额用尽";
            else
                TestResult = $"{providerName} 连接失败: HTTP {(int)response.StatusCode} - {body}";
        }
        catch (Exception ex)
        {
            var providerName = GlmProvider switch
            {
                "nvidia" => "NVIDIA NIM",
                "cerebras" => "Cerebras",
                "google" => "Google",
                _ => "智谱 (Zhipu)"
            };
            TestResult = $"{providerName} 连接异常: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddCategory()
    {
        if (!string.IsNullOrWhiteSpace(NewCategory) && !Categories.Contains(NewCategory))
        {
            Categories.Add(NewCategory);
            NewCategory = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveCategory(string category) => Categories.Remove(category);
}
