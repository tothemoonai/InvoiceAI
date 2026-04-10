namespace InvoiceAI.Core.Services;

public interface IFileService
{
    Task<string> ComputeFileHashAsync(string filePath);
    bool IsSupportedFile(string filePath);
    IReadOnlyList<string> FilterSupportedFiles(IEnumerable<string> files);
    Task<string> PrepareForOcrAsync(string filePath);
    Task<string?> CopyToInvoiceArchiveAsync(string sourceFilePath, string archiveBasePath, string category, string issuerName, DateTime? transactionDate);
    
    /// <summary>
    /// 将 PDF 文件转换为图片列表。如果不是 PDF，返回原始路径。
    /// 图片保存到 archiveBasePath/PDF/{pdfFileName}/ 目录下。
    /// </summary>
    Task<IReadOnlyList<string>> ConvertPdfToImagesAsync(string pdfFilePath, string archiveBasePath);
}
