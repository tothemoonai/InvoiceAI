using System.Collections.ObjectModel;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;
using Moq;

namespace InvoiceAI.Core.Tests;

public class MainViewModelTests
{
    private readonly Mock<IInvoiceService> _invoiceMock = new();
    private readonly Mock<IFileService> _fileMock = new();
    private readonly Mock<IBaiduOcrService> _ocrMock = new();
    private readonly Mock<IGlmService> _glmMock = new();
    private readonly Mock<IExcelExportService> _excelMock = new();
    private readonly Mock<IAppSettingsService> _settingsMock = new();

    private MainViewModel CreateVm()
        => new(_invoiceMock.Object, _fileMock.Object, _ocrMock.Object,
            _glmMock.Object, _excelMock.Object, _settingsMock.Object);

    private void SetupSettings()
    {
        _settingsMock.Setup(s => s.Settings).Returns(new AppSettings());
    }

    [Fact]
    public async Task LoadDataAsync_LoadsInvoicesAndCategories()
    {
        SetupSettings();
        var invoices = new List<Invoice>
        {
            new() { Id = 1, IssuerName = "A社" },
            new() { Id = 2, IssuerName = "B社" }
        };
        _invoiceMock.Setup(i => i.GetUnconfirmedAsync()).ReturnsAsync(invoices);
        _invoiceMock.Setup(i => i.GetCategoryCountsAsync())
            .ReturnsAsync(new Dictionary<string, int> { { "電気・ガス", 3 } });

        var vm = CreateVm();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Invoices.Count);
        Assert.Equal("全部", vm.Categories[0]);
        Assert.True(vm.Categories.Count > 1);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task FilterByCategoryAsync_All_ShowsAllInvoices()
    {
        SetupSettings();
        var invoices = new List<Invoice> { new() { Id = 1 }, new() { Id = 2 } };
        _invoiceMock.Setup(i => i.GetUnconfirmedAsync()).ReturnsAsync(invoices);

        var vm = CreateVm();
        await vm.FilterByCategoryCommand.ExecuteAsync("全部");

        Assert.Equal(2, vm.Invoices.Count);
        Assert.Equal("全部", vm.SelectedCategory);
    }

    [Fact]
    public async Task FilterByCategoryAsync_SpecificCategory_Filters()
    {
        SetupSettings();
        var filtered = new List<Invoice> { new() { Id = 1, Category = "食料品" } };
        _invoiceMock.Setup(i => i.GetByCategoryAsync("食料品")).ReturnsAsync(filtered);
        var vm = CreateVm();
        await vm.FilterByCategoryCommand.ExecuteAsync("食料品");
        Assert.Single(vm.Invoices);
        Assert.Equal("食料品", vm.SelectedCategory);
    }

    [Fact]
    public async Task SearchAsync_WithText_FiltersByName()
    {
        SetupSettings();
        var all = new List<Invoice>
        {
            new() { Id = 1, IssuerName = "東京電力" },
            new() { Id = 2, IssuerName = "大阪ガス" }
        };
        _invoiceMock.Setup(i => i.GetAllAsync()).ReturnsAsync(all);
        var vm = CreateVm();
        vm.SearchText = "東京";
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.Single(vm.Invoices);
        Assert.Equal("東京電力", vm.Invoices[0].IssuerName);
    }

    [Fact]
    public async Task SearchAsync_WithText_FiltersByDescription()
    {
        SetupSettings();
        var all = new List<Invoice>
        {
            new() { Id = 1, IssuerName = "A社", Description = "電気料金" },
            new() { Id = 2, IssuerName = "B社", Description = "ガス料金" }
        };
        _invoiceMock.Setup(i => i.GetAllAsync()).ReturnsAsync(all);
        var vm = CreateVm();
        vm.SearchText = "電気";
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.Single(vm.Invoices);
    }

    [Fact]
    public async Task SearchAsync_EmptyText_ReloadsAll()
    {
        SetupSettings();
        var invoices = new List<Invoice> { new() { Id = 1 }, new() { Id = 2 } };
        _invoiceMock.Setup(i => i.GetUnconfirmedAsync()).ReturnsAsync(invoices);
        _invoiceMock.Setup(i => i.GetCategoryCountsAsync())
            .ReturnsAsync(new Dictionary<string, int>());
        var vm = CreateVm();
        vm.SearchText = "";
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Invoices.Count);
    }

    [Fact]
    public async Task DeleteInvoiceAsync_RemovesFromCollection()
    {
        SetupSettings();
        var invoice = new Invoice { Id = 5, IssuerName = "Test" };
        _invoiceMock.Setup(i => i.DeleteAsync(5)).Returns(Task.CompletedTask);
        var vm = CreateVm();
        vm.Invoices = new ObservableCollection<Invoice> { invoice };
        await vm.DeleteInvoiceCommand.ExecuteAsync(invoice);
        Assert.Empty(vm.Invoices);
        _invoiceMock.Verify(i => i.DeleteAsync(5), Times.Once);
    }

    [Fact]
    public async Task DeleteInvoiceAsync_NullInvoice_DoesNothing()
    {
        var vm = CreateVm();
        await vm.DeleteInvoiceCommand.ExecuteAsync(null);
        _invoiceMock.Verify(i => i.DeleteAsync(It.IsAny<int>()), Times.Never);
    }
}
