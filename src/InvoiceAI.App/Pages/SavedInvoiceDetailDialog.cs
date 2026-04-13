using System.Text.Json;
using CommunityToolkit.Maui.Markup;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;
using InvoiceAI.App.Utils;
using Microsoft.Maui.Controls.Shapes;

namespace InvoiceAI.App.Pages;

public class SavedInvoiceDetailDialog : ContentPage
{
    private readonly int _invoiceId;
    private readonly SavedInvoicesViewModel _vm;
    private readonly IInvoiceService _invoiceService;
    private readonly IAppSettingsService _settingsService;

    private Invoice _invoice = null!;
    private List<InvoiceItem> _items = [];

    private Entry _issuerEntry = null!;
    private Entry _regNumberEntry = null!;
    private DatePicker _datePicker = null!;
    private Entry _descriptionEntry = null!;
    private Picker _categoryPicker = null!;
    private Entry _exclAmountEntry = null!;
    private Entry _inclAmountEntry = null!;
    private Entry _taxAmountEntry = null!;
    private Picker _typePicker = null!;
    private Entry _recipientEntry = null!;
    private CollectionView _itemsList = null!;
    private Label _errorLabel = null!;

    public SavedInvoiceDetailDialog(int invoiceId, SavedInvoicesViewModel viewModel, IInvoiceService invoiceService, IAppSettingsService settingsService)
    {
        _invoiceId = invoiceId;
        _vm = viewModel;
        _invoiceService = invoiceService;
        _settingsService = settingsService;
        BackgroundColor = ThemeManager.Background;
        Title = "✏ 编辑发票详情";
        
        // 先显示加载中
        Content = new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new ActivityIndicator { IsRunning = true, Color = ThemeManager.BrandPrimary, Scale = 1.5 },
                new Label { Text = "加载中...", FontSize = 14, TextColor = ThemeManager.TextSecondary, Margin = new Thickness(0, 8, 0, 0) }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadInvoiceData();
    }

    private async Task LoadInvoiceData()
    {
        _invoice = await _invoiceService.GetByIdAsync(_invoiceId);
        if (_invoice == null)
        {
            await DisplayAlert("错误", "发票不存在或已被删除", "OK");
            await Navigation.PopAsync();
            return;
        }

        try
        {
            _items = JsonSerializer.Deserialize<List<InvoiceItem>>(_invoice.ItemsJson ?? "[]") ?? [];
        }
        catch
        {
            _items = [];
        }

        // 确保在主线程上更新UI
        MainThread.BeginInvokeOnMainThread(() => BuildUI());
    }

