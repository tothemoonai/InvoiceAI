using System.Collections.ObjectModel;
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
        _baiduApiKey = s.BaiduOcr.ApiKey;
        _baiduSecretKey = s.BaiduOcr.SecretKey;
        _glmApiKey = s.Glm.ApiKey;
        _glmEndpoint = s.Glm.Endpoint;
        _glmModel = s.Glm.Model;
        _selectedLanguage = s.Language;
        _categories = new ObservableCollection<string>(s.Categories);
    }

    [ObservableProperty] private string _baiduApiKey = string.Empty;
    [ObservableProperty] private string _baiduSecretKey = string.Empty;
    [ObservableProperty] private string _glmApiKey = string.Empty;
    [ObservableProperty] private string _glmEndpoint = string.Empty;
    [ObservableProperty] private string _glmModel = string.Empty;
    [ObservableProperty] private string _selectedLanguage = "zh";
    [ObservableProperty] private ObservableCollection<string> _categories = [];
    [ObservableProperty] private string _newCategory = string.Empty;
    [ObservableProperty] private string _testResult = string.Empty;

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
        s.BaiduOcr.ApiKey = BaiduApiKey;
        s.BaiduOcr.SecretKey = BaiduSecretKey;
        s.Glm.ApiKey = GlmApiKey;
        s.Glm.Endpoint = GlmEndpoint;
        s.Glm.Model = GlmModel;
        s.Language = SelectedLanguage;
        s.Categories = Categories.ToList();
        await _settingsService.SaveAsync();
        TestResult = "设置已保存";
    }

    [RelayCommand]
    private async Task TestBaiduConnectionAsync()
    {
        if (_ocrService == null || _httpClient == null)
        {
            TestResult = "OCR 服务不可用";
            return;
        }

        if (string.IsNullOrWhiteSpace(BaiduApiKey) || string.IsNullOrWhiteSpace(BaiduSecretKey))
        {
            TestResult = "请填写百度 API Key 和 Secret Key";
            return;
        }

        try
        {
            TestResult = "正在测试百度 OCR 连接...";
            // Temporarily apply keys and test
            var settings = _settingsService.Settings;
            settings.BaiduOcr.ApiKey = BaiduApiKey;
            settings.BaiduOcr.SecretKey = BaiduSecretKey;

            // Try to get access token
            var url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={BaiduApiKey}&client_secret={BaiduSecretKey}";
            var response = await _httpClient.PostAsync(url, null);
            TestResult = response.IsSuccessStatusCode
                ? "百度 OCR 连接成功"
                : $"百度 OCR 连接失败: {response.StatusCode}";
        }
        catch (Exception ex)
        {
            TestResult = $"百度 OCR 连接异常: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TestGlmConnectionAsync()
    {
        if (_glmService == null)
        {
            TestResult = "GLM 服务不可用";
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
            // Temporarily apply keys
            var settings = _settingsService.Settings;
            settings.Glm.ApiKey = GlmApiKey;
            settings.Glm.Endpoint = GlmEndpoint;
            settings.Glm.Model = GlmModel;

            var result = await _glmService.ProcessInvoiceAsync("测试文本：这是一个测试连接");
            TestResult = $"GLM 连接成功: {result.IssuerName}";
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
