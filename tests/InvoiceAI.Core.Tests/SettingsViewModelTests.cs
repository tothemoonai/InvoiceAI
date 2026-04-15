using System.Net;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using Moq;

namespace InvoiceAI.Core.Tests;

public class SettingsViewModelTests
{
    private static IAppSettingsService CreateSettingsService()
    {
        var mock = new Mock<IAppSettingsService>();
        mock.Setup(s => s.Settings).Returns(new AppSettings());
        return mock.Object;
    }

    [Fact]
    public void Constructor_LoadsDefaultSettings()
    {
        var settings = CreateSettingsService();
        var vm = new SettingsViewModel(settings);
        Assert.Equal("zh", vm.SelectedLanguage);
        Assert.NotEmpty(vm.Categories);
        Assert.True(vm.Categories.Contains("電気・ガス"));
    }

    [Fact]
    public void IsChineseLanguage_SetTrue_SetsLanguage()
    {
        var vm = new SettingsViewModel(CreateSettingsService());
        vm.SelectedLanguage = "en";
        vm.IsChineseLanguage = true;
        Assert.Equal("zh", vm.SelectedLanguage);
    }

    [Fact]
    public void IsJapaneseLanguage_SetTrue_SetsLanguage()
    {
        var vm = new SettingsViewModel(CreateSettingsService());
        vm.IsJapaneseLanguage = true;
        Assert.Equal("ja", vm.SelectedLanguage);
    }

    [Fact]
    public void AddCategory_NewCategory_AddsToList()
    {
        var vm = new SettingsViewModel(CreateSettingsService());
        var initialCount = vm.Categories.Count;
        vm.NewCategory = "新カテゴリ";
        vm.AddCategoryCommand.Execute(null);
        Assert.Equal(initialCount + 1, vm.Categories.Count);
        Assert.True(vm.Categories.Contains("新カテゴリ"));
        Assert.Empty(vm.NewCategory);
    }

    [Fact]
    public void AddCategory_DuplicateCategory_DoesNotAdd()
    {
        var vm = new SettingsViewModel(CreateSettingsService());
        var existingCat = vm.Categories[0];
        var initialCount = vm.Categories.Count;
        vm.NewCategory = existingCat;
        vm.AddCategoryCommand.Execute(null);
        Assert.Equal(initialCount, vm.Categories.Count);
    }

    [Fact]
    public void AddCategory_EmptyName_DoesNotAdd()
    {
        var vm = new SettingsViewModel(CreateSettingsService());
        var initialCount = vm.Categories.Count;
        vm.NewCategory = "";
        vm.AddCategoryCommand.Execute(null);
        Assert.Equal(initialCount, vm.Categories.Count);
    }

    [Fact]
    public void RemoveCategory_ExistingCategory_RemovesIt()
    {
        var vm = new SettingsViewModel(CreateSettingsService());
        var cat = vm.Categories[0];
        var initialCount = vm.Categories.Count;
        vm.RemoveCategoryCommand.Execute(cat);
        Assert.Equal(initialCount - 1, vm.Categories.Count);
        Assert.DoesNotContain(cat, vm.Categories);
    }

    [Fact]
    public async Task SaveAsync_PersistsSettings()
    {
        var settingsMock = new Mock<IAppSettingsService>();
        settingsMock.Setup(s => s.Settings).Returns(new AppSettings());
        settingsMock.Setup(s => s.SaveAsync()).Returns(Task.CompletedTask);
        var vm = new SettingsViewModel(settingsMock.Object);
        vm.BaiduToken = "test-token";
        vm.GlmApiKey = "glm-key";
        await vm.SaveCommand.ExecuteAsync(null);
        settingsMock.Verify(s => s.SaveAsync(), Times.Once);
        Assert.Equal("设置已保存", vm.TestResult);
    }

    [Fact]
    public async Task TestBaiduConnectionAsync_NoKeys_ShowsError()
    {
        var ocrMock = new Mock<IBaiduOcrService>();
        var vm = new SettingsViewModel(CreateSettingsService(), ocrMock.Object, httpClient: new HttpClient());
        vm.BaiduToken = "";
        vm.BaiduEndpoint = "";
        await vm.TestBaiduConnectionCommand.ExecuteAsync(null);
        Assert.Contains("请填写", vm.TestResult);
    }

    [Fact]
    public async Task TestGlmConnectionAsync_NoKey_ShowsError()
    {
        var glmMock = new Mock<IGlmService>();
        var vm = new SettingsViewModel(CreateSettingsService(), glmService: glmMock.Object, httpClient: new HttpClient());
        vm.GlmApiKey = "";
        await vm.TestGlmConnectionCommand.ExecuteAsync(null);
        Assert.Contains("请填写", vm.TestResult);
    }

    [Fact]
    public async Task TestBaiduConnectionAsync_NoOcrService_ShowsUnavailable()
    {
        var vm = new SettingsViewModel(CreateSettingsService(), ocrService: null);
        await vm.TestBaiduConnectionCommand.ExecuteAsync(null);
        Assert.Contains("不可用", vm.TestResult);
    }

