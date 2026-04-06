using InvoiceAI.Core.Services;

namespace InvoiceAI.Core.Tests;

public class FileServiceTests
{
    private readonly FileService _fileService = new();

    [Fact]
    public void IsSupportedFile_Jpg_ReturnsTrue()
        => Assert.True(_fileService.IsSupportedFile("test.jpg"));

    [Fact]
    public void IsSupportedFile_Jpeg_ReturnsTrue()
        => Assert.True(_fileService.IsSupportedFile("photo.JPEG"));

    [Fact]
    public void IsSupportedFile_Png_ReturnsTrue()
        => Assert.True(_fileService.IsSupportedFile("scan.png"));

    [Fact]
    public void IsSupportedFile_Pdf_ReturnsTrue()
        => Assert.True(_fileService.IsSupportedFile("invoice.pdf"));

    [Fact]
    public void IsSupportedFile_Txt_ReturnsFalse()
        => Assert.False(_fileService.IsSupportedFile("readme.txt"));

    [Fact]
    public void IsSupportedFile_CaseInsensitive()
        => Assert.True(_fileService.IsSupportedFile("PHOTO.PNG"));

    [Fact]
    public void FilterSupportedFiles_MixedFiles_FiltersCorrectly()
    {
        var files = new[] { "a.jpg", "b.txt", "c.pdf", "d.xlsx", "e.png" };
        var result = _fileService.FilterSupportedFiles(files);
        Assert.Equal(["a.jpg", "c.pdf", "e.png"], result);
    }

    [Fact]
    public async Task ComputeFileHashAsync_ReturnsConsistentHash()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "test content");
        try
        {
            var hash1 = await _fileService.ComputeFileHashAsync(tempFile);
            var hash2 = await _fileService.ComputeFileHashAsync(tempFile);
            Assert.Equal(hash1, hash2);
            Assert.Equal(64, hash1.Length); // SHA256 hex = 64 chars
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ComputeFileHashAsync_DifferentContent_ReturnsDifferentHash()
    {
        var file1 = Path.GetTempFileName();
        var file2 = Path.GetTempFileName();
        await File.WriteAllTextAsync(file1, "content A");
        await File.WriteAllTextAsync(file2, "content B");
        try
        {
            var hash1 = await _fileService.ComputeFileHashAsync(file1);
            var hash2 = await _fileService.ComputeFileHashAsync(file2);
            Assert.NotEqual(hash1, hash2);
        }
        finally
        {
            File.Delete(file1);
            File.Delete(file2);
        }
    }
}
