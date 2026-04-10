using System.Security.Cryptography;
using PDFtoImage;
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

    /// <summary>
    /// 将 PDF 文件转换为图片（每页一张），保存到用户设置的归档目录下的 PDF 子目录。
    /// 目录结构: {archiveBasePath}/PDF/{pdfFileNameWithoutExt}/page_1.jpg, page_2.jpg, ...
    /// 返回转换后的图片路径列表。
    /// 如果输入文件不是 PDF 或转换失败，返回原始文件路径。
    /// </summary>
    public async Task<IReadOnlyList<string>> ConvertPdfToImagesAsync(string pdfFilePath, string archiveBasePath)
    {
        var ext = Path.GetExtension(pdfFilePath);
        if (!ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return new[] { pdfFilePath };

        var images = new List<string>();
        try
        {
            // 输出目录: {archiveBasePath}/PDF/{pdfFileName}/
            var pdfFileName = Path.GetFileNameWithoutExtension(pdfFilePath);
            var pdfDir = Path.Combine(archiveBasePath, "PDF", pdfFileName);
            Directory.CreateDirectory(pdfDir);

            // 读取 PDF 为字节数组
            var pdfBytes = await File.ReadAllBytesAsync(pdfFilePath);

            // 获取页数
            var pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);

            for (int i = 0; i < pageCount; i++)
            {
                var pageFileName = pageCount == 1
                    ? $"{pdfFileName}.jpg"
                    : $"page_{i + 1}.jpg";
                var outputPath = Path.Combine(pdfDir, pageFileName);

                // 使用 Byte[] 重载将 PDF 页面保存为图片
                PDFtoImage.Conversion.SaveJpeg(outputPath, pdfBytes, page: i);
                
                images.Add(outputPath);
            }

            // 压缩图片到合适尺寸 (OCR 服务通常处理 2000px 以内的图片)
            for (int i = 0; i < images.Count; i++)
            {
                try
                {
                    using var image = await Image.LoadAsync(images[i]);
                    if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(MaxImageDimension, MaxImageDimension),
                            Mode = ResizeMode.Max
                        }));
                        await image.SaveAsJpegAsync(images[i], new JpegEncoder { Quality = 85 });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PDF] 压缩图片失败 {images[i]}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvoiceAI");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, "pdf_convert_error.log");
            File.AppendAllText(logFile, $"{DateTime.Now:O}\nInput: {pdfFilePath}\nArchive: {archiveBasePath}\nError: {ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}\n\n");
            
            System.Diagnostics.Debug.WriteLine($"[PDF] 转换失败: {ex.Message}");
            return new[] { pdfFilePath };
        }

        return images.Count > 0 ? images : new[] { pdfFilePath };
    }

    public async Task<string?> CopyToInvoiceArchiveAsync(string sourceFilePath, string archiveBasePath, string category, string issuerName, DateTime? transactionDate)
    {
        if (string.IsNullOrWhiteSpace(archiveBasePath))
            return null;

        try
        {
            // Sanitize category and issuer name for folder/file names
            var safeCategory = SanitizeFileName(string.IsNullOrWhiteSpace(category) ? "未分类" : category);
            var safeIssuer = SanitizeFileName(string.IsNullOrWhiteSpace(issuerName) ? "未知 issuer" : issuerName);
            var dateStr = transactionDate?.ToString("yyyyMMdd") ?? "nodate";
            var timestamp = DateTime.Now.ToString("HHmmss");

            // Create category subfolder
            var categoryDir = Path.Combine(archiveBasePath, safeCategory);
            Directory.CreateDirectory(categoryDir);

            // Build filename: YYYYMMDD_HHMMSS_issuerName.ext
            var ext = Path.GetExtension(sourceFilePath);
            var newFileName = $"{dateStr}_{timestamp}_{safeIssuer}{ext}";
            var destPath = Path.Combine(categoryDir, newFileName);

            // If file already exists, add a suffix
            if (File.Exists(destPath))
            {
                var baseName = Path.GetFileNameWithoutExtension(newFileName);
                destPath = Path.Combine(categoryDir, $"{baseName}_{Guid.NewGuid():N}{ext}");
            }

            // Check if compression is needed (images > 1MB)
            var sourceInfo = new FileInfo(sourceFilePath);
            if (sourceInfo.Length > MaxFileSize && !ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // Compress before copying
                using var image = await Image.LoadAsync(sourceFilePath);
                if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxImageDimension, MaxImageDimension),
                        Mode = ResizeMode.Max
                    }));
                }

                // If original is not JPEG, convert to JPEG for compression
                if (!ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    var jpgPath = Path.ChangeExtension(destPath, ".jpg");
                    await image.SaveAsJpegAsync(jpgPath, new JpegEncoder { Quality = 80 });
                    return jpgPath;
                }
                else
                {
                    await image.SaveAsJpegAsync(destPath, new JpegEncoder { Quality = 80 });
                    return destPath;
                }
            }
            else
            {
                // Just copy
                File.Copy(sourceFilePath, destPath, overwrite: false);
                return destPath;
            }
        }
        catch
        {
            // Silently fail - archiving is optional
            return null;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalid.Contains(c)));
    }
}
