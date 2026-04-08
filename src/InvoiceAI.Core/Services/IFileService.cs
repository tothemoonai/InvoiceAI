namespace InvoiceAI.Core.Services;

public interface IFileService
{
    Task<string> ComputeFileHashAsync(string filePath);
    bool IsSupportedFile(string filePath);
    IReadOnlyList<string> FilterSupportedFiles(IEnumerable<string> files);
    Task<string> PrepareForOcrAsync(string filePath);
    Task<string?> CopyToInvoiceArchiveAsync(string sourceFilePath, string archiveBasePath, string category, string issuerName, DateTime? transactionDate);
}
