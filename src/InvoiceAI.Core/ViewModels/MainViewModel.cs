using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;

namespace InvoiceAI.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IInvoiceService _invoiceService;
    private readonly IFileService _fileService;
    private readonly IBaiduOcrService _ocrService;
    private readonly IGlmService _glmService;
    private readonly IExcelExportService _excelExportService;
    private readonly IAppSettingsService _settingsService;

    public MainViewModel(
        IInvoiceService invoiceService,
        IFileService fileService,
        IBaiduOcrService ocrService,
        IGlmService glmService,
        IExcelExportService excelExportService,
        IAppSettingsService settingsService)
    {
        _invoiceService = invoiceService;
        _fileService = fileService;
        _ocrService = ocrService;
        _glmService = glmService;
        _excelExportService = excelExportService;
        _settingsService = settingsService;
    }

    // Properties
    [ObservableProperty] private ObservableCollection<Invoice> _invoices = [];
    [ObservableProperty] private ObservableCollection<Invoice> _recentImports = [];
    [ObservableProperty] private ObservableCollection<string> _categories = [];
    [ObservableProperty] private Dictionary<string, int> _categoryCounts = [];
    [ObservableProperty] private string _selectedCategory = "全部";
    [ObservableProperty] private Invoice? _selectedInvoice;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // Load data
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var invoices = await _invoiceService.GetAllAsync();
            Invoices.Clear();
            foreach (var inv in invoices) Invoices.Add(inv);

            CategoryCounts = await _invoiceService.GetCategoryCountsAsync();
            var cats = _settingsService.Settings.Categories.ToList();
            cats.Insert(0, "全部");
            Categories.Clear();
            foreach (var cat in cats) Categories.Add(cat);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Filter by category
    [RelayCommand]
    private async Task FilterByCategoryAsync(string category)
    {
        SelectedCategory = category;
        IsBusy = true;
        try
        {
            var invoices = category == "全部"
                ? await _invoiceService.GetAllAsync()
                : await _invoiceService.GetByCategoryAsync(category);
            Invoices.Clear();
            foreach (var inv in invoices) Invoices.Add(inv);
        }
        finally { IsBusy = false; }
    }

    // Search
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadDataAsync();
            return;
        }
        var all = await _invoiceService.GetAllAsync();
        var filtered = all.Where(i =>
            (i.IssuerName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.RegistrationNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();
        Invoices.Clear();
        foreach (var inv in filtered) Invoices.Add(inv);
    }

    // Export
    [RelayCommand]
    private async Task ExportAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        IsBusy = true;
        StatusMessage = "正在导出...";
        try
        {
            await _excelExportService.ExportAsync(Invoices.ToList(), filePath);
            StatusMessage = $"已导出到 {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    // Delete
    [RelayCommand]
    private async Task DeleteInvoiceAsync(Invoice? invoice)
    {
        if (invoice == null) return;
        await _invoiceService.DeleteAsync(invoice.Id);
        Invoices.Remove(invoice);
        if (SelectedInvoice == invoice) SelectedInvoice = null;
    }
}
