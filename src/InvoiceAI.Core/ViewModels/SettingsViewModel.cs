using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Services;

namespace InvoiceAI.Core.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;

    public SettingsViewModel(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
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
