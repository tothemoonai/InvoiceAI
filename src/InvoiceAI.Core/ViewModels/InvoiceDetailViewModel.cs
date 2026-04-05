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

    partial void OnCurrentInvoiceChanged(Invoice? value)
    {
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
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (CurrentInvoice == null) return;
        CurrentInvoice.IsConfirmed = true;
        await _invoiceService.UpdateAsync(CurrentInvoice);
    }

    [RelayCommand]
    private async Task UpdateCategoryAsync(string category)
    {
        if (CurrentInvoice == null) return;
        CurrentInvoice.Category = category;
        await _invoiceService.UpdateAsync(CurrentInvoice);
    }
}
