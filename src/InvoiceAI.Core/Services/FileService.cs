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
    /// 将 PDF 文件转换为图片（每页一张），保存到 TEMP/testdata/images/ 目录。
    /// 返回转换后的图片路径列表（单页 PDF 返回单个路径，多页 PDF 返回多个路径）。
    /// 如果输入文件不是 PDF 或转换失败，返回原始文件路径。
    /// </summary>
    public async Task<IReadOnlyList<string>> ConvertPdfToImagesAsync(string pdfFilePath)
    {
        var ext = Path.GetExtension(pdfFilePath);
        if (!ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return new[] { pdfFilePath };

        var images = new List<string>();
        try
        {
            // 输出目录: TEMP/testdata/images/ (项目根目录下)
            var projectRoot = FindProjectRoot();
            var outputDir = Path.Combine(projectRoot, "TEMP", "testdata", "images");
            Directory.CreateDirectory(outputDir);

            var baseName = Path.GetFileNameWithoutExtension(pdfFilePath);
            
            // 使用同步方法获取所有页面
            var bitmaps = PDFtoImage.Conversion.ToImages(pdfFilePath);
            var bitmapsList = bitmaps.ToList();
            var pageCount = bitmapsList.Count;

            for (int i = 0; i < pageCount; i++)
            {
                var pageFileName = pageCount == 1
                    ? $"{baseName}.jpg"
                    : $"{baseName}_p{i + 1}.jpg";
                var outputPath = Path.Combine(outputDir, pageFileName);

                var bitmap = bitmapsList[i];
                using var image = SkiaSharpToImageSharp(bitmap);
                
                // 压缩到合适尺寸
                if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxImageDimension, MaxImageDimension),
                        Mode = ResizeMode.Max
                    }));
                }

                await image.SaveAsJpegAsync(outputPath, new JpegEncoder { Quality = 90 });
                images.Add(outputPath);
                bitmap.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PDF] 转换失败: {ex.Message}");
            // 转换失败时返回原始文件路径
            return new[] { pdfFilePath };
        }

        return images.Count > 0 ? images : new[] { pdfFilePath };
    }

    /// <summary>
    /// 将 SkiaSharp.SKBitmap 转换为 SixLabors.ImageSharp.Image
    /// </summary>
    private static SixLabors.ImageSharp.Image SkiaSharpToImageSharp(SkiaSharp.SKBitmap bitmap)
    {
        var info = bitmap.Info;
        var pixels = bitmap.GetPixelSpan();
        return SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(
            pixels, bitmap.Width, bitmap.Height);
    }

    /// <summary>
    /// 查找项目根目录（包含 TEMP 子目录的目录）
    /// </summary>
    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 15; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "TEMP")))
                return dir;
            var parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent) || parent == dir) break;
            dir = parent;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
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
