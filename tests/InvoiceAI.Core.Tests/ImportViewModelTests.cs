using System.Collections.ObjectModel;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;
using Moq;

namespace InvoiceAI.Core.Tests;

public class ImportViewModelTests
{
    private readonly Mock<IBaiduOcrService> _ocrMock = new();
    private readonly Mock<IGlmService> _glmMock = new();
    private readonly Mock<IInvoiceService> _invoiceMock = new();
    private readonly Mock<IFileService> _fileMock = new();

    private ImportViewModel CreateVm()
        => new(_ocrMock.Object, _glmMock.Object, _invoiceMock.Object, _fileMock.Object);

    private static GlmInvoiceResponse CreateValidResponse() => new()
    {
        IssuerName = "テスト株式会社",
        RegistrationNumber = "T1234567890123",
        TransactionDate = "2025-01-15",
        Description = "電気料金",
        Items = [new() { Name = "電気", Amount = 5000, TaxRate = 10 }],
        TaxExcludedAmount = 5000,
        TaxIncludedAmount = 5500,
        TaxAmount = 500,
        InvoiceType = "Standard",
        MissingFields = [],
        SuggestedCategory = "電気・ガス"
    };

    [Fact]
    public async Task ProcessFilesAsync_UnsupportedFiles_NoProcessing()
    {
        _fileMock.Setup(f => f.FilterSupportedFiles(It.IsAny<string[]>()))
            .Returns((IReadOnlyList<string>)new List<string>());

        var vm = CreateVm();
        await vm.ProcessFilesCommand.ExecuteAsync(new[] { "test.txt" });

        Assert.False(vm.IsProcessing);
        Assert.Empty(vm.Results);
    }

    [Fact]
    public async Task ProcessFilesAsync_DuplicateFile_SkipsIt()
    {
        _fileMock.Setup(f => f.FilterSupportedFiles(It.IsAny<string[]>()))
            .Returns((IReadOnlyList<string>)new List<string> { "invoice.jpg" });
        _fileMock.Setup(f => f.ComputeFileHashAsync("invoice.jpg"))
            .ReturnsAsync("abc123");
        _invoiceMock.Setup(i => i.ExistsByHashAsync("abc123"))
            .ReturnsAsync(true);

        var vm = CreateVm();
        await vm.ProcessFilesCommand.ExecuteAsync(new[] { "invoice.jpg" });

        Assert.Empty(vm.Results);
        _ocrMock.Verify(o => o.RecognizeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessFilesAsync_ValidFile_ProcessesSuccessfully()
    {
        var response = CreateValidResponse();

        _fileMock.Setup(f => f.FilterSupportedFiles(It.IsAny<string[]>()))
            .Returns((IReadOnlyList<string>)new List<string> { "invoice.jpg" });
        _fileMock.Setup(f => f.ComputeFileHashAsync("invoice.jpg"))
            .ReturnsAsync("hash123");
        _invoiceMock.Setup(i => i.ExistsByHashAsync("hash123"))
            .ReturnsAsync(false);
        _ocrMock.Setup(o => o.RecognizeAsync("invoice.jpg"))
            .ReturnsAsync("OCR text");
        _glmMock.Setup(g => g.ProcessInvoiceAsync("OCR text"))
            .ReturnsAsync(response);
        _invoiceMock.Setup(i => i.SaveAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice inv) => { inv.Id = 1; return inv; });

        var vm = CreateVm();
        await vm.ProcessFilesCommand.ExecuteAsync(new[] { "invoice.jpg" });

        Assert.Single(vm.Results);
        Assert.Equal("テスト株式会社", vm.Results[0].IssuerName);
        Assert.Equal(InvoiceType.Standard, vm.Results[0].InvoiceType);
        Assert.Equal(5000, vm.Results[0].TaxExcludedAmount);
    }

    [Fact]
    public async Task ProcessFilesAsync_OcrError_ContinuesWithNextFile()
    {
        _fileMock.Setup(f => f.FilterSupportedFiles(It.IsAny<string[]>()))
            .Returns((IReadOnlyList<string>)new List<string> { "bad.jpg", "good.jpg" });
        _fileMock.Setup(f => f.ComputeFileHashAsync("bad.jpg"))
            .ReturnsAsync("hash1");
        _fileMock.Setup(f => f.ComputeFileHashAsync("good.jpg"))
            .ReturnsAsync("hash2");
        _invoiceMock.Setup(i => i.ExistsByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _ocrMock.Setup(o => o.RecognizeAsync("bad.jpg"))
            .ThrowsAsync(new Exception("OCR failed"));
        _ocrMock.Setup(o => o.RecognizeAsync("good.jpg"))
            .ReturnsAsync("good OCR");
        _glmMock.Setup(g => g.ProcessInvoiceAsync("good OCR"))
            .ReturnsAsync(CreateValidResponse());
        _invoiceMock.Setup(i => i.SaveAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice inv) => { inv.Id = 1; return inv; });

        var vm = CreateVm();
        await vm.ProcessFilesCommand.ExecuteAsync(new[] { "bad.jpg", "good.jpg" });

        Assert.Single(vm.Results);
        Assert.Contains("失败", vm.ImportItems[0].Status);
    }

    [Fact]
    public async Task ProcessFilesAsync_SetsStatusOnCompletion()
    {
        _fileMock.Setup(f => f.FilterSupportedFiles(It.IsAny<string[]>()))
            .Returns((IReadOnlyList<string>)new List<string> { "a.jpg" });
        _fileMock.Setup(f => f.ComputeFileHashAsync("a.jpg"))
            .ReturnsAsync("h");
        _invoiceMock.Setup(i => i.ExistsByHashAsync("h"))
            .ReturnsAsync(false);
        _ocrMock.Setup(o => o.RecognizeAsync("a.jpg"))
            .ReturnsAsync("text");
        _glmMock.Setup(g => g.ProcessInvoiceAsync("text"))
            .ReturnsAsync(CreateValidResponse());
        _invoiceMock.Setup(i => i.SaveAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice inv) => { inv.Id = 1; return inv; });

        var vm = CreateVm();
        await vm.ProcessFilesCommand.ExecuteAsync(new[] { "a.jpg" });

        Assert.Contains("完成", vm.StatusMessage);
    }

    [Fact]
    public void CancelCommand_SetsIsProcessingFalse()
    {
        var vm = CreateVm();
        vm.IsProcessing = true;
        vm.CancelCommand.Execute(null);
        Assert.False(vm.IsProcessing);
    }
}
