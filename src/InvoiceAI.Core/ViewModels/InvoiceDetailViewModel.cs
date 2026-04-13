using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;

namespace InvoiceAI.Core.ViewModels;

public partial class InvoiceDetailViewModel : ObservableObject
{
    private readonly IInvoiceService _invoiceService;
    private readonly IAppSettingsService _settingsService;

    public InvoiceDetailViewModel(IInvoiceService invoiceService, IAppSettingsService settingsService)
    {
        _invoiceService = invoiceService;
        _settingsService = settingsService;
    }

    [ObservableProperty] private Invoice? _currentInvoice;
    [ObservableProperty] private List<InvoiceItem> _invoiceItems = [];
    [ObservableProperty] private string _invoiceTypeDisplay = string.Empty;
    [ObservableProperty] private string _missingFieldsDisplay = string.Empty;
    [ObservableProperty] private string _invoiceTypeColor = "#888";
    [ObservableProperty] private ObservableCollection<string> _availableCategories = [];

    // 可编辑字段属性
    [ObservableProperty] private string _editIssuerName = string.Empty;
    [ObservableProperty] private string _editRegistrationNumber = string.Empty;
    [ObservableProperty] private DateTime? _editTransactionDate;
    [ObservableProperty] private string _editDescription = string.Empty;
    [ObservableProperty] private string _editCategory = string.Empty;
    [ObservableProperty] private decimal? _editTaxExcludedAmount;
    [ObservableProperty] private decimal? _editTaxIncludedAmount;
    [ObservableProperty] private decimal? _editTaxAmount;
    [ObservableProperty] private string _editRecipientName = string.Empty;
    [ObservableProperty] private InvoiceType _editInvoiceType;

    // 编辑模式标志
    [ObservableProperty] private bool _isEditMode;

    // 验证错误消息（供 UI 层绑定显示）
    [ObservableProperty] private string _validationError = string.Empty;

    partial void OnCurrentInvoiceChanged(Invoice? value)
    {
        IsEditMode = false;
        ValidationError = string.Empty;

        if (value == null) return;

        // Parse items JSON
        try
        {
            InvoiceItems = JsonSerializer.Deserialize<List<InvoiceItem>>(value.ItemsJson ?? "[]") ?? [];
        }
        catch { InvoiceItems = []; }

        // Set type display
        InvoiceTypeDisplay = value.InvoiceType switch
        {
            InvoiceType.Standard => "✅ 標準インボイス",
            InvoiceType.Simplified => "⚠ 簡易インボイス",
            _ => "✗ 非适格"
        };

        InvoiceTypeColor = value.InvoiceType switch
        {
            InvoiceType.Standard => "#4CAF50",
            InvoiceType.Simplified => "#FF9800",
            _ => "#F44336"
        };

        MissingFieldsDisplay = string.Join(", ", value.MissingFields ?? "");
        AvailableCategories = new ObservableCollection<string>(_settingsService.Settings.Categories);

        // 加载可编辑字段
        LoadEditFields(value);
    }

    private void LoadEditFields(Invoice invoice)
    {
        EditIssuerName = invoice.IssuerName ?? string.Empty;
        EditRegistrationNumber = invoice.RegistrationNumber ?? string.Empty;
        EditTransactionDate = invoice.TransactionDate;
        EditDescription = invoice.Description ?? string.Empty;
        EditCategory = invoice.Category ?? string.Empty;
        EditTaxExcludedAmount = invoice.TaxExcludedAmount;
        EditTaxIncludedAmount = invoice.TaxIncludedAmount;
        EditTaxAmount = invoice.TaxAmount;
        EditRecipientName = invoice.RecipientName ?? string.Empty;
        EditInvoiceType = invoice.InvoiceType;
    }

    [RelayCommand]
    private void StartEditing()
    {
        if (CurrentInvoice == null) return;
        ValidationError = string.Empty;
        IsEditMode = true;
        LoadEditFields(CurrentInvoice);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (CurrentInvoice == null) return;

        // 验证必填字段
        if (string.IsNullOrWhiteSpace(EditIssuerName))
        {
            ValidationError = "発行事業者不能为空";
            return;
        }

        ValidationError = string.Empty;

        // 应用编辑
        CurrentInvoice.IssuerName = EditIssuerName.Trim();
        CurrentInvoice.RegistrationNumber = EditRegistrationNumber.Trim();
        CurrentInvoice.TransactionDate = EditTransactionDate;
        CurrentInvoice.Description = EditDescription.Trim();
        CurrentInvoice.Category = EditCategory.Trim();
        CurrentInvoice.TaxExcludedAmount = EditTaxExcludedAmount;
        CurrentInvoice.TaxIncludedAmount = EditTaxIncludedAmount;
        CurrentInvoice.TaxAmount = EditTaxAmount;
        CurrentInvoice.RecipientName = EditRecipientName.Trim();
        CurrentInvoice.InvoiceType = EditInvoiceType;

        // 序列化 ItemsJson
        CurrentInvoice.ItemsJson = JsonSerializer.Serialize(InvoiceItems);

        // 更新缺失字段显示
        MissingFieldsDisplay = string.Join(", ", CurrentInvoice.MissingFields ?? "");

        // 更新类型显示
        InvoiceTypeDisplay = CurrentInvoice.InvoiceType switch
        {
            InvoiceType.Standard => "✅ 標準インボイス",
            InvoiceType.Simplified => "⚠ 簡易インボイス",
            _ => "✗ 非适格"
        };

        InvoiceTypeColor = CurrentInvoice.InvoiceType switch
        {
            InvoiceType.Standard => "#4CAF50",
            InvoiceType.Simplified => "#FF9800",
            _ => "#F44336"
        };

        // 保存到数据库（不改变 IsConfirmed 和 Category，只更新编辑的字段）
        await _invoiceService.UpdateAsync(CurrentInvoice);

        IsEditMode = false;
    }

    [RelayCommand]
    private void CancelEditing()
    {
        if (CurrentInvoice == null) return;
        ValidationError = string.Empty;
        IsEditMode = false;
        LoadEditFields(CurrentInvoice);
    }

    [RelayCommand]
    private async Task UpdateCategoryAsync(string category)
    {
        if (CurrentInvoice == null) return;
        CurrentInvoice.Category = category;
        await _invoiceService.UpdateAsync(CurrentInvoice);
    }
}
