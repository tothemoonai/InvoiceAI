using System.Security.Cryptography;

namespace InvoiceAI.Core.Services;

public class FileService : IFileService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

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
}
