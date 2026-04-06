using System.Text.Json;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;
using Moq;

namespace InvoiceAI.Core.Tests;

public class InvoiceDetailViewModelTests
{
    private readonly Mock<IInvoiceService> _invoiceMock = new();
    private readonly Mock<IAppSettingsService> _settingsMock = new();

    private InvoiceDetailViewModel CreateVm()
    {
        _settingsMock.Setup(s => s.Settings)
            .Returns(new InvoiceAI.Core.Helpers.AppSettings());
        return new InvoiceDetailViewModel(_invoiceMock.Object, _settingsMock.Object);
    }

    [Fact]
    public void CurrentInvoice_SetToStandard_SetsCorrectDisplay()
    {
        var vm = CreateVm();
        var invoice = new Invoice
        {
            IssuerName = "テスト",
            InvoiceType = InvoiceType.Standard,
            ItemsJson = "[]",
            MissingFields = "[]"
        };
        vm.CurrentInvoice = invoice;
        Assert.Equal("✅ 標準インボイス", vm.InvoiceTypeDisplay);
        Assert.Equal("#4CAF50", vm.InvoiceTypeColor);
    }

    [Fact]
    public void CurrentInvoice_SetToSimplified_SetsCorrectDisplay()
    {
        var vm = CreateVm();
        var invoice = new Invoice
        {
            InvoiceType = InvoiceType.Simplified,
            ItemsJson = "[]"
        };
        vm.CurrentInvoice = invoice;
        Assert.Equal("⚠ 簡易インボイス", vm.InvoiceTypeDisplay);
        Assert.Equal("#FF9800", vm.InvoiceTypeColor);
    }

    [Fact]
    public void CurrentInvoice_SetToNonQualified_SetsCorrectDisplay()
    {
        var vm = CreateVm();
        var invoice = new Invoice
        {
            InvoiceType = InvoiceType.NonQualified,
            ItemsJson = "[]"
        };
        vm.CurrentInvoice = invoice;
        Assert.Equal("✗ 非适格", vm.InvoiceTypeDisplay);
        Assert.Equal("#F44336", vm.InvoiceTypeColor);
    }

    [Fact]
    public void CurrentInvoice_WithItems_ParsesItemJson()
    {
        var vm = CreateVm();
        var items = new[]
        {
            new InvoiceItem { Name = "電気", Amount = 5000, TaxRate = 10 },
            new InvoiceItem { Name = "ガス", Amount = 3000, TaxRate = 10 }
        };
        var invoice = new Invoice
        {
            InvoiceType = InvoiceType.Standard,
            ItemsJson = JsonSerializer.Serialize(items)
        };
        vm.CurrentInvoice = invoice;
        Assert.Equal(2, vm.InvoiceItems.Count);
        Assert.Equal("電気", vm.InvoiceItems[0].Name);
        Assert.Equal(3000, vm.InvoiceItems[1].Amount);
    }

    [Fact]
    public void CurrentInvoice_InvalidItemsJson_SetsEmptyList()
    {
        var vm = CreateVm();
        var invoice = new Invoice
        {
            InvoiceType = InvoiceType.Standard,
            ItemsJson = "not valid json{{"
        };
        vm.CurrentInvoice = invoice;
        Assert.Empty(vm.InvoiceItems);
    }

    [Fact]
    public void CurrentInvoice_WithMissingFields_DisplaysThem()
    {
        var vm = CreateVm();
        var invoice = new Invoice
        {
            InvoiceType = InvoiceType.NonQualified,
            ItemsJson = "[]",
            MissingFields = "[\"2\", \"5\", \"6\"]"
        };
        vm.CurrentInvoice = invoice;
        Assert.Contains("2", vm.MissingFieldsDisplay);
        Assert.Contains("5", vm.MissingFieldsDisplay);
        Assert.Contains("6", vm.MissingFieldsDisplay);
    }

    [Fact]
    public async Task SaveAsync_MarksAsConfirmed()
    {
        _invoiceMock.Setup(i => i.UpdateAsync(It.IsAny<Invoice>()))
            .Returns(Task.CompletedTask);
        var vm = CreateVm();
        var invoice = new Invoice { Id = 1, IsConfirmed = false };
        vm.CurrentInvoice = invoice;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(invoice.IsConfirmed);
        _invoiceMock.Verify(i => i.UpdateAsync(invoice), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_NullInvoice_DoesNothing()
    {
        var vm = CreateVm();
        await vm.SaveCommand.ExecuteAsync(null);
        _invoiceMock.Verify(i => i.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
    }
}
