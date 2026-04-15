using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;
using Moq;

namespace InvoiceAI.Core.Tests;

public class InvoiceImportServiceFallbackTests : IDisposable
{
    private readonly Mock<IBaiduOcrService> _mockOcr;
    private readonly Mock<IGlmService> _mockGlm;
    private readonly Mock<IInvoiceService> _mockInvoice;
    private readonly Mock<IFileService> _mockFile;
    private readonly Mock<IAppSettingsService> _mockSettings;
    private readonly Mock<IProviderFallbackManager> _mockFallback;
    private readonly InvoiceImportService _service;
    private readonly AppSettings _testSettings;
    private readonly string _testFile;

    public InvoiceImportServiceFallbackTests()
    {
        _mockOcr = new Mock<IBaiduOcrService>();
        _mockGlm = new Mock<IGlmService>();
        _mockInvoice = new Mock<IInvoiceService>();
        _mockFile = new Mock<IFileService>();
        _mockSettings = new Mock<IAppSettingsService>();
        _mockFallback = new Mock<IProviderFallbackManager>();

        _testSettings = new AppSettings();
        _mockSettings.Setup(s => s.Settings).Returns(_testSettings);

        _testFile = Path.GetTempFileName() + ".jpg";
        File.WriteAllText(_testFile, "test");

        _mockFile.Setup(f => f.FilterSupportedFiles(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<string> { _testFile });
        _mockFile.Setup(f => f.ComputeFileHashAsync(It.IsAny<string>()))
            .ReturnsAsync("test-hash");
        _mockInvoice.Setup(i => i.ExistsByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockInvoice.Setup(i => i.SaveAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice inv) => inv);
        _mockOcr.Setup(o => o.RecognizeAsync(It.IsAny<string>()))
            .ReturnsAsync("OCR text");
        _mockFile.Setup(f => f.PrepareForOcrAsync(It.IsAny<string>()))
            .ReturnsAsync((string s) => s);

        _service = new InvoiceImportService(
            _mockOcr.Object,
            _mockGlm.Object,
            _mockInvoice.Object,
            _mockFile.Object,
            _mockSettings.Object,
            _mockFallback.Object);
    }

    public void Dispose()
    {
        if (File.Exists(_testFile)) File.Delete(_testFile);
    }

    [Fact]
    public async Task ImportAsync_WhenGlmFails_TriesNextProvider()
    {
        // Arrange
        _testSettings.Glm.Provider = "zhipu";
        _testSettings.Glm.VerifiedProviders.AddRange(new[] { "zhipu", "nvidia" });
        _testSettings.InvoiceArchivePath = string.Empty;

        _mockGlm.SetupSequence(g => g.ProcessBatchAsync(It.IsAny<string[]>()))
            .ThrowsAsync(new HttpRequestException("Network error"))
            .ReturnsAsync(new List<GlmInvoiceResponse> { new() });

        _mockFallback.Setup(f => f.TryGetNextProvider("zhipu", It.IsAny<string>()))
            .Returns("nvidia");

        var statusMessages = new List<string>();
        _service.StatusChanged += (s, msg) => statusMessages.Add(msg);

        // Act
        var result = await _service.ImportAsync(new[] { _testFile });

        // Assert
        _mockFallback.Verify(f => f.TryGetNextProvider("zhipu", It.IsAny<string>()), Times.Once);
        Assert.Equal("zhipu", _testSettings.Glm.Provider); // Should be restored after import
    }

    [Fact]
    public async Task ImportAsync_RestoresOriginalProvider_AfterImport()
    {
        // Arrange
        _testSettings.Glm.Provider = "zhipu";
        _testSettings.Glm.VerifiedProviders.Add("zhipu");
        _testSettings.InvoiceArchivePath = string.Empty;

        _mockGlm.Setup(g => g.ProcessBatchAsync(It.IsAny<string[]>()))
            .ReturnsAsync(new List<GlmInvoiceResponse> { new() });

        // Act
        await _service.ImportAsync(new[] { _testFile });

        // Assert
        Assert.Equal("zhipu", _testSettings.Glm.Provider);
        _mockFallback.Verify(f => f.Reset(), Times.Once);
    }
}
