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
        vm.BaiduApiKey = "test-key";
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
        vm.BaiduApiKey = "";
        vm.BaiduSecretKey = "";
        await vm.TestBaiduConnectionCommand.ExecuteAsync(null);
        Assert.Contains("请填写", vm.TestResult);
    }

    [Fact]
    public async Task TestGlmConnectionAsync_NoKey_ShowsError()
    {
        var glmMock = new Mock<IGlmService>();
        var vm = new SettingsViewModel(CreateSettingsService(), glmService: glmMock.Object);
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
}
