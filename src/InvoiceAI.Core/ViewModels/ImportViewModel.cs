using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;
using System.Text.Json;

namespace InvoiceAI.Core.ViewModels;

public partial class ImportViewModel : ObservableObject
{
    private const int MaxFiles = 5;
    private static string LogDir => Helpers.LogHelper.LogDir;

    private readonly IBaiduOcrService _ocrService;
    private readonly IGlmService _glmService;
    private readonly IInvoiceService _invoiceService;
    private readonly IFileService _fileService;
    private readonly IAppSettingsService _settingsService;

    public ImportViewModel(
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

    [ObservableProperty] private ObservableCollection<ImportItem> _importItems = [];
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<Invoice> _results = [];

    [RelayCommand]
    private async Task ProcessFilesAsync(string[] filePaths)
    {
        var supported = _fileService.FilterSupportedFiles(filePaths);
        if (supported.Count == 0) return;

        // Limit to MaxFiles
        if (supported.Count > MaxFiles)
            supported = supported.Take(MaxFiles).ToList();

        IsProcessing = true;
        Results.Clear();
        ImportItems.Clear();
        foreach (var f in supported)
            ImportItems.Add(new ImportItem { FileName = Path.GetFileName(f), FilePath = f, Status = "等待中" });

        var totalSw = Stopwatch.StartNew();
        var n = supported.Count;

        try
        {
            // ── Phase 0: Convert PDF to images ────────────────────
            var pdfArchivePath = _settingsService.Settings.InvoiceArchivePath;
            var pdfConvertedPaths = new List<string>(); // 存储转换后的图片路径
            var pdfConversionMap = new Dictionary<int, List<int>>(); // 原始文件索引 → 转换后的图片索引列表

            for (int i = 0; i < n; i++)
            {
                if (!IsProcessing) return;
                StatusMessage = $"检查文件类型 ({i + 1}/{n})...";

                var ext = Path.GetExtension(supported[i]);
                if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    ImportItems[i].Status = "📄 PDF转换中...";
                    try
                    {
                        var images = await _fileService.ConvertPdfToImagesAsync(supported[i], pdfArchivePath);
                        var startIdx = pdfConvertedPaths.Count;
                        foreach (var img in images)
                        {
                            pdfConvertedPaths.Add(img);
                        }
                        pdfConversionMap[i] = Enumerable.Range(startIdx, images.Count).ToList();
                        ImportItems[i].Status = $"PDF转换完成 ✓ ({images.Count} 页)";
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PDF] 转换失败: {ex.Message}");
                        // 转换失败时使用原始 PDF 路径
                        pdfConvertedPaths.Add(supported[i]);
                        pdfConversionMap[i] = new List<int> { pdfConvertedPaths.Count - 1 };
                        ImportItems[i].Status = "PDF转换失败，使用原文件";
                    }
                }
                else
                {
                    pdfConvertedPaths.Add(supported[i]);
                    pdfConversionMap[i] = new List<int> { pdfConvertedPaths.Count - 1 };
                }
            }
            
            // 更新文件列表为转换后的图片路径
            var effectiveFiles = pdfConvertedPaths.ToList();
            var effectiveCount = effectiveFiles.Count;

            // ── Phase 1: Compress images ──────────────────────────
            var preparedPaths = new List<string>();
            for (int i = 0; i < effectiveCount; i++)
            {
                if (!IsProcessing) return;
                UpdateProgress(i, effectiveCount * 2 + 1);
                StatusMessage = $"压缩图片 ({i + 1}/{effectiveCount})...";

                try
                {
                    var prepared = await _fileService.PrepareForOcrAsync(effectiveFiles[i]);
                    preparedPaths.Add(prepared);
                }
                catch (Exception ex)
                {
                    preparedPaths.Add(effectiveFiles[i]); // fallback to original
                    LogError(effectiveFiles[i], ex);
                }
            }

            // ── Phase 2: OCR each image ───────────────────────────
            var ocrResults = new (string Path, string Text, string Hash)[effectiveCount];
            var ocrSuccessIndices = new List<int>();
            for (int i = 0; i < effectiveCount; i++)
            {
                if (!IsProcessing) return;
                UpdateProgress(effectiveCount + i, effectiveCount * 2 + 1);
                StatusMessage = $"OCR识别 ({i + 1}/{effectiveCount}): {Path.GetFileName(effectiveFiles[i])}";

                try
                {
                    var hash = await _fileService.ComputeFileHashAsync(effectiveFiles[i]);
                    if (await _invoiceService.ExistsByHashAsync(hash))
                    {
                        ocrResults[i] = (effectiveFiles[i], "", hash);
                        continue;
                    }

                    var ocrSw = Stopwatch.StartNew();
                    var ocrText = await _ocrService.RecognizeAsync(preparedPaths[i]);
                    ocrSw.Stop();

                    ocrResults[i] = (effectiveFiles[i], ocrText, hash);
                    ocrSuccessIndices.Add(i);
                    ImportItems[i].Status = $"OCR完成 ({ocrSw.ElapsedMilliseconds}ms) ✓";
                    LogTiming($"OCR {ImportItems[i].FileName}", ocrSw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    ImportItems[i].Status = $"❌ OCR失败: {ex.Message}";
                    ocrResults[i] = (supported[i], "", "");
                    LogError(supported[i], ex);
                }
            }

            if (ocrSuccessIndices.Count == 0)
            {
                var skippedCount = ImportItems.Count(it => it.Status.Contains("已存在"));
                StatusMessage = skippedCount > 0
                    ? $"所有 {skippedCount} 张图片已存在，无新发票"
                    : "没有成功的OCR结果";
                IsProcessing = false;
                return;
            }

            // ── Phase 3: Batch GLM analysis ───────────────────────
            UpdateProgress(n * 2, n * 2 + 1);
            foreach (var idx in ocrSuccessIndices)
                ImportItems[idx].Status = "🤖 AI分析中...";

            var glmSw = Stopwatch.StartNew();
            var ocrTexts = ocrSuccessIndices.Select(i => ocrResults[i].Text).ToArray();
            List<GlmInvoiceResponse> glmResults;

            // Show elapsed time while waiting
            var progressCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!progressCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(3000, progressCts.Token);
                    if (progressCts.Token.IsCancellationRequested) break;
                    var sec = (int)glmSw.Elapsed.TotalSeconds;
                    StatusMessage = $"🤖 AI分析中... 已等待 {sec}s（批量 {ocrSuccessIndices.Count} 张）";
                }
            }, progressCts.Token);

            try
            {
                glmResults = await _glmService.ProcessBatchAsync(ocrTexts);
            }
            catch (Exception ex)
            {
                progressCts.Cancel();
                LogError("GLM batch", ex);
                glmResults = [];
                foreach (var idx in ocrSuccessIndices)
                    ImportItems[idx].Status = $"❌ AI分析失败: {ex.Message}";
                StatusMessage = $"AI分析失败: {Results.Count}/{n} 成功";
                IsProcessing = false;
                return;
            }
            finally
            {
                progressCts.Cancel();
            }
            glmSw.Stop();

            // Token stats
            var totalTokens = glmResults.Sum(r => r.TotalTokens);
            var promptTokens = glmResults.Sum(r => r.PromptTokens);
            var completionTokens = glmResults.Sum(r => r.CompletionTokens);
            LogTiming($"GLM batch ({ocrSuccessIndices.Count} invoices)", glmSw.ElapsedMilliseconds,
                promptTokens, completionTokens, totalTokens);

            // ── Map and save results ──────────────────────────────
            int glmIdx = 0;
            for (int j = 0; j < ocrSuccessIndices.Count; j++)
            {
                var idx = ocrSuccessIndices[j];
                
                // Process all GLM results for this file (single file may contain multiple invoices)
                int invoicesForThisFile = 0;
                while (glmIdx < glmResults.Count)
                {
                    // For multi-invoice files, check if this GLM result belongs to current file
                    // Heuristic: if we have more GLM results than files, distribute evenly
                    int expectedPerFile = Math.Max(1, glmResults.Count / ocrSuccessIndices.Count);
                    if (invoicesForThisFile >= expectedPerFile && glmResults.Count - glmIdx > ocrSuccessIndices.Count - j - 1)
                        break;

                    var glm = glmResults[glmIdx];
                    var (_, ocrText, hash) = ocrResults[idx];

                    var invoice = MapToInvoice(glm, ocrText, supported[idx], hash);
                    invoice = await _invoiceService.SaveAsync(invoice);
                    Results.Add(invoice);

                    // Archive the invoice file if path is configured
                    var archivePath = _settingsService.Settings.InvoiceArchivePath;
                    if (!string.IsNullOrWhiteSpace(archivePath))
                    {
                        var sourceFile = supported[idx];
                        await _fileService.CopyToInvoiceArchiveAsync(
                            sourceFile,
                            archivePath,
                            invoice.Category,
                            invoice.IssuerName,
                            invoice.TransactionDate);
                    }

                    var tokenInfo = glm.TotalTokens > 0 ? $" (tokens: {glm.TotalTokens})" : "";
                    var invoiceNum = invoicesForThisFile > 0 ? $" #{invoicesForThisFile + 1}" : "";
                    ImportItems[idx].Status = $"✅ 完成{invoiceNum}{tokenInfo}";

                    glmIdx++;
                    invoicesForThisFile++;
                }
            }

            // Handle any remaining GLM results (edge case)
            while (glmIdx < glmResults.Count)
            {
                var glm = glmResults[glmIdx];
                var lastIdx = ocrSuccessIndices[^1];
                var (_, ocrText, hash) = ocrResults[lastIdx];

                var invoice = MapToInvoice(glm, ocrText, supported[lastIdx], hash);
                invoice = await _invoiceService.SaveAsync(invoice);
                Results.Add(invoice);
                glmIdx++;
            }

            UpdateProgress(n * 2 + 1, n * 2 + 1);
            totalSw.Stop();
            var tokenSummary = totalTokens > 0 ? $", tokens: {totalTokens}" : "";
            StatusMessage = $"处理完成: {Results.Count}/{n} 成功 (总耗时 {totalSw.ElapsedMilliseconds / 1000.0:F1}s{tokenSummary})";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void UpdateProgress(int completed, int total)
        => Progress = (double)completed / total * 100;

    [RelayCommand]
    private void Cancel() => IsProcessing = false;

    private static void LogError(string filePath, Exception ex)
    {
        Directory.CreateDirectory(LogDir);
        var innerMsg = ex.InnerException != null ? $"\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "";
        System.IO.File.AppendAllText(
            Path.Combine(LogDir, "import_error.log"),
            $"[{DateTime.Now:HH:mm:ss}] {filePath}: {ex.GetType().FullName}: {ex.Message}{innerMsg}\n{ex.StackTrace}\n\n");
    }

    private static void LogTiming(string label, long ms, int promptTokens = 0, int completionTokens = 0, int totalTokens = 0)
    {
        Directory.CreateDirectory(LogDir);
        var tokenInfo = totalTokens > 0
            ? $" | tokens: {totalTokens} (prompt: {promptTokens}, completion: {completionTokens})"
            : "";
        System.IO.File.AppendAllText(
            Path.Combine(LogDir, "timing.log"),
            $"[{DateTime.Now:HH:mm:ss}] {label}: {ms}ms{tokenInfo}\n");
    }

    private static Invoice MapToInvoice(GlmInvoiceResponse glm, string ocrText, string filePath, string hash)
    {
        var inv = new Invoice
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
            InvoiceType = glm.InvoiceType switch
            {
                "Standard" => InvoiceType.Standard,
                "Simplified" => InvoiceType.Simplified,
                _ => InvoiceType.NonQualified
            },
            ItemsJson = JsonSerializer.Serialize(glm.Items),
            IsConfirmed = false
        };

        if (DateTime.TryParse(glm.TransactionDate, out var date))
            inv.TransactionDate = date;

        return inv;
    }
}

public class ImportItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
