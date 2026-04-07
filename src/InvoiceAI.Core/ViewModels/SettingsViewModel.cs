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

    private string _previousGlmProvider = "zhipu";

    public SettingsViewModel(
        IAppSettingsService settingsService,
        IBaiduOcrService? ocrService = null,
        IGlmService? glmService = null,
        HttpClient? httpClient = null)
    {
        _settingsService = settingsService;
        _ocrService = ocrService;
        _glmService = glmService;
        _httpClient = httpClient;
        var s = _settingsService.Settings;
        _baiduToken = s.BaiduOcr.Token;
        _baiduEndpoint = s.BaiduOcr.Endpoint;
        _glmProvider = s.Glm.Provider;
        _previousGlmProvider = s.Glm.Provider;
        _glmApiKey = s.Glm.ApiKey;
        _glmEndpoint = s.Glm.Endpoint;
        _glmModel = s.Glm.Model;
        _selectedLanguage = s.Language;
        _categories = new ObservableCollection<string>(s.Categories);
    }

    [ObservableProperty] private string _baiduToken = string.Empty;
    [ObservableProperty] private string _baiduEndpoint = string.Empty;
    [ObservableProperty] private string _glmProvider = "zhipu";
    [ObservableProperty] private string _glmApiKey = string.Empty;
    [ObservableProperty] private string _glmEndpoint = string.Empty;
    [ObservableProperty] private string _glmModel = string.Empty;

    partial void OnGlmProviderChanged(string value)
    {
        if (value == _previousGlmProvider) return;
        var s = _settingsService.Settings;
        // Save current UI fields to old provider's storage
        if (_previousGlmProvider == "nvidia")
        {
            s.Glm.NvidiaApiKey = GlmApiKey;
            s.Glm.NvidiaEndpoint = GlmEndpoint;
            s.Glm.NvidiaModel = GlmModel;
        }
        else
        {
            s.Glm.ApiKey = GlmApiKey;
            s.Glm.Endpoint = GlmEndpoint;
            s.Glm.Model = GlmModel;
        }
        // Update active provider in memory so GlmService picks it up immediately
        s.Glm.Provider = value;
        // Load new provider's values into UI
        if (value == "nvidia")
        {
            GlmApiKey = s.Glm.NvidiaApiKey;
            GlmEndpoint = s.Glm.NvidiaEndpoint;
            GlmModel = s.Glm.NvidiaModel;
        }
        else
        {
            GlmApiKey = s.Glm.ApiKey;
            GlmEndpoint = s.Glm.Endpoint;
            GlmModel = s.Glm.Model;
        }
        _previousGlmProvider = value;
    }
    [ObservableProperty] private string _selectedLanguage = "zh";
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
        if (s.Glm.Provider == "nvidia")
        {
            GlmApiKey = s.Glm.NvidiaApiKey;
            GlmEndpoint = s.Glm.NvidiaEndpoint;
            GlmModel = s.Glm.NvidiaModel;
        }
        else
        {
            GlmApiKey = s.Glm.ApiKey;
            GlmEndpoint = s.Glm.Endpoint;
            GlmModel = s.Glm.Model;
        }
        SelectedLanguage = s.Language;
        Categories = new ObservableCollection<string>(s.Categories);
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _settingsService.Settings;
        s.BaiduOcr.Token = BaiduToken;
        s.BaiduOcr.Endpoint = BaiduEndpoint;
        s.Glm.Provider = GlmProvider;
        // Save UI fields to active provider
        if (GlmProvider == "nvidia")
        {
            s.Glm.NvidiaApiKey = GlmApiKey;
            s.Glm.NvidiaEndpoint = GlmEndpoint;
            s.Glm.NvidiaModel = GlmModel;
        }
        else
        {
            s.Glm.ApiKey = GlmApiKey;
            s.Glm.Endpoint = GlmEndpoint;
            s.Glm.Model = GlmModel;
        }
        s.Language = SelectedLanguage;
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
            TestResult = "请填写 GLM API Key";
            return;
        }

        try
        {
            TestResult = "正在测试 GLM 连接...";
            var settings = _settingsService.Settings;
            // Save current UI fields to active provider before testing
            if (GlmProvider == "nvidia")
            {
                settings.Glm.NvidiaApiKey = GlmApiKey;
                settings.Glm.NvidiaEndpoint = GlmEndpoint;
                settings.Glm.NvidiaModel = GlmModel;
            }
            else
            {
                settings.Glm.ApiKey = GlmApiKey;
                settings.Glm.Endpoint = GlmEndpoint;
                settings.Glm.Model = GlmModel;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, GlmEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {GlmApiKey}");
            request.Content = new StringContent(
                $"{{\"model\":\"{GlmModel}\",\"messages\":[{{\"role\":\"user\",\"content\":\"hi\"}}]}}",
                System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                TestResult = "GLM 连接成功";
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                TestResult = "GLM API Key 无效";
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                TestResult = "GLM 无权限或配额用尽";
            else
                TestResult = $"GLM 连接失败: HTTP {(int)response.StatusCode} - {body}";
        }
        catch (Exception ex)
        {
            TestResult = $"GLM 连接异常: {ex.Message}";
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