    [Fact]
    public async Task TestGlmConnectionAsync_NoGlmService_ShowsUnavailable()
    {
        var vm = new SettingsViewModel(CreateSettingsService(), glmService: null);
        await vm.TestGlmConnectionCommand.ExecuteAsync(null);
        Assert.Contains("不可用", vm.TestResult);
    }

    [Fact]
    public void Constructor_DefaultProvider_IsZhipu()
    {
        var vm = new SettingsViewModel(CreateSettingsService());
        Assert.Equal("zhipu", vm.GlmProvider);
        Assert.True(vm.IsZhipuProvider);
        Assert.False(vm.IsNvidiaProvider);
    }

    [Fact]
    public void GlmProvider_SwitchToNvidia_UpdatesFields()
    {
        var mock = new Mock<IAppSettingsService>();
        var settings = new AppSettings();
        settings.Glm.NvidiaApiKey = "nvidia-test-key";
        settings.Glm.NvidiaEndpoint = "https://integrate.api.nvidia.com/v1/chat/completions";
        settings.Glm.NvidiaModel = "z-ai/glm4.7";
        mock.Setup(s => s.Settings).Returns(settings);
        var vm = new SettingsViewModel(mock.Object);

        vm.GlmProvider = "nvidia";
        Assert.Equal("nvidia-test-key", vm.GlmApiKey);
        Assert.Equal("z-ai/glm4.7", vm.GlmModel);
        Assert.True(vm.IsNvidiaProvider);
        Assert.False(vm.IsZhipuProvider);
    }

    [Fact]
    public void GlmProvider_SwitchSavesOldProviderFields()
    {
        var mock = new Mock<IAppSettingsService>();
        var settings = new AppSettings();
        mock.Setup(s => s.Settings).Returns(settings);
        var vm = new SettingsViewModel(mock.Object);

        vm.GlmApiKey = "zhipu-key";
        vm.GlmEndpoint = "https://zhipu.test";
        vm.GlmModel = "glm-test";
        vm.GlmProvider = "nvidia";

        // Old provider (zhipu) fields should be saved
        Assert.Equal("zhipu-key", settings.Glm.ApiKey);
        Assert.Equal("https://zhipu.test", settings.Glm.Endpoint);
        Assert.Equal("glm-test", settings.Glm.Model);
    }

    [Fact]
    public async Task SaveAsync_WithNvidiaProvider_SavesNvidiaFields()
    {
        var settingsMock = new Mock<IAppSettingsService>();
        settingsMock.Setup(s => s.Settings).Returns(new AppSettings());
        settingsMock.Setup(s => s.SaveAsync()).Returns(Task.CompletedTask);
        var vm = new SettingsViewModel(settingsMock.Object);

        vm.GlmProvider = "nvidia";
        vm.GlmApiKey = "nvidia-key";
        vm.GlmEndpoint = "https://nvidia.test";
        vm.GlmModel = "z-ai/glm4.7";
        await vm.SaveCommand.ExecuteAsync(null);

        var s = settingsMock.Object.Settings;
        Assert.Equal("nvidia", s.Glm.Provider);
        Assert.Equal("nvidia-key", s.Glm.NvidiaApiKey);
        Assert.Equal("https://nvidia.test", s.Glm.NvidiaEndpoint);
        Assert.Equal("z-ai/glm4.7", s.Glm.NvidiaModel);
    }

    [Fact]
    public void GetActiveConfig_Zhipu_ReturnsZhipuValues()
    {
        var settings = new AppSettings();
        settings.Glm.ApiKey = "zhipu-key";
        settings.Glm.Model = "glm-4.7";
        var (apiKey, endpoint, model, maxTokens) = settings.Glm.GetActiveConfig();
        Assert.Equal("zhipu-key", apiKey);
        Assert.Equal("glm-4.7", model);
        Assert.Equal(100000, maxTokens);
    }

    [Fact]
    public void GetActiveConfig_Nvidia_ReturnsNvidiaValues()
    {
        var settings = new AppSettings();
        settings.Glm.Provider = "nvidia";
        settings.Glm.NvidiaApiKey = "nvidia-key";
        settings.Glm.NvidiaModel = "z-ai/glm4.7";
        var (apiKey, endpoint, model, maxTokens) = settings.Glm.GetActiveConfig();
        Assert.Equal("nvidia-key", apiKey);
        Assert.Equal("z-ai/glm4.7", model);
        Assert.Equal(32768, maxTokens);
    }

    [Fact]
    public void GlmSettings_VerifiedProviders_DefaultsToEmptyList()
    {
        // Arrange & Act
        var settings = new AppSettings();

        // Assert
        Assert.NotNull(settings.Glm.VerifiedProviders);
        Assert.Empty(settings.Glm.VerifiedProviders);
    }
}
