using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Models;
using Microsoft.Extensions.Logging;

namespace InvoiceAI.Core.Services;

public class InvoiceImportService : IInvoiceImportService
{
    private readonly IBaiduOcrService _ocrService;
    private readonly IGlmService _glmService;
    private readonly IInvoiceService _invoiceService;
    private readonly IFileService _fileService;
    private readonly IAppSettingsService _settingsService;

    // 每批处理的图片数量
    private const int BatchSize = 5;

    public InvoiceImportService(
        IBaiduOcrService ocrService,
        IGlmService glmService,
        IInvoiceService invoiceService,
        IFileService fileService,
        IAppSettingsService settingsService)
    {
        _ocrService = ocrService;
        _glmService = glmService;
        _invoiceService = invoiceService;
        _fileService = fileService;
        _settingsService = settingsService;
    }

    public async Task<ImportResult> ImportAsync(IEnumerable<string> filePaths, Action<int, int>? onProgress = null)
    {
        var supported = _fileService.FilterSupportedFiles(filePaths);
        var result = new ImportResult { TotalFiles = supported.Count };

        if (supported.Count == 0) return result;

        var allImageItems = new List<ImageItem>();
        var pdfFailedFiles = new List<string>();

        // Phase 0: Convert PDFs to images
        foreach (var filePath in supported)
        {
            var ext = Path.GetExtension(filePath);
            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var images = await _fileService.ConvertPdfToImagesAsync(filePath, _settingsService.Settings.InvoiceArchivePath);
                    foreach (var img in images)
                        allImageItems.Add(new ImageItem(img, Path.GetFileName(filePath)));
                }
                catch (Exception ex)
                {
                    pdfFailedFiles.Add(Path.GetFileName(filePath));
                    result.Errors.Add($"PDF 拆分失败: {Path.GetFileName(filePath)} - {ex.Message}");
                }
            }
            else
            {
                allImageItems.Add(new ImageItem(filePath, Path.GetFileName(filePath)));
            }
        }

        result.TotalImages = allImageItems.Count;
        if (result.TotalImages == 0) return result;

        // Process in batches
        int totalBatches = (result.TotalImages + BatchSize - 1) / BatchSize;
        for (int batch = 0; batch < totalBatches; batch++)
        {
            onProgress?.Invoke(batch + 1, totalBatches);

            var batchStart = batch * BatchSize;
            var batchEnd = Math.Min(batchStart + BatchSize, result.TotalImages);
            var batchItems = allImageItems.Skip(batchStart).Take(batchEnd - batchStart).ToList();

            await ProcessBatchAsync(batchItems, result);
        }

        return result;
    }

    private async Task ProcessBatchAsync(List<ImageItem> batchItems, ImportResult result)
    {
        if (batchItems.Count == 0) return;

        var batchPaths = batchItems.Select(i => i.FilePath).ToList();
        var preparedPaths = new List<string>();
        var ocrResults = new (string Path, string Text, string Hash)[batchItems.Count];
        var ocrSuccessIndices = new List<int>();

        // Phase 1: Compress
        for (int i = 0; i < batchItems.Count; i++)
        {
            try
            {
                preparedPaths.Add(await _fileService.PrepareForOcrAsync(batchPaths[i]));
            }
            catch
            {
                preparedPaths.Add(batchPaths[i]); // Fallback
            }
        }

        // Phase 2: OCR
        for (int i = 0; i < batchItems.Count; i++)
        {
            try
            {
                var hash = await _fileService.ComputeFileHashAsync(batchPaths[i]);
                if (await _invoiceService.ExistsByHashAsync(hash)) continue;

                var text = await _ocrService.RecognizeAsync(preparedPaths[i]);
                if (string.IsNullOrEmpty(text)) continue;

                ocrResults[i] = (batchPaths[i], text, hash);
                ocrSuccessIndices.Add(i);
            }
            catch
            {
                // OCR failed
            }
        }

        if (ocrSuccessIndices.Count == 0) return;

        // Phase 3: GLM AI
        var ocrTexts = ocrSuccessIndices.Select(i => ocrResults[i].Text).ToArray();
        List<GlmInvoiceResponse> glmResults;
        try
        {
            glmResults = await _glmService.ProcessBatchAsync(ocrTexts);
        }
        catch
        {
            return;
        }

        // Phase 4: Save and Archive
        // Map GLM results to OCR results (1-to-1 mapping for simplicity and correctness)
        for (int j = 0; j < ocrSuccessIndices.Count && j < glmResults.Count; j++)
        {
            var idx = ocrSuccessIndices[j];
            var glm = glmResults[j];
            var (_, ocrText, hash) = ocrResults[idx];
            var sourceFile = batchPaths[idx];

            var invoice = MapToInvoice(glm, ocrText, sourceFile, hash);
            invoice = await _invoiceService.SaveAsync(invoice);
            result.Invoices.Add(invoice);
            result.SuccessCount++;

            // Archive
            if (!string.IsNullOrWhiteSpace(_settingsService.Settings.InvoiceArchivePath))
            {
                try
                {
                    await _fileService.CopyToInvoiceArchiveAsync(
                        sourceFile,
                        _settingsService.Settings.InvoiceArchivePath,
                        invoice.Category,
                        invoice.IssuerName,
                        invoice.TransactionDate);
                }
                catch { }
            }
        }
    }

    private static Invoice MapToInvoice(GlmInvoiceResponse glm, string ocrText, string filePath, string hash)
    {
        return new Invoice
        {
            IssuerName = glm.IssuerName,
            RegistrationNumber = glm.RegistrationNumber,
            Description = glm.Description,
            TaxExcludedAmount = glm.TaxExcludedAmount,
            TaxIncludedAmount = glm.TaxIncludedAmount,
            TaxAmount = glm.TaxAmount,
            RecipientName = glm.RecipientName,
            Category = glm.SuggestedCategory,
            SourceFilePath = filePath,
            FileHash = hash,
            OcrRawText = ocrText,
            GlmRawResponse = JsonSerializer.Serialize(glm),
            MissingFields = JsonSerializer.Serialize(glm.MissingFields),
            InvoiceType = glm.InvoiceType switch { "Standard" => InvoiceType.Standard, "Simplified" => InvoiceType.Simplified, _ => InvoiceType.NonQualified },
            ItemsJson = JsonSerializer.Serialize(glm.Items),
            IsConfirmed = false
        };
    }

    private class ImageItem
    {
        public string FilePath { get; }
        public string SourceFileName { get; }
        public ImageItem(string filePath, string sourceFileName) { FilePath = filePath; SourceFileName = sourceFileName; }
    }
}
