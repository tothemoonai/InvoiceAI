using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;
using System.Text.Json;

namespace InvoiceAI.Core.ViewModels;

public partial class ImportViewModel : ObservableObject
{
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

        IsProcessing = true;
        Results.Clear();
        ImportItems = new ObservableCollection<ImportItem>(
            supported.Select(f => new ImportItem { FileName = Path.GetFileName(f), FilePath = f, Status = "等待中" }));

        for (int i = 0; i < supported.Count; i++)
        {
            if (!IsProcessing) break;
            var filePath = supported[i];
            var item = ImportItems[i];
            try
            {
                Progress = (double)(i + 1) / supported.Count * 100;
                StatusMessage = $"处理中 ({i + 1}/{supported.Count}): {Path.GetFileName(filePath)}";

                // Check duplicate
                var hash = await _fileService.ComputeFileHashAsync(filePath);
                if (await _invoiceService.ExistsByHashAsync(hash))
                {
                    item.Status = "⚠ 已存在（跳过）";
                    continue;
                }

                // OCR
                item.Status = "🔍 OCR识别中...";
                var ocrText = await _ocrService.RecognizeAsync(filePath);
                item.Status = "🤖 AI分析中...";

                // GLM
                var glmResult = await _glmService.ProcessInvoiceAsync(ocrText);

                // Map to Invoice
                var invoice = MapToInvoice(glmResult, ocrText, filePath, hash);
                invoice = await _invoiceService.SaveAsync(invoice);

                Results.Add(invoice);
                item.Status = "✅ 完成";
            }
            catch (Exception ex)
            {
                item.Status = $"❌ 失败: {ex.Message}";
            }
        }

        StatusMessage = $"处理完成: {Results.Count}/{supported.Count} 成功";
        IsProcessing = false;
    }

    [RelayCommand]
    private void Cancel() => IsProcessing = false;

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
