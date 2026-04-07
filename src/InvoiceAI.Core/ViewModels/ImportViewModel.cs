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

    private readonly IBaiduOcrService _ocrService;
    private readonly IGlmService _glmService;
    private readonly IInvoiceService _invoiceService;
    private readonly IFileService _fileService;

    public ImportViewModel(
        IBaiduOcrService ocrService,
        IGlmService glmService,
        IInvoiceService invoiceService,
        IFileService fileService)
    {
        _ocrService = ocrService;
        _glmService = glmService;
        _invoiceService = invoiceService;
        _fileService = fileService;
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
            // ── Phase 1: Compress images ──────────────────────────
            var preparedPaths = new List<string>();
            for (int i = 0; i < n; i++)
            {
                if (!IsProcessing) return;
                UpdateProgress(i, n * 2 + 1);
                StatusMessage = $"压缩图片 ({i + 1}/{n})...";
                ImportItems[i].Status = "压缩中...";

                try
                {
                    var prepared = await _fileService.PrepareForOcrAsync(supported[i]);
                    preparedPaths.Add(prepared);
                    ImportItems[i].Status = "压缩完成 ✓";
                }
                catch (Exception ex)
                {
                    preparedPaths.Add(supported[i]); // fallback to original
                    ImportItems[i].Status = $"压缩失败，使用原图";
                    LogError(supported[i], ex);
                }
            }

            // ── Phase 2: OCR each image ───────────────────────────
            var ocrResults = new (string Path, string Text, string Hash)[n];
            var ocrSuccessIndices = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!IsProcessing) return;
                UpdateProgress(n + i, n * 2 + 1);
                StatusMessage = $"OCR识别 ({i + 1}/{n}): {ImportItems[i].FileName}";
                ImportItems[i].Status = "🔍 OCR识别中...";

                try
                {
                    var hash = await _fileService.ComputeFileHashAsync(supported[i]);
                    if (await _invoiceService.ExistsByHashAsync(hash))
                    {
                        ImportItems[i].Status = "⚠ 已存在（跳过）";
                        ocrResults[i] = (supported[i], "", hash);
                        continue;
                    }

                    var ocrSw = Stopwatch.StartNew();
                    var ocrText = await _ocrService.RecognizeAsync(preparedPaths[i]);
                    ocrSw.Stop();

                    ocrResults[i] = (supported[i], ocrText, hash);
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
            LogTiming($"GLM batch ({ocrSuccessIndices.Count} invoices)", glmSw.ElapsedMilliseconds);

            // ── Map and save results ──────────────────────────────
            for (int j = 0; j < ocrSuccessIndices.Count; j++)
            {
                if (j >= glmResults.Count) break;
                var idx = ocrSuccessIndices[j];
                var glm = glmResults[j];
                var (_, ocrText, hash) = ocrResults[idx];

                var invoice = MapToInvoice(glm, ocrText, supported[idx], hash);
                invoice = await _invoiceService.SaveAsync(invoice);
                Results.Add(invoice);
                ImportItems[idx].Status = "✅ 完成";
            }

            UpdateProgress(n * 2 + 1, n * 2 + 1);
            totalSw.Stop();
            StatusMessage = $"处理完成: {Results.Count}/{n} 成功 (总耗时 {totalSw.ElapsedMilliseconds / 1000.0:F1}s)";
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
        var logDir = Path.Combine(Path.GetTempPath(), "InvoiceAI");
        Directory.CreateDirectory(logDir);
        var innerMsg = ex.InnerException != null ? $"\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "";
        System.IO.File.AppendAllText(
            Path.Combine(logDir, "import_error.log"),
            $"[{DateTime.Now:HH:mm:ss}] {filePath}: {ex.GetType().FullName}: {ex.Message}{innerMsg}\n{ex.StackTrace}\n\n");
    }

    private static void LogTiming(string label, long ms)
    {
        var logDir = Path.Combine(Path.GetTempPath(), "InvoiceAI");
        Directory.CreateDirectory(logDir);
        System.IO.File.AppendAllText(
            Path.Combine(logDir, "timing.log"),
            $"[{DateTime.Now:HH:mm:ss}] {label}: {ms}ms\n");
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
