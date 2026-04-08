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
    [ObservableProperty] private ObservableCollection<Invoice> _selectedInvoices = [];
    [ObservableProperty] private ObservableCollection<string> _categories = [];
    [ObservableProperty] private Dictionary<string, int> _categoryCounts = [];
    [ObservableProperty] private string _selectedCategory = "全部";
    [ObservableProperty] private Invoice? _selectedInvoice;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showConfirmedOnly;

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
        await RefreshInvoiceListAsync();
    }

    // Toggle confirmed filter
    partial void OnShowConfirmedOnlyChanged(bool value)
    {
        _ = RefreshInvoiceListAsync();
    }

    private async Task RefreshInvoiceListAsync()
    {
        IsBusy = true;
        try
        {
            var invoices = SelectedCategory == "全部"
                ? (ShowConfirmedOnly
                    ? await _invoiceService.GetConfirmedAsync()
                    : await _invoiceService.GetAllAsync())
                : await _invoiceService.GetByCategoryAsync(SelectedCategory);

            if (ShowConfirmedOnly && SelectedCategory != "全部")
            {
                // Additional filtering: only confirmed items within category
                invoices = invoices.Where(i => i.IsConfirmed).ToList();
            }

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
    private async Task ExportAsync(object? parameters)
    {
        string? filePath = null;
        DateTime? startDate = null;
        DateTime? endDate = null;
        bool skipConfirmed = false;

        if (parameters is string path)
        {
            filePath = path;
        }
        else if (parameters is ValueTuple<string, DateTime?, DateTime?> tuple3)
        {
            (filePath, startDate, endDate) = tuple3;
        }
        else if (parameters is ValueTuple<string, DateTime?, DateTime?, bool> tuple4)
        {
            (filePath, startDate, endDate, skipConfirmed) = tuple4;
        }

        if (string.IsNullOrEmpty(filePath)) return;

        IsBusy = true;
        StatusMessage = "正在导出...";
        try
        {
            var invoices = Invoices.ToList();
            int skippedCount = 0;
            if (skipConfirmed)
            {
                var beforeFilter = invoices.Count;
                invoices = invoices.Where(i => !i.IsConfirmed).ToList();
                skippedCount = beforeFilter - invoices.Count;
            }

            await _excelExportService.ExportAsync(invoices, filePath, startDate, endDate);
            StatusMessage = $"已导出到 {filePath}";
            if (skipConfirmed && skippedCount > 0)
                StatusMessage = $"已导出到 {filePath}（跳过 {skippedCount} 张已确认发票）";

            // Auto-save after export if enabled
            if (_settingsService.Settings.AutoSaveAfterExport && invoices.Count > 0)
            {
                var filteredInvoices = invoices;
                if (startDate.HasValue || endDate.HasValue)
                {
                    filteredInvoices = filteredInvoices.Where(i =>
                    {
                        if (!i.TransactionDate.HasValue) return false;
                        if (startDate.HasValue && i.TransactionDate.Value < startDate.Value) return false;
                        if (endDate.HasValue && i.TransactionDate.Value > endDate.Value) return false;
                        return true;
                    }).ToList();
                }

                var savedCount = 0;
                foreach (var inv in filteredInvoices)
                {
                    if (!inv.IsConfirmed)
                    {
                        inv.IsConfirmed = true;
                        await _invoiceService.UpdateAsync(inv);
                        savedCount++;
                    }
                }
                if (savedCount > 0)
                    StatusMessage = $"已导出并自动保存 {savedCount} 张发票";
            }
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
        if (invoice == null && SelectedInvoices.Count == 0) return;

        // If single invoice provided, add to list
        var toDelete = new List<Invoice>();
        if (invoice != null)
        {
            toDelete.Add(invoice);
        }
        else
        {
            toDelete.AddRange(SelectedInvoices.ToList());
        }

        // Confirm deletion
        var count = toDelete.Count;
        var msg = count == 1 
            ? $"确定要删除 {toDelete[0].IssuerName} 的发票吗？"
            : $"确定要删除选中的 {count} 张发票吗？";

        // Note: confirmation handled in UI layer
        foreach (var inv in toDelete)
        {
            await _invoiceService.DeleteAsync(inv.Id);
            Invoices.Remove(inv);
        }

        SelectedInvoices.Clear();
        if (SelectedInvoice != null && !Invoices.Contains(SelectedInvoice))
            SelectedInvoice = null;
    }
}
