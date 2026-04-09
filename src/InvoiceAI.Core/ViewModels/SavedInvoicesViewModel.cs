using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;

namespace InvoiceAI.Core.ViewModels;

/// <summary>
/// 用于已保存发票列表窗口的行数据（简化显示）
/// </summary>
public partial class SavedInvoiceRow : ObservableObject
{
    public int Id { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string IssuerName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? TaxExcludedAmount { get; set; }
    public decimal? TaxIncludedAmount { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsConfirmed { get; set; }

    public string InvoiceTypeDisplay => InvoiceType switch
    {
        InvoiceType.Standard => "標準",
        InvoiceType.Simplified => "簡易",
        _ => "非适格"
    };
}

public partial class SavedInvoicesViewModel : ObservableObject
{
    private readonly IInvoiceService _invoiceService;
    private readonly IAppSettingsService _settingsService;
    private List<SavedInvoiceRow> _allRows = [];

    public SavedInvoicesViewModel(IInvoiceService invoiceService, IAppSettingsService settingsService)
    {
        _invoiceService = invoiceService;
        _settingsService = settingsService;
    }

    [ObservableProperty] private ObservableCollection<SavedInvoiceRow> _invoices = [];
    [ObservableProperty] private ObservableCollection<string> _categories = ["全部"];
    [ObservableProperty] private string _selectedCategory = "全部";
    [ObservableProperty] private DateTime? _filterStartDate;
    [ObservableProperty] private DateTime? _filterEndDate;
    [ObservableProperty] private string _sortMode = "TransactionDate"; // 或 "CreatedAt"
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnFilterStartDateChanged(DateTime? value) => ApplyFilters();
    partial void OnFilterEndDateChanged(DateTime? value) => ApplyFilters();
    partial void OnSortModeChanged(string value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var invoices = await _invoiceService.GetConfirmedAsync();

            _allRows = invoices.Select(MapToRow).ToList();

            // 加载分类
            var cats = await _invoiceService.GetDistinctCategoriesAsync();
            Categories.Clear();
            Categories.Add("全部");
            foreach (var cat in cats) Categories.Add(cat);

            ApplyFilters();
            StatusMessage = $"共 {_allRows.Count} 条记录";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        var filtered = _allRows.AsEnumerable();

        // 按分类过滤
        if (SelectedCategory != "全部")
        {
            filtered = filtered.Where(i => i.Category == SelectedCategory);
        }

        // 按创建日期（导出时间）过滤
        if (FilterStartDate.HasValue)
        {
            var start = FilterStartDate.Value.Date;
            filtered = filtered.Where(i => i.CreatedAt.Date >= start);
        }
        if (FilterEndDate.HasValue)
        {
            var end = FilterEndDate.Value.Date.AddDays(1).AddTicks(-1);
            filtered = filtered.Where(i => i.CreatedAt <= end);
        }

        // 排序
        var list = SortMode switch
        {
            "CreatedAt" => filtered.OrderByDescending(i => i.CreatedAt).ToList(),
            _ => filtered.OrderBy(i => i.TransactionDate ?? DateTime.MaxValue).ToList()
        };

        Invoices.Clear();
        foreach (var item in list) Invoices.Add(item);

        StatusMessage = $"显示 {Invoices.Count} / {_allRows.Count} 条记录";
    }

    [RelayCommand]
    private async Task DeleteInvoiceAsync(SavedInvoiceRow? row)
    {
        if (row == null) return;

        await _invoiceService.DeleteAsync(row.Id);
        _allRows.RemoveAll(r => r.Id == row.Id);
        ApplyFilters();
        StatusMessage = $"已删除: {row.IssuerName}";
    }

    [RelayCommand]
    private async Task SaveInvoiceAsync(Invoice invoice)
    {
        await _invoiceService.UpdateAsync(invoice);
        await LoadDataAsync();
    }

    private static SavedInvoiceRow MapToRow(Invoice inv) => new()
    {
        Id = inv.Id,
        TransactionDate = inv.TransactionDate,
        IssuerName = inv.IssuerName,
        RegistrationNumber = inv.RegistrationNumber,
        Description = inv.Description,
        Category = inv.Category,
        TaxExcludedAmount = inv.TaxExcludedAmount,
        TaxIncludedAmount = inv.TaxIncludedAmount,
        InvoiceType = inv.InvoiceType,
        CreatedAt = inv.CreatedAt,
        IsConfirmed = inv.IsConfirmed
    };
}
