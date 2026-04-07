using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace InvoiceAI.Core.Services;

public class FileService : IFileService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private const long MaxFileSize = 1024 * 1024; // 1MB
    private const int MaxImageDimension = 2000;

    public async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool IsSupportedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public IReadOnlyList<string> FilterSupportedFiles(IEnumerable<string> files)
    {
        return files.Where(IsSupportedFile).ToList();
    }

    public async Task<string> PrepareForOcrAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        // PDF compression not supported; already small enough; or not an image
        if (fileInfo.Length <= MaxFileSize || Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return filePath;

        using var image = await Image.LoadAsync(filePath);
        if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(MaxImageDimension, MaxImageDimension),
                Mode = ResizeMode.Max
            }));
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "InvoiceAI", "compressed");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, $"{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}.jpg");
        await image.SaveAsJpegAsync(tempPath, new JpegEncoder { Quality = 80 });
        return tempPath;
    }
}
