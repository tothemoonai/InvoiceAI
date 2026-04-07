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

    private void SetupFileDefaults(params string[] files)
    {
        _fileMock.Setup(f => f.FilterSupportedFiles(It.IsAny<IEnumerable<string>>()))
            .Returns((IReadOnlyList<string>)files.ToList());
        foreach (var f in files)
        {
            _fileMock.Setup(x => x.PrepareForOcrAsync(f)).ReturnsAsync(f);
            _fileMock.Setup(x => x.ComputeFileHashAsync(f)).ReturnsAsync("hash_" + f);
        }
        _invoiceMock.Setup(i => i.ExistsByHashAsync(It.IsAny<string>())).ReturnsAsync(false);
        _invoiceMock.Setup(i => i.SaveAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice inv) => { inv.Id = 1; return inv; });
    }

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
        SetupFileDefaults("invoice.jpg");
        _invoiceMock.Setup(i => i.ExistsByHashAsync("hash_invoice.jpg")).ReturnsAsync(true);

        var vm = CreateVm();
        await vm.ProcessFilesCommand.ExecuteAsync(new[] { "invoice.jpg" });

        Assert.Empty(vm.Results);
        _ocrMock.Verify(o => o.RecognizeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessFilesAsync_ValidFile_ProcessesSuccessfully()
    {
        var response = CreateValidResponse();
        SetupFileDefaults("invoice.jpg");
        _ocrMock.Setup(o => o.RecognizeAsync("invoice.jpg")).ReturnsAsync("OCR text");
        _glmMock.Setup(g => g.ProcessBatchAsync(It.Is<string[]>(a => a.Length == 1 && a[0] == "OCR text")))
            .ReturnsAsync(new List<GlmInvoiceResponse> { response });

        var vm = CreateVm();
        await vm.ProcessFilesCommand.ExecuteAsync(new[] { "invoice.jpg" });

        Assert.Single(vm.Results);
        Assert.Equal("テスト株式会社", vm.Results[0].IssuerName);
        Assert.Equal(InvoiceType.Standard, vm.Results[0].InvoiceType);
    }

    [Fact]
    public async Task ProcessFilesAsync_OcrError_SkipsFailedFile()
    {
        SetupFileDefaults("bad.jpg", "good.jpg");
        _ocrMock.Setup(o => o.RecognizeAsync("bad.jpg")).ThrowsAsync(new Exception("OCR failed"));
        _ocrMock.Setup(o => o.RecognizeAsync("good.jpg")).ReturnsAsync("good OCR");
        _glmMock.Setup(g => g.ProcessBatchAsync(It.Is<string[]>(a => a.Length == 1 && a[0] == "good OCR")))
            .ReturnsAsync(new List<GlmInvoiceResponse> { CreateValidResponse() });

        var vm = CreateVm();
        await vm.ProcessFilesCommand.ExecuteAsync(new[] { "bad.jpg", "good.jpg" });

        Assert.Single(vm.Results);
        Assert.Contains("失败", vm.ImportItems[0].Status);
        Assert.Equal("✅ 完成", vm.ImportItems[1].Status);
    }

    [Fact]
    public async Task ProcessFilesAsync_SetsStatusOnCompletion()
    {
        SetupFileDefaults("a.jpg");
        _ocrMock.Setup(o => o.RecognizeAsync("a.jpg")).ReturnsAsync("text");
        _glmMock.Setup(g => g.ProcessBatchAsync(It.Is<string[]>(a => a[0] == "text")))
            .ReturnsAsync(new List<GlmInvoiceResponse> { CreateValidResponse() });

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
