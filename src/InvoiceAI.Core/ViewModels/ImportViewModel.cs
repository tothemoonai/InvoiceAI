using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;

namespace InvoiceAI.Core.ViewModels;

public partial class ImportViewModel : ObservableObject
{
    private static string LogDir => Helpers.LogHelper.LogDir;

    private readonly IInvoiceImportService _importService;
    private readonly IFileService _fileService;

    public ImportViewModel(
        IInvoiceImportService importService,
        IFileService fileService)
    {
        _importService = importService;
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
        if (supported.Count == 0)
        {
            StatusMessage = "错误：没有支持的文件格式（仅支持 JPG、PNG、PDF）";
            return;
        }

        IsProcessing = true;
        Results.Clear();
        ImportItems.Clear();
        foreach (var f in supported)
            ImportItems.Add(new ImportItem { FileName = Path.GetFileName(f), FilePath = f, Status = "等待中" });

        try
        {
            // Call the shared service
            var result = await _importService.ImportAsync(supported, (batch, total) =>
            {
                StatusMessage = $"处理批次 {batch}/{total}...";
            });

            // Update UI with results
            Results.Clear();
            foreach (var inv in result.Invoices)
                Results.Add(inv);

            if (result.SuccessCount > 0)
            {
                StatusMessage = $"处理完成: {result.SuccessCount}/{result.TotalImages} 成功";
            }
            else
            {
                StatusMessage = "处理完成，但未保存任何发票";
            }

            if (result.Errors.Count > 0)
            {
                StatusMessage += $" | 错误: {result.Errors.Count}";
            }
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void Cancel() => IsProcessing = false;
}

public class ImportItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
