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
    private readonly IProviderFallbackManager _fallbackManager;

    // 每批处理的图片数量
    private const int BatchSize = 3;

    public event EventHandler<string>? StatusChanged;

    public InvoiceImportService(
        IBaiduOcrService ocrService,
        IGlmService glmService,
        IInvoiceService invoiceService,
        IFileService fileService,
        IAppSettingsService settingsService,
        IProviderFallbackManager fallbackManager)
    {
        _ocrService = ocrService;
        _glmService = glmService;
        _invoiceService = invoiceService;
        _fileService = fileService;
        _settingsService = settingsService;
        _fallbackManager = fallbackManager;

        // Subscribe to GLM service status changes and forward them
        _glmService.StatusChanged += (sender, message) => OnStatusChanged(message);
    }

    private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);

    public async Task<ImportResult> ImportAsync(IEnumerable<string> filePaths, Action<int, int>? onProgress = null)
    {
        var originalProvider = _settingsService.Settings.Glm.Provider;
        try
        {
            var supported = _fileService.FilterSupportedFiles(filePaths);
            var result = new ImportResult { TotalFiles = supported.Count };

            if (supported.Count == 0) return result;

            var allImageItems = new List<ImageItem>();
            var pdfFailedFiles = new List<string>();

            // Phase 0: Convert PDFs to images
            bool hasPdf = supported.Any(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
            if (hasPdf)
                OnStatusChanged("📄 正在转换 PDF 为图片...");

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
        finally
        {
            // Restore original provider after import completes
            _settingsService.Settings.Glm.Provider = originalProvider;
            _fallbackManager.Reset();
        }
    }

    private async Task ProcessBatchAsync(List<ImageItem> batchItems, ImportResult result)
    {
        if (batchItems.Count == 0) return;

        var batchPaths = batchItems.Select(i => i.FilePath).ToList();
        var preparedPaths = new List<string>();
        var ocrResults = new (string Path, string Text, string Hash)[batchItems.Count];
        var ocrSuccessIndices = new List<int>();

        // Phase 1: Compress
        OnStatusChanged("📦 正在压缩图片...");
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
        OnStatusChanged("🔍 正在进行 OCR 识别...");
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
            catch (Exception ocrEx)
            {
                LogHelper.Log($"[OCR] Failed for {Path.GetFileName(batchPaths[i])}: {ocrEx.Message}");
            }
        }

        if (ocrSuccessIndices.Count == 0) return;

        // Phase 3: GLM AI — with fallback logic
        // Status will be updated by GLM service (shows provider and model)
        var ocrTexts = ocrSuccessIndices.Select(i => ocrResults[i].Text).ToArray();
        var glmResults = new GlmInvoiceResponse[ocrTexts.Length];
        int processedCount = 0;

        var currentProvider = _settingsService.Settings.Glm.Provider;

        // 3a: Try batch processing first
        List<GlmInvoiceResponse>? batchResults = null;
        bool batchFailed = false;
        try
        {
            batchResults = await _glmService.ProcessBatchAsync(ocrTexts);

            if (batchResults == null || batchResults.Count == 0)
                throw new InvalidOperationException("GLM 返回结果为空");

            var allFailed = batchResults.All(r => r.MissingFields?.Count > 5);
            if (allFailed)
                throw new InvalidOperationException("GLM 返回结果全部标记为缺失字段");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Try fallback to next verified provider
            var nextProvider = _fallbackManager.TryGetNextProvider(currentProvider, ex.Message);
            if (nextProvider != null)
            {
                _settingsService.Settings.Glm.Provider = nextProvider;
                OnStatusChanged($"⚠️ 提供商切换中... {GetProviderDisplayName(nextProvider)}");

                try
                {
                    batchResults = await _glmService.ProcessBatchAsync(ocrTexts);
                    if (batchResults == null || batchResults.Count == 0)
                        throw new InvalidOperationException("切换提供商后 GLM 返回结果仍为空");
                }
                catch (Exception retryEx)
                {
                    batchFailed = true;
                    LogHelper.Log($"Batch failed with fallback: {retryEx.Message}");
                }
            }
            else
            {
                batchFailed = true;
                LogHelper.Log($"Batch failed, no fallback: {ex.Message}");
            }
        }

        // 3b: Use batch results for successfully processed invoices
        if (batchResults != null && batchResults.Count > 0)
        {
            int copyCount = Math.Min(batchResults.Count, ocrTexts.Length);
            for (int i = 0; i < copyCount; i++)
            {
                glmResults[i] = batchResults[i];
            }
            processedCount = copyCount;
        }

        // 3c: Process any remaining invoices individually (batch returned fewer than expected)
        if (processedCount < ocrTexts.Length)
        {
            OnStatusChanged($"🔄 批量返回 {processedCount}/{ocrTexts.Length} 张，正在逐张处理剩余发票...");
            for (int i = processedCount; i < ocrTexts.Length; i++)
            {
                try
                {
                    var singleResults = await _glmService.ProcessBatchAsync([ocrTexts[i]]);
                    if (singleResults != null && singleResults.Count > 0)
                    {
                        glmResults[i] = singleResults[0];
                        processedCount++;
                    }
                }
                catch
                {
                    // Individual processing failed, skip this invoice
                }
            }
        }

        if (processedCount == 0)
        {
            HandleFallbackFailure(batchItems, result, new Exception("批量处理失败且逐张处理也失败"));
            return;
        }

        // Phase 4: Save and Archive
        OnStatusChanged("💾 正在保存发票数据...");
        for (int j = 0; j < ocrSuccessIndices.Count; j++)
        {
            if (glmResults[j] == null) continue; // Skip invoices that failed both batch and individual processing

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
        OnStatusChanged($"✅ 已保存 {result.SuccessCount} 张发票");
    }

    private static Invoice MapToInvoice(GlmInvoiceResponse glm, string ocrText, string filePath, string hash)
    {
        return new Invoice
        {
            IssuerName = glm.IssuerName,
            RegistrationNumber = glm.RegistrationNumber,
            Description = glm.Description,
            TaxExcludedAmount = ParseDecimal(glm.TaxExcludedAmount),
            TaxIncludedAmount = ParseDecimal(glm.TaxIncludedAmount),
            TaxAmount = ParseDecimal(glm.TaxAmount),
            RecipientName = glm.RecipientName,
            Category = glm.SuggestedCategory,
            SourceFilePath = filePath,
            FileHash = hash,
            OcrRawText = ocrText,
            GlmRawResponse = JsonSerializer.Serialize(glm),
            MissingFields = JsonSerializer.Serialize(glm.MissingFields),
            InvoiceType = glm.InvoiceType switch { "Standard" => InvoiceType.Standard, "Simplified" => InvoiceType.Simplified, _ => InvoiceType.NonQualified },
            ItemsJson = JsonSerializer.Serialize(glm.Items),
            TransactionDate = ParseDate(glm.TransactionDate),
            IsConfirmed = false
        };
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Remove currency symbols, commas, percentage signs
        var cleaned = value.Replace(",", "").Replace("¥", "").Replace("円", "").Replace("%", "").Trim();
        return decimal.TryParse(cleaned, out var result) ? result : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Handle formats: YYYY-MM-DD, YYYY-MM, YYYY/MM/DD, YYYY年MM月DD日
        var cleaned = value.Replace("年", "-").Replace("月", "-").Replace("日", "").Replace("/", "-").Trim();
        return DateTime.TryParse(cleaned, out var result) ? result : null;
    }

    private void HandleFallbackFailure(List<ImageItem> batchItems, ImportResult result, Exception ex)
    {
        foreach (var item in batchItems)
        {
            result.Errors.Add($"AI 分析失败（所有可用提供商均已失败）: {item.FilePath}");
        }
        OnStatusChanged("所有已验证提供商均失败，请检查网络连接或提供商设置");
    }

    private static string GetProviderDisplayName(string providerId) => providerId switch
    {
        "zhipu" => "智谱 (Zhipu)",
        "nvidia" => "NVIDIA NIM",
        "cerebras" => "Cerebras",
        "google" => "Google",
        _ => providerId
    };

    private class ImageItem
    {
        public string FilePath { get; }
        public string SourceFileName { get; }
        public ImageItem(string filePath, string sourceFileName) { FilePath = filePath; SourceFileName = sourceFileName; }
    }
}