    private void BuildUI()
    {
        var cardBg = ThemeManager.CardBackground;

        // 使用 Grid 直接放置所有表单字段，不嵌套 Grid
        var formGrid = new Grid
        {
            RowDefinitions = GenerateRows(9),
            ColumnDefinitions = { new ColumnDefinition(100), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
            Padding = new Thickness(16, 12),
            RowSpacing = 8
        };

        // 发行方
        formGrid.Add(new Label { Text = "发行方", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 0);
        _issuerEntry = CreateEntry(_invoice.IssuerName);
        formGrid.Add(_issuerEntry, 1, 0);

        // 登録番号
        formGrid.Add(new Label { Text = "登録番号", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 1);
        _regNumberEntry = CreateEntry(_invoice.RegistrationNumber);
        formGrid.Add(_regNumberEntry, 1, 1);

        // 交易日期
        formGrid.Add(new Label { Text = "交易日期", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 2);
        _datePicker = new DatePicker
        {
            Date = _invoice.TransactionDate ?? DateTime.Now,
            Format = "yyyy-MM-dd",
            TextColor = ThemeManager.TextPrimary,
            FontSize = 13
        };
        formGrid.Add(_datePicker, 1, 2);

        // 内容
        formGrid.Add(new Label { Text = "内容", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 3);
        _descriptionEntry = CreateEntry(_invoice.Description);
        formGrid.Add(_descriptionEntry, 1, 3);

        // 分类
        formGrid.Add(new Label { Text = "分类", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 4);
        _categoryPicker = CreateCategoryPicker();
        formGrid.Add(_categoryPicker, 1, 4);

        // 税抜金額
        formGrid.Add(new Label { Text = "税抜金額", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 5);
        _exclAmountEntry = CreateEntry(_invoice.TaxExcludedAmount?.ToString() ?? "");
        formGrid.Add(_exclAmountEntry, 1, 5);

        // 税込金額
        formGrid.Add(new Label { Text = "税込金額", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 6);
        _inclAmountEntry = CreateEntry(_invoice.TaxIncludedAmount?.ToString() ?? "");
        formGrid.Add(_inclAmountEntry, 1, 6);

        // 消费税額
        formGrid.Add(new Label { Text = "消费税額", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 7);
        _taxAmountEntry = CreateEntry(_invoice.TaxAmount?.ToString() ?? "");
        formGrid.Add(_taxAmountEntry, 1, 7);

        // 交付先
        formGrid.Add(new Label { Text = "交付先", FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 8);
        _recipientEntry = CreateEntry(_invoice.RecipientName);
        formGrid.Add(_recipientEntry, 1, 8);

        var formCard = new Border
        {
            BackgroundColor = cardBg,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            Content = formGrid
        };

        var typeCard = new Border
        {
            BackgroundColor = cardBg,
            Padding = new Thickness(16, 12),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = "发票类型", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = ThemeManager.TextSecondary },
                    (_typePicker = new Picker
                    {
                        ItemsSource = new List<string> { "標準インボイス", "簡易インボイス", "非适格" },
                        SelectedItem = _invoice.InvoiceType switch
                        {
                            InvoiceType.Standard => "標準インボイス",
                            InvoiceType.Simplified => "簡易インボイス",
                            _ => "非适格"
                        }
                    })
                }
            }
        };

        var itemsCard = new Border
        {
            BackgroundColor = cardBg,
            Padding = new Thickness(16, 12),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = $"明細項目 ({_items.Count} 项)", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = ThemeManager.TextSecondary },
                    (_itemsList = new CollectionView
                    {
                        ItemsSource = _items,
                        ItemTemplate = new DataTemplate(() =>
                        {
                            var nameEntry = new Entry { FontSize = 12, Placeholder = "品目名" };
                            nameEntry.SetBinding(Entry.TextProperty, nameof(InvoiceItem.Name));
                            var rateEntry = new Entry { FontSize = 12, Placeholder = "税率", WidthRequest = 50, Keyboard = Keyboard.Numeric };
                            rateEntry.SetBinding(Entry.TextProperty, nameof(InvoiceItem.TaxRate));
                            var amountEntry = new Entry { FontSize = 12, Placeholder = "金额", WidthRequest = 80, Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.End };
                            amountEntry.SetBinding(Entry.TextProperty, nameof(InvoiceItem.Amount));
                            return new Grid
                            {
                                ColumnDefinitions = { new ColumnDefinition(new GridLength(1, GridUnitType.Star)), new ColumnDefinition(60), new ColumnDefinition(90) },
                                Padding = new Thickness(0, 2),
                                Children = { nameEntry.Column(0), new Label { Text = "%", FontSize = 12, VerticalOptions = LayoutOptions.Center }.Column(1), amountEntry.Column(2) }
                            };
                        })
                    })
                }
            }
        };

        _errorLabel = new Label { FontSize = 12, TextColor = ThemeManager.Error, IsVisible = false, Margin = new Thickness(0, 4) };

        var saveBtn = new Button { Text = "💾 保存", BackgroundColor = ThemeManager.Success, TextColor = Colors.White, FontSize = 14 };
        saveBtn.Clicked += OnSaveClicked;

        var cancelBtn = new Button { Text = "✕ 取消", BackgroundColor = ThemeManager.TextSecondary, TextColor = Colors.White, FontSize = 14 };
        cancelBtn.Clicked += async (s, e) => await Navigation.PopAsync();

        var deleteBtn = new Button { Text = "🗑 删除", BackgroundColor = ThemeManager.Error, TextColor = Colors.White, FontSize = 14 };
        deleteBtn.Clicked += OnDeleteClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Padding = new Thickness(16),
                Children =
                {
                    formCard,
                    typeCard,
                    itemsCard,
                    _errorLabel,
                    new HorizontalStackLayout { Spacing = 12, Children = { saveBtn, cancelBtn, deleteBtn } }
                }
            }
        };
    }

    private static Grid BuildFormRow(string labelText, View input) => new()
    {
        ColumnDefinitions = { new ColumnDefinition(100), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
        Children =
        {
            new Label { Text = labelText, FontSize = 13, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center }.Column(0),
            input.Column(1)
        }
    };

    private static Entry CreateEntry(string text)
    {
        var entry = new Entry
        {
            Text = text ?? string.Empty,
            FontSize = 13,
            TextColor = ThemeManager.TextPrimary,
            BackgroundColor = ThemeManager.Background,
            Placeholder = "请输入",
            PlaceholderColor = ThemeManager.TextTertiary
        };
        // 确保 Entry 可见
        entry.IsVisible = true;
        entry.IsEnabled = true;
        return entry;
    }

    private Picker CreateCategoryPicker()
    {
        var categories = _settingsService.Settings.Categories.ToList();
        var picker = new Picker
        {
            ItemsSource = categories,
            FontSize = 13,
            TextColor = ThemeManager.TextPrimary
        };
        
        // 设置选中项
        if (!string.IsNullOrEmpty(_invoice.Category))
        {
            var index = categories.IndexOf(_invoice.Category);
            if (index >= 0)
                picker.SelectedIndex = index;
        }
        
        return picker;
    }

    private static RowDefinitionCollection GenerateRows(int count)
    {
        var rows = new RowDefinitionCollection();
        for (int i = 0; i < count; i++) rows.Add(new RowDefinition(new GridLength(1, GridUnitType.Auto)));
        return rows;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        _errorLabel.IsVisible = false;

        if (string.IsNullOrWhiteSpace(_issuerEntry.Text)) { ShowError("发行方名称不能为空"); return; }
        if (!string.IsNullOrWhiteSpace(_exclAmountEntry.Text) && !decimal.TryParse(_exclAmountEntry.Text, out _)) { ShowError("税抜金额格式不正确"); return; }
        if (!string.IsNullOrWhiteSpace(_inclAmountEntry.Text) && !decimal.TryParse(_inclAmountEntry.Text, out _)) { ShowError("税込金额格式不正确"); return; }
        if (!string.IsNullOrWhiteSpace(_taxAmountEntry.Text) && !decimal.TryParse(_taxAmountEntry.Text, out _)) { ShowError("消费税额格式不正确"); return; }

        _invoice.IssuerName = _issuerEntry.Text?.Trim() ?? "";
        _invoice.RegistrationNumber = _regNumberEntry.Text?.Trim() ?? "";
        _invoice.TransactionDate = _datePicker.Date;
        _invoice.Description = _descriptionEntry.Text?.Trim() ?? "";
        _invoice.Category = _categoryPicker.SelectedItem?.ToString() ?? "未分类";
        _invoice.RecipientName = string.IsNullOrWhiteSpace(_recipientEntry.Text) ? null : _recipientEntry.Text.Trim();
        _invoice.TaxExcludedAmount = string.IsNullOrWhiteSpace(_exclAmountEntry.Text) ? null : decimal.Parse(_exclAmountEntry.Text);
        _invoice.TaxIncludedAmount = string.IsNullOrWhiteSpace(_inclAmountEntry.Text) ? null : decimal.Parse(_inclAmountEntry.Text);
        _invoice.TaxAmount = string.IsNullOrWhiteSpace(_taxAmountEntry.Text) ? null : decimal.Parse(_taxAmountEntry.Text);
        _invoice.InvoiceType = _typePicker.SelectedItem switch
        {
            "標準インボイス" => InvoiceType.Standard,
            "簡易インボイス" => InvoiceType.Simplified,
            _ => InvoiceType.NonQualified
        };
        _invoice.ItemsJson = JsonSerializer.Serialize(_items);
        _invoice.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _vm.UpdateInvoiceCommand.ExecuteAsync(_invoice);
            await DisplayAlert("保存成功", "发票信息已更新", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("确认删除", $"确定要删除 {_invoice.IssuerName} 的发票吗？此操作不可撤销。", "删除", "取消");
        if (!confirm) return;

        try
        {
            await _vm.DeleteInvoiceCommand.ExecuteAsync(new SavedInvoiceRow { Id = _invoice.Id, IssuerName = _invoice.IssuerName });
            await DisplayAlert("删除成功", "发票已删除", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
        }
    }

    private void ShowError(string message) { _errorLabel.Text = message; _errorLabel.IsVisible = true; }
}
