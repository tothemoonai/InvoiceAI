# 已保存发票列表管理功能 - 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 创建独立的已保存发票列表窗口，支持表格展示、筛选、编辑、删除功能；同时移除主界面的"确认"相关功能。

**Architecture:** 新增 SavedInvoicesViewModel + SavedInvoicesWindow（独立 MAUI Window）+ SavedInvoiceDetailDialog（模态对话框）。主界面修改为默认显示未导出发票，移除确认按钮和开关。

**Tech Stack:** .NET MAUI, CommunityToolkit.Mvvm, EF Core, SQLite, CommunityToolkit.Maui.Markup

---

## 文件结构

### 新增文件
| 文件 | 职责 |
|------|------|
| `src/InvoiceAI.Core/ViewModels/SavedInvoicesViewModel.cs` | 已保存列表 ViewModel |
| `src/InvoiceAI.App/Pages/SavedInvoicesWindow.cs` | 已保存列表窗口 UI |
| `src/InvoiceAI.App/Pages/SavedInvoiceDetailDialog.cs` | 发票详情编辑对话框 |

### 修改文件
| 文件 | 修改内容 |
|------|----------|
| `src/InvoiceAI.Core/Services/IInvoiceService.cs` | 增加 `GetByCreateDateRangeAsync` 方法签名 |
| `src/InvoiceAI.Core/Services/InvoiceService.cs` | 实现 `GetByCreateDateRangeAsync` 方法 |
| `src/InvoiceAI.Core/ViewModels/MainViewModel.cs` | 移除 `ShowConfirmedOnly` 属性和方法；修改 `LoadDataAsync` 和 `RefreshInvoiceListAsync` 默认查询未导出发票 |
| `src/InvoiceAI.Core/ViewModels/InvoiceDetailViewModel.cs` | 移除 `SaveAsync` 命令 |
| `src/InvoiceAI.App/Pages/MainPage.cs` | 增加"已保存列表"按钮；移除"确认"按钮、"仅显示已确认"开关、"✅"徽章；修改中间栏标题 |
| `src/InvoiceAI.App/MauiProgram.cs` | 注册 `SavedInvoicesViewModel` |

---

## 任务分解

### Task 1: 扩展 IInvoiceService 和 InvoiceService - 增加按创建时间范围查询

**Files:**
- Modify: `src/InvoiceAI.Core/Services/IInvoiceService.cs:12`
- Modify: `src/InvoiceAI.Core/Services/InvoiceService.cs:40`

- [ ] **Step 1: 在 IInvoiceService 接口中增加新方法签名**

在 `GetConfirmedAsync()` 方法后面增加：

```csharp
    Task<List<Invoice>> GetByCreateDateRangeAsync(DateTime start, DateTime end);
    List<string> GetDistinctCategories();
```

- [ ] **Step 2: 在 InvoiceService 中实现新方法**

在 `GetConfirmedAsync()` 方法后面增加：

```csharp
    public async Task<List<Invoice>> GetByCreateDateRangeAsync(DateTime start, DateTime end)
        => await _dbContext.Invoices
            .Where(i => i.IsConfirmed && i.CreatedAt >= start && i.CreatedAt <= end)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

    public List<string> GetDistinctCategories()
        => _dbContext.Invoices
            .Where(i => i.IsConfirmed)
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 4: Commit**

```bash
git add src/InvoiceAI.Core/Services/IInvoiceService.cs src/InvoiceAI.Core/Services/InvoiceService.cs
git commit -m "feat: 增加按创建时间范围查询已确认发票的服务方法"
```

---

### Task 2: 创建 SavedInvoicesViewModel

**Files:**
- Create: `src/InvoiceAI.Core/ViewModels/SavedInvoicesViewModel.cs`

- [ ] **Step 1: 创建 SavedInvoicesViewModel 完整代码**

```csharp
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

    partial void OnSelectedCategoryChanged(string? value) => ApplyFilters();
    partial void OnFilterStartDateChanged(DateTime? value) => ApplyFilters();
    partial void OnFilterEndDateChanged(DateTime? value) => ApplyFilters();
    partial void OnSortModeChanged(string? value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var invoices = await _invoiceService.GetConfirmedAsync();

            _allRows = invoices.Select(MapToRow).ToList();

            // 加载分类
            var cats = _invoiceService.GetDistinctCategories();
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

        // 确认删除由 UI 层处理，这里直接执行
        await _invoiceService.DeleteAsync(row.Id);
        _allRows.RemoveAll(r => r.Id == row.Id);
        ApplyFilters();
        StatusMessage = $"已删除: {row.IssuerName}";
    }

    [RelayCommand]
    private async Task SaveInvoiceAsync(Invoice invoice)
    {
        await _invoiceService.UpdateAsync(invoice);
        // 刷新数据
        await LoadDataAsync();
    }

    public Invoice? GetInvoiceById(int id)
    {
        return _allRows.FirstOrDefault(r => r.Id == id) is var row && row.Id != 0
            ? MapToInvoice(row)
            : null;
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

    // SavedInvoiceRow → Invoice（用于编辑后保存）
    private static Invoice MapToInvoice(SavedInvoiceRow row) => new()
    {
        Id = row.Id,
        TransactionDate = row.TransactionDate,
        IssuerName = row.IssuerName,
        RegistrationNumber = row.RegistrationNumber,
        Description = row.Description,
        Category = row.Category,
        TaxExcludedAmount = row.TaxExcludedAmount,
        TaxIncludedAmount = row.TaxIncludedAmount,
        InvoiceType = row.InvoiceType,
        CreatedAt = row.CreatedAt,
        IsConfirmed = row.IsConfirmed,
        // 以下字段从数据库原有数据保留，编辑时需从数据库加载完整对象
    };
}
```

**注意**: `SaveInvoiceAsync` 接收完整 Invoice 对象，由详情对话框传入编辑后的完整数据。`GetInvoiceById` 方法仅用于简单场景，实际编辑时应通过 `_invoiceService.GetByIdAsync(id)` 获取完整对象。

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.Core/ViewModels/SavedInvoicesViewModel.cs
git commit -m "feat: 创建已保存列表 ViewModel"
```

---

### Task 3: 创建 SavedInvoicesWindow UI

**Files:**
- Create: `src/InvoiceAI.App/Pages/SavedInvoicesWindow.cs`

- [ ] **Step 1: 创建 SavedInvoicesWindow 完整代码**

```csharp
using CommunityToolkit.Maui.Markup;
using InvoiceAI.Core.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using InvoiceAI.App.Utils;

namespace InvoiceAI.App.Pages;

public class SavedInvoicesWindow : ContentPage
{
    private readonly SavedInvoicesViewModel _vm;
    private CollectionView _invoiceTable = null!;
    private DatePicker _startDatePicker = null!;
    private DatePicker _endDatePicker = null!;
    private Picker _categoryPicker = null!;
    private Label _statusLabel = null!;

    public SavedInvoicesWindow(SavedInvoicesViewModel viewModel)
    {
        _vm = viewModel;
        BindingContext = viewModel;

        Title = "已保存发票列表";
        BackgroundColor = ThemeManager.Background;

        BuildUI();
        WireEvents();

        _vm.LoadDataCommand.Execute(null);
    }

    private void BuildUI()
    {
        _statusLabel = new Label
        {
            FontSize = 12,
            TextColor = ThemeManager.TextSecondary,
            Padding = new Thickness(16, 4)
        };
        _statusLabel.SetBinding(Label.TextProperty, nameof(_vm.StatusMessage));

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(1, GridUnitType.Auto)),   // 工具栏
                new RowDefinition(new GridLength(1, GridUnitType.Star)),   // 表格
                new RowDefinition(new GridLength(1, GridUnitType.Auto))    // 状态栏
            },
            Children =
            {
                BuildToolbar().Row(0),
                BuildTable().Row(1),
                _statusLabel.Row(2)
            }
        };
    }

    private View BuildToolbar()
    {
        // 分类筛选
        _categoryPicker = new Picker
        {
            WidthRequest = 120,
            VerticalOptions = LayoutOptions.Center
        };
        _categoryPicker.SetBinding(Picker.ItemsSourceProperty, nameof(_vm.Categories));
        _categoryPicker.SetBinding(Picker.SelectedItemProperty, nameof(_vm.SelectedCategory));

        // 日期范围
        var startLabel = new Label
        {
            Text = "开始日期:",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        _startDatePicker = new DatePicker
        {
            WidthRequest = 130,
            VerticalOptions = LayoutOptions.Center,
            Format = "yyyy-MM-dd"
        };
        _startDatePicker.SetBinding(DatePicker.DateProperty, nameof(_vm.FilterStartDate));

        var endLabel = new Label
        {
            Text = "结束日期:",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        _endDatePicker = new DatePicker
        {
            WidthRequest = 130,
            VerticalOptions = LayoutOptions.Center,
            Format = "yyyy-MM-dd"
        };
        _endDatePicker.SetBinding(DatePicker.DateProperty, nameof(_vm.FilterEndDate));

        // 排序选择
        var sortLabel = new Label
        {
            Text = "排序:",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var sortPicker = new Picker
        {
            WidthRequest = 120,
            VerticalOptions = LayoutOptions.Center,
            ItemsSource = new List<string> { "交易日期", "导出时间" }
        };
        sortPicker.SelectedIndexChanged += (s, e) =>
        {
            _vm.SortMode = sortPicker.SelectedIndex == 1 ? "CreatedAt" : "TransactionDate";
        };

        // 刷新按钮
        var refreshBtn = new Button
        {
            Text = "🔄 刷新",
            FontSize = 12,
            WidthRequest = 80,
            HeightRequest = 32,
            Margin = new Thickness(8, 0, 0, 0)
        };
        refreshBtn.SetBinding(Button.CommandProperty, nameof(_vm.LoadDataCommand));

        var clearFilterBtn = new Button
        {
            Text = "✕ 清除筛选",
            FontSize = 12,
            WidthRequest = 90,
            HeightRequest = 32,
            Margin = new Thickness(8, 0, 0, 0),
            BackgroundColor = ThemeManager.TextSecondary
        };
        clearFilterBtn.Clicked += (s, e) =>
        {
            _categoryPicker.SelectedItem = "全部";
            _startDatePicker.Date = DateTime.Now.AddMonths(-1);
            _endDatePicker.Date = DateTime.Now;
            _vm.FilterStartDate = null;
            _vm.FilterEndDate = null;
        };

        return new Border
        {
            BackgroundColor = ThemeManager.CardBackground,
            Padding = new Thickness(12, 8),
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "分类:", FontSize = 12, VerticalOptions = LayoutOptions.Center },
                            _categoryPicker,
                            startLabel,
                            _startDatePicker,
                            endLabel,
                            _endDatePicker,
                            sortLabel,
                            sortPicker,
                            refreshBtn,
                            clearFilterBtn
                        }
                    }
                }
            }
        };
    }

    private View BuildTable()
    {
        // 表头
        var header = BuildTableHeader();

        // 数据表格
        _invoiceTable = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsSource = _vm.Invoices,
            ItemTemplate = new DataTemplate(() => BuildTableRow()),
            EmptyView = new VerticalStackLayout
            {
                Padding = 40,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = "📋", FontSize = 48, Opacity = 0.3 },
                    new Label
                    {
                        Text = "暂无已保存的发票记录",
                        FontSize = 16,
                        TextColor = ThemeManager.TextSecondary
                    }
                }
            }
        };

        _invoiceTable.SelectionChanged += OnInvoiceSelected;

        return new Border
        {
            BackgroundColor = ThemeManager.Get("BackgroundTertiary", "DarkBackgroundTertiary"),
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),   // 表头
                    new RowDefinition(new GridLength(1, GridUnitType.Star))    // 数据
                },
                Children =
                {
                    header.Row(0),
                    _invoiceTable.Row(1)
                }
            }
        };
    }

    private static View BuildTableHeader()
    {
        var style = new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 12 },
                new Setter { Property = Label.FontAttributesProperty, Value = FontAttributes.Bold },
                new Setter { Property = Label.TextColorProperty, Value = ThemeManager.TextPrimary },
                new Setter { Property = Label.VerticalOptionsProperty, Value = LayoutOptions.Center }
            }
        };

        return new Border
        {
            BackgroundColor = ThemeManager.Background,
            Padding = new Thickness(8, 6),
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(100),   // 日期
                    new ColumnDefinition(150),   // 发行方
                    new ColumnDefinition(120),   // 登録番号
                    new ColumnDefinition(200),   // 内容
                    new ColumnDefinition(80),    // 分类
                    new ColumnDefinition(100),   // 税抜金额
                    new ColumnDefinition(100),   // 税込金额
                    new ColumnDefinition(80),    // 类型
                    new ColumnDefinition(140)    // 导出时间
                },
                Children =
                {
                    new Label { Text = "日期", Style = style }.Column(0),
                    new Label { Text = "发行方", Style = style }.Column(1),
                    new Label { Text = "登録番号", Style = style }.Column(2),
                    new Label { Text = "内容", Style = style }.Column(3),
                    new Label { Text = "分类", Style = style }.Column(4),
                    new Label { Text = "税抜金額", Style = style, HorizontalOptions = LayoutOptions.End }.Column(5),
                    new Label { Text = "税込金額", Style = style, HorizontalOptions = LayoutOptions.End }.Column(6),
                    new Label { Text = "类型", Style = style }.Column(7),
                    new Label { Text = "导出时间", Style = style }.Column(8)
                }
            }
        };
    }

    private static View BuildTableRow()
    {
        var dateLbl = CreateLabel(12, ThemeManager.TextSecondary);
        dateLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.TransactionDate), stringFormat: "{0:yyyy-MM-dd}");

        var issuerLbl = CreateLabel(12, ThemeManager.TextPrimary);
        issuerLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.IssuerName));

        var regLbl = CreateLabel(11, ThemeManager.TextTertiary);
        regLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.RegistrationNumber));

        var descLbl = CreateLabel(12, ThemeManager.TextPrimary);
        descLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.Description));

        var catLbl = CreateLabel(11, ThemeManager.TextSecondary);
        catLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.Category));

        var exclLbl = CreateLabel(12, ThemeManager.TextSecondary, LayoutOptions.End);
        exclLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.TaxExcludedAmount), stringFormat: "¥{0:N0}");

        var inclLbl = CreateLabel(12, ThemeManager.BrandPrimary, LayoutOptions.End);
        inclLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.TaxIncludedAmount), stringFormat: "¥{0:N0}");

        var typeLbl = CreateLabel(11, ThemeManager.TextSecondary);
        typeLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.InvoiceTypeDisplay));

        var createdLbl = CreateLabel(11, ThemeManager.TextTertiary);
        createdLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.CreatedAt), stringFormat: "{0:yyyy-MM-dd HH:mm}");

        var row = new Border
        {
            BackgroundColor = ThemeManager.CardBackground,
            Margin = new Thickness(0, 1),
            Padding = new Thickness(8, 4),
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(100),
                    new ColumnDefinition(150),
                    new ColumnDefinition(120),
                    new ColumnDefinition(200),
                    new ColumnDefinition(80),
                    new ColumnDefinition(100),
                    new ColumnDefinition(100),
                    new ColumnDefinition(80),
                    new ColumnDefinition(140)
                },
                Children =
                {
                    dateLbl.Column(0),
                    issuerLbl.Column(1),
                    regLbl.Column(2),
                    descLbl.Column(3),
                    catLbl.Column(4),
                    exclLbl.Column(5),
                    inclLbl.Column(6),
                    typeLbl.Column(7),
                    createdLbl.Column(8)
                }
            }
        };

        // 选中状态视觉反馈
        VisualStateManager.SetVisualStateGroups(row, new VisualStateGroupList
        {
            new VisualStateGroup
            {
                Name = "CommonStates",
                States =
                {
                    new VisualState
                    {
                        Name = "Normal",
                        Setters =
                        {
                            new Setter { Property = Border.BackgroundColorProperty, Value = ThemeManager.CardBackground }
                        }
                    },
                    new VisualState
                    {
                        Name = "Selected",
                        Setters =
                        {
                            new Setter { Property = Border.BackgroundColorProperty, Value = Color.FromArgb("#BBDEFB") }
                        }
                    }
                }
            }
        });

        return row;
    }

    private static Label CreateLabel(double fontSize, Color textColor, LayoutOptions? horizontalOptions = null)
    {
        var label = new Label
        {
            FontSize = fontSize,
            TextColor = textColor,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };
        if (horizontalOptions.HasValue)
            label.HorizontalOptions = horizontalOptions.Value;
        return label;
    }

    private void WireEvents()
    {
        // 分类选择变化时重新应用过滤
        _categoryPicker.SelectedIndexChanged += (s, e) =>
        {
            if (_categoryPicker.SelectedItem is string cat)
            {
                _vm.SelectedCategory = cat;
            }
        };
    }

    private async void OnInvoiceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is SavedInvoiceRow row)
        {
            // 清除选择以防止重复触发
            _invoiceTable.SelectedItem = null;

            // 弹出详情编辑对话框
            await OpenDetailDialog(row);
        }
    }

    private async Task OpenDetailDialog(SavedInvoiceRow row)
    {
        var dialog = new SavedInvoiceDetailDialog(row.Id, _vm);
        await Navigation.PushModalAsync(dialog);
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 编译失败（因为 SavedInvoiceDetailDialog 还不存在）— 这是预期的，下一步会创建

---

### Task 4: 创建 SavedInvoiceDetailDialog 详情编辑对话框

**Files:**
- Create: `src/InvoiceAI.App/Pages/SavedInvoiceDetailDialog.cs`

- [ ] **Step 1: 创建 SavedInvoiceDetailDialog 完整代码**

```csharp
using System.Text.Json;
using CommunityToolkit.Maui.Markup;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;
using InvoiceAI.App.Utils;
using Microsoft.Maui.Controls.Shapes;

namespace InvoiceAI.App.Pages;

public class SavedInvoiceDetailDialog : ContentPage
{
    private readonly int _invoiceId;
    private readonly SavedInvoicesViewModel _vm;
    private readonly IInvoiceService _invoiceService;

    private Invoice _invoice = null!;
    private List<InvoiceItem> _items = [];

    // 编辑控件
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

    public SavedInvoiceDetailDialog(int invoiceId, SavedInvoicesViewModel viewModel)
    {
        _invoiceId = invoiceId;
        _vm = viewModel;
        _invoiceService = viewModel.GetType().GetField("_invoiceService",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(viewModel) as IInvoiceService
            ?? throw new InvalidOperationException("无法获取 InvoiceService");

        BackgroundColor = Color.FromArgb("#80000000");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadInvoiceData();
    }

    private async Task LoadInvoiceData()
    {
        _invoice = await _invoiceService.GetByIdAsync(_invoiceId)
            ?? throw new InvalidOperationException("发票不存在");

        try
        {
            _items = JsonSerializer.Deserialize<List<InvoiceItem>>(_invoice.ItemsJson ?? "[]") ?? [];
        }
        catch
        {
            _items = [];
        }

        BuildUI();
    }

    private void BuildUI()
    {
        var cardBg = ThemeManager.CardBackground;

        // 基本信息编辑区
        var formCard = new Border
        {
            BackgroundColor = cardBg,
            Padding = new Thickness(16, 12),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            Content = new Grid
            {
                RowDefinitions = GenerateRows(8),
                ColumnDefinitions =
                {
                    new ColumnDefinition(100),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                },
                Children =
                {
                    BuildFormRow("发行方", _issuerEntry = CreateEntry(_invoice.IssuerName)).Row(0),
                    BuildFormRow("登録番号", _regNumberEntry = CreateEntry(_invoice.RegistrationNumber)).Row(1),
                    BuildFormRow("交易日期", _datePicker = new DatePicker
                    {
                        Date = _invoice.TransactionDate ?? DateTime.Now,
                        Format = "yyyy-MM-dd"
                    }).Row(2),
                    BuildFormRow("分类", _categoryPicker = CreateCategoryPicker()).Row(3),
                    BuildFormRow("税抜金額", _exclAmountEntry = CreateEntry(_invoice.TaxExcludedAmount?.ToString("") ?? "")).Row(4),
                    BuildFormRow("税込金額", _inclAmountEntry = CreateEntry(_invoice.TaxIncludedAmount?.ToString("") ?? "")).Row(5),
                    BuildFormRow("消費税額", _taxAmountEntry = CreateEntry(_invoice.TaxAmount?.ToString("") ?? "")).Row(6),
                    BuildFormRow("交付先", _recipientEntry = CreateEntry(_invoice.RecipientName ?? "")).Row(7)
                }
            }
        };

        // 类型选择
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
                    _typePicker = new Picker
                    {
                        ItemsSource = new List<string> { "標準インボイス", "簡易インボイス", "非适格" },
                        SelectedItem = _invoice.InvoiceType switch
                        {
                            InvoiceType.Standard => "標準インボイス",
                            InvoiceType.Simplified => "簡易インボイス",
                            _ => "非适格"
                        }
                    }
                }
            }
        };

        // 明細項目
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
                    _itemsList = new CollectionView
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
                                ColumnDefinitions =
                                {
                                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                                    new ColumnDefinition(60),
                                    new ColumnDefinition(90)
                                },
                                Padding = new Thickness(0, 2),
                                Children =
                                {
                                    nameEntry.Column(0),
                                    new Label { Text = "%", FontSize = 12, VerticalOptions = LayoutOptions.Center }.Column(1),
                                    amountEntry.Column(2)
                                }
                            };
                        })
                    }
                }
            }
        };

        // 错误提示
        _errorLabel = new Label
        {
            FontSize = 12,
            TextColor = ThemeManager.Error,
            IsVisible = false,
            Margin = new Thickness(0, 4)
        };

        // 操作按钮
        var saveBtn = new Button
        {
            Text = "💾 保存",
            BackgroundColor = ThemeManager.Success,
            TextColor = Colors.White,
            FontSize = 14
        };
        saveBtn.Clicked += OnSaveClicked;

        var cancelBtn = new Button
        {
            Text = "✕ 取消",
            BackgroundColor = ThemeManager.TextSecondary,
            TextColor = Colors.White,
            FontSize = 14
        };
        cancelBtn.Clicked += async (s, e) => await Navigation.PopModalAsync();

        var deleteBtn = new Button
        {
            Text = "🗑 删除",
            BackgroundColor = ThemeManager.Error,
            TextColor = Colors.White,
            FontSize = 14
        };
        deleteBtn.Clicked += OnDeleteClicked;

        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 12,
            Children = { saveBtn, cancelBtn, deleteBtn }
        };

        // 整体布局
        Content = new Border
        {
            BackgroundColor = ThemeManager.Background,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            Margin = new Thickness(40, 30),
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),   // 标题
                    new RowDefinition(new GridLength(1, GridUnitType.Star)),   // 内容
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),   // 错误
                    new RowDefinition(new GridLength(1, GridUnitType.Auto))    // 按钮
                },
                Children =
                {
                    new Label
                    {
                        Text = "✏ 编辑发票详情",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = ThemeManager.TextPrimary,
                        Padding = new Thickness(16, 12)
                    }.Row(0),
                    new ScrollView
                    {
                        Content = new VerticalStackLayout
                        {
                            Spacing = 12,
                            Padding = new Thickness(16, 0, 16, 16),
                            Children = { formCard, typeCard, itemsCard }
                        }
                    }.Row(1),
                    _errorLabel.Row(2),
                    new Border
                    {
                        BackgroundColor = ThemeManager.CardBackground,
                        Padding = new Thickness(16, 12),
                        StrokeShape = new RoundRectangle { CornerRadius = new Thickness(0, 0, 12, 12) },
                        StrokeThickness = 0,
                        Content = buttonRow
                    }.Row(3)
                }
            }
        };
    }

    private static Grid BuildFormRow(string labelText, View input)
    {
        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(100),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            },
            Children =
            {
                new Label
                {
                    Text = labelText,
                    FontSize = 13,
                    TextColor = ThemeManager.TextSecondary,
                    VerticalOptions = LayoutOptions.Center
                }.Column(0),
                input.Column(1)
            }
        };
    }

    private static Entry CreateEntry(string text) => new()
    {
        Text = text,
        FontSize = 13
    };

    private Picker CreateCategoryPicker()
    {
        var settingsService = _vm.GetType().GetField("_settingsService",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_vm) as IAppSettingsService;

        var categories = settingsService?.Settings.Categories.ToList() ?? new List<string> { "未分类" };
        return new Picker
        {
            ItemsSource = categories,
            SelectedItem = _invoice.Category,
            FontSize = 13
        };
    }

    private static RowDefinitionCollection GenerateRows(int count)
    {
        var rows = new RowDefinitionCollection();
        for (int i = 0; i < count; i++)
        {
            rows.Add(new RowDefinition(new GridLength(1, GridUnitType.Auto)));
        }
        return rows;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        _errorLabel.IsVisible = false;

        // 验证必填字段
        if (string.IsNullOrWhiteSpace(_issuerEntry.Text))
        {
            ShowError("发行方名称不能为空");
            return;
        }

        // 解析金额
        if (!string.IsNullOrWhiteSpace(_exclAmountEntry.Text) && !decimal.TryParse(_exclAmountEntry.Text, out _))
        {
            ShowError("税抜金额格式不正确");
            return;
        }
        if (!string.IsNullOrWhiteSpace(_inclAmountEntry.Text) && !decimal.TryParse(_inclAmountEntry.Text, out _))
        {
            ShowError("税込金额格式不正确");
            return;
        }
        if (!string.IsNullOrWhiteSpace(_taxAmountEntry.Text) && !decimal.TryParse(_taxAmountEntry.Text, out _))
        {
            ShowError("消费税额格式不正确");
            return;
        }

        // 更新发票数据
        _invoice.IssuerName = _issuerEntry.Text?.Trim() ?? "";
        _invoice.RegistrationNumber = _regNumberEntry.Text?.Trim() ?? "";
        _invoice.TransactionDate = _datePicker.Date;
        _invoice.Category = _categoryPicker.SelectedItem?.ToString() ?? "未分类";
        _invoice.Description = _descriptionEntry.Text?.Trim() ?? "";
        _invoice.RecipientName = string.IsNullOrWhiteSpace(_recipientEntry.Text) ? null : _recipientEntry.Text.Trim();

        _invoice.TaxExcludedAmount = string.IsNullOrWhiteSpace(_exclAmountEntry.Text)
            ? null
            : decimal.Parse(_exclAmountEntry.Text);

        _invoice.TaxIncludedAmount = string.IsNullOrWhiteSpace(_inclAmountEntry.Text)
            ? null
            : decimal.Parse(_inclAmountEntry.Text);

        _invoice.TaxAmount = string.IsNullOrWhiteSpace(_taxAmountEntry.Text)
            ? null
            : decimal.Parse(_taxAmountEntry.Text);

        _invoice.InvoiceType = _typePicker.SelectedItem switch
        {
            "標準インボイス" => InvoiceType.Standard,
            "簡易インボイス" => InvoiceType.Simplified,
            _ => InvoiceType.NonQualified
        };

        // 序列化明細項目
        _invoice.ItemsJson = JsonSerializer.Serialize(_items);
        _invoice.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _vm.SaveInvoiceAsync(_invoice);
            await DisplayAlert("保存成功", "发票信息已更新", "OK");
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("确认删除",
            $"确定要删除 {_invoice.IssuerName} 的发票吗？此操作不可撤销。",
            "删除", "取消");

        if (confirm)
        {
            try
            {
                await _vm.DeleteInvoiceCommand.ExecuteAsync(
                    new SavedInvoiceRow { Id = _invoice.Id, IssuerName = _invoice.IssuerName });
                await DisplayAlert("删除成功", "发票已删除", "OK");
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                ShowError($"删除失败: {ex.Message}");
            }
        }
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.IsVisible = true;
    }
}
```

**注意**: 上述代码中使用反射获取 `_invoiceService` 和 `_settingsService`。更好的方式是在构造函数中注入，但需要修改 MauiProgram。为了简化，我们采用反射方式。

**更好的方案**: 在构造函数中直接传入服务。修改构造函数:

```csharp
    private readonly IInvoiceService _invoiceService;
    private readonly IAppSettingsService _settingsService;

    public SavedInvoiceDetailDialog(int invoiceId, SavedInvoicesViewModel viewModel, IInvoiceService invoiceService, IAppSettingsService settingsService)
    {
        _invoiceId = invoiceId;
        _vm = viewModel;
        _invoiceService = invoiceService;
        _settingsService = settingsService;
        BackgroundColor = Color.FromArgb("#80000000");
    }
```

但这样需要在 SavedInvoicesWindow 中注入这些服务。让我用更简洁的方式 — 直接在 SavedInvoicesWindow 中通过 DI 获取服务并传入。

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 可能有编译错误（需要修复反射获取服务的问题），先尝试编译

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.App/Pages/SavedInvoiceDetailDialog.cs
git commit -m "feat: 创建发票详情编辑对话框"
```

---

### Task 5: 注册 SavedInvoicesViewModel 到 DI 容器

**Files:**
- Modify: `src/InvoiceAI.App/MauiProgram.cs:45`

- [ ] **Step 1: 在 MauiProgram 中注册 SavedInvoicesViewModel**

找到注册 ViewModel 的部分:
```csharp
        // ViewModels (singleton — shared state across MainPage)
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<InvoiceDetailViewModel>();
        builder.Services.AddSingleton<ImportViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
```

在 `SettingsViewModel` 后面增加:
```csharp
        builder.Services.AddSingleton<InvoiceAI.Core.ViewModels.SavedInvoicesViewModel>();
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.App/MauiProgram.cs
git commit -m "feat: 注册 SavedInvoicesViewModel 到 DI 容器"
```

---

### Task 6: 修改 MainViewModel — 移除 ShowConfirmedOnly，修改加载逻辑

**Files:**
- Modify: `src/InvoiceAI.Core/ViewModels/MainViewModel.cs`

- [ ] **Step 1: 移除 ShowConfirmedOnly 属性**

找到:
```csharp
    [ObservableProperty] private bool _showConfirmedOnly;
```
删除该行。

- [ ] **Step 2: 移除 OnShowConfirmedOnlyChanged 方法**

找到:
```csharp
    partial void OnShowConfirmedOnlyChanged(bool value)
    {
        _ = RefreshInvoiceListAsync();
    }
```
删除该方法。

- [ ] **Step 3: 修改 LoadDataAsync 方法**

将 `LoadDataAsync` 方法替换为:
```csharp
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            // 默认只加载未导出发票
            var invoices = await _invoiceService.GetUnconfirmedAsync();
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
```

- [ ] **Step 4: 修改 RefreshInvoiceListAsync 方法**

将 `RefreshInvoiceListAsync` 方法替换为:
```csharp
    private async Task RefreshInvoiceListAsync()
    {
        IsBusy = true;
        try
        {
            // 只显示未导出发票
            var invoices = SelectedCategory == "全部"
                ? await _invoiceService.GetUnconfirmedAsync()
                : await _invoiceService.GetByCategoryAsync(SelectedCategory)
                    .ContinueWith(t => t.Result.Where(i => !i.IsConfirmed).ToList());

            Invoices.Clear();
            foreach (var inv in invoices) Invoices.Add(inv);
        }
        finally { IsBusy = false; }
    }
```

- [ ] **Step 5: 增加 OpenSavedInvoicesCommand**

在 ExportAsync 方法后面增加:
```csharp
    // Open saved invoices window
    public Action? OpenSavedInvoicesWindow { get; set; }

    [RelayCommand]
    private void OpenSavedInvoices()
    {
        OpenSavedInvoicesWindow?.Invoke();
    }
```

- [ ] **Step 6: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 7: Commit**

```bash
git add src/InvoiceAI.Core/ViewModels/MainViewModel.cs
git commit -m "feat: 修改主ViewModel - 移除确认过滤，增加打开已保存列表命令"
```

---

### Task 7: 修改 InvoiceDetailViewModel — 移除 SaveAsync 命令

**Files:**
- Modify: `src/InvoiceAI.Core/ViewModels/InvoiceDetailViewModel.cs`

- [ ] **Step 1: 移除 SaveAsync 命令**

找到:
```csharp
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (CurrentInvoice == null) return;
        CurrentInvoice.IsConfirmed = true;
        await _invoiceService.UpdateAsync(CurrentInvoice);
    }
```
删除该方法。

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.Core/ViewModels/InvoiceDetailViewModel.cs
git commit -m "feat: 移除详情ViewModel的确认保存命令"
```

---

### Task 8: 修改 MainPage — 增加"已保存列表"按钮，移除确认相关 UI

**Files:**
- Modify: `src/InvoiceAI.App/Pages/MainPage.cs`

- [ ] **Step 1: 在 BuildCategoryPanel 中增加"已保存列表"按钮**

找到 BuildCategoryPanel 中的按钮区域:
```csharp
                    new VerticalStackLayout
                    {
                        Padding = 8,
                        Spacing = 6,
                        Children =
                        {
                            BuildActionButton("📥 导入", OnImportClicked, ThemeManager.BrandPrimary),
                            BuildActionButton("📤 导出", OnExportClicked, ThemeManager.Success),
                            BuildActionButton("⚙ 设置", OnSettingsClicked, ThemeManager.TextSecondary)
                        }
                    }.Row(2)
```

替换为:
```csharp
                    new VerticalStackLayout
                    {
                        Padding = 8,
                        Spacing = 6,
                        Children =
                        {
                            BuildActionButton("📥 导入", OnImportClicked, ThemeManager.BrandPrimary),
                            BuildActionButton("📤 导出", OnExportClicked, ThemeManager.Success),
                            BuildActionButton("💾 已保存", OnSavedInvoicesClicked, Color.FromArgb("#5C6BC0")),
                            BuildActionButton("⚙ 设置", OnSettingsClicked, ThemeManager.TextSecondary)
                        }
                    }.Row(2)
```

- [ ] **Step 2: 在 BuildInvoiceListPanel 中移除"仅显示已确认"开关和"✅"徽章**

移除确认开关相关代码。找到:
```csharp
        // Confirmed filter toggle
        var confirmedSwitch = new Switch
        {
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 4, 12, 0)
        };
        confirmedSwitch.SetBinding(Switch.IsToggledProperty, nameof(_vm.ShowConfirmedOnly));

        var confirmedLabel = new Label
        {
            Text = "仅显示已确认",
            FontSize = 12,
            TextColor = ThemeManager.TextSecondary,
            VerticalOptions = LayoutOptions.Center
        };

        var filterRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(8, 4, 8, 0),
            HorizontalOptions = LayoutOptions.End,
            Children = { confirmedLabel, confirmedSwitch }
        };
```
删除以上代码，替换为空的 filterRow:
```csharp
        // 过滤行（已移除确认开关，保留为空布局）
        var filterRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(8, 4, 8, 0),
            HorizontalOptions = LayoutOptions.End
        };
```

- [ ] **Step 3: 移除"✅"确认标记徽章**

找到发票列表模板中的 confirmedBadge 相关代码:
```csharp
                // Confirmed badge
                var confirmedBadge = new Border
                {
                    Padding = new Thickness(6, 2),
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    StrokeThickness = 0,
                    BackgroundColor = ThemeManager.Success,
                    HorizontalOptions = LayoutOptions.End,
                    Content = new Label
                    {
                        Text = "✅",
                        FontSize = 10
                    }
                };
                confirmedBadge.SetBinding(IsVisibleProperty, nameof(Invoice.IsConfirmed));
```
删除以上代码。

然后在 content 的 Children 中移除 confirmedBadge:
```csharp
                    Children =
                    {
                        issuerLabel.Row(0).Column(0),
                        typeBadge.Row(0).Column(1),
                        new HorizontalStackLayout
                        {
                            Spacing = 0,
                            Children = { dateLabel, amountLabel }
                        }.Row(1).Column(0).ColumnSpan(2),
                        catLabel.Row(2).Column(0),
                        confirmedBadge.Row(2).Column(1)
                    }
```

替换为:
```csharp
                    Children =
                    {
                        issuerLabel.Row(0).Column(0),
                        typeBadge.Row(0).Column(1),
                        new HorizontalStackLayout
                        {
                            Spacing = 0,
                            Children = { dateLabel, amountLabel }
                        }.Row(1).Column(0).ColumnSpan(2),
                        catLabel.Row(2).Column(0)
                    }
```

- [ ] **Step 4: 修改 BuildDetailPanel — 移除"确认"按钮**

找到 actions 部分:
```csharp
        // Action buttons (fixed at top, outside ScrollView)
        var actions = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 12, 16, 8),
            Children =
            {
                BuildActionButton("✅ 确认", OnSaveClicked, ThemeManager.Success),
                BuildActionButton("🗑 删除", OnDeleteClicked, ThemeManager.Error)
            }
        };
        actions.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));
```

替换为:
```csharp
        // Action buttons (fixed at top, outside ScrollView)
        var actions = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 12, 16, 8),
            Children =
            {
                BuildActionButton("🗑 删除", OnDeleteClicked, ThemeManager.Error)
            }
        };
        actions.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));
```

- [ ] **Step 5: 修改中间栏标题**

找到:
```csharp
        var savedHeader = new Label
        {
            Text = "📋 已保存记录",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeManager.BrandPrimary,
            Margin = new Thickness(12, 8, 0, 2)
        };
```

替换为:
```csharp
        var savedHeader = new Label
        {
            Text = "📋 未导出发票",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeManager.BrandPrimary,
            Margin = new Thickness(12, 8, 0, 2)
        };
```

- [ ] **Step 6: 移除 OnSaveClicked 方法**

找到:
```csharp
    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_detailVm.CurrentInvoice == null)
        {
            await this.DisplayAlert("提示", "请先选择一张发票", "OK");
            return;
        }

        await _detailVm.SaveCommand.ExecuteAsync(null);

        // 刷新列表以更新确认标记
        await _vm.LoadDataCommand.ExecuteAsync(null);

        await this.DisplayAlert("保存成功", "发票已确认保存。\n\n状态已标记为「已确认」✅", "OK");
    }
```
删除该方法。

- [ ] **Step 7: 增加 OnSavedInvoicesClicked 方法**

在 OnSettingsClicked 方法后面增加:
```csharp
    private async void OnSavedInvoicesClicked(object? sender, EventArgs e)
    {
        try
        {
            var viewModel = _services.GetRequiredService<InvoiceAI.Core.ViewModels.SavedInvoicesViewModel>();
            var savedWindow = new SavedInvoicesWindow(viewModel);
            var navPage = new NavigationPage(savedWindow);
            var window = new Window(navPage);
            await Application.Current!.OpenWindowAsync(window);
        }
        catch (Exception ex)
        {
            await this.DisplayAlert("错误", $"打开已保存列表失败:\n{ex.Message}", "OK");
        }
    }
```

- [ ] **Step 8: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 9: Commit**

```bash
git add src/InvoiceAI.App/Pages/MainPage.cs
git commit -m "feat: 主界面增加已保存列表按钮，移除确认相关UI"
```

---

### Task 9: 修复 SavedInvoiceDetailDialog 中的服务注入问题

**Files:**
- Modify: `src/InvoiceAI.App/Pages/SavedInvoiceDetailDialog.cs`
- Modify: `src/InvoiceAI.App/Pages/SavedInvoicesWindow.cs`

- [ ] **Step 1: 修改 SavedInvoiceDetailDialog 构造函数**

使用反射获取服务的方式不够优雅且容易出错。改为通过构造函数传入服务。

将构造函数修改为:
```csharp
    private readonly int _invoiceId;
    private readonly SavedInvoicesViewModel _vm;
    private readonly IInvoiceService _invoiceService;
    private readonly IAppSettingsService _settingsService;

    public SavedInvoiceDetailDialog(int invoiceId, SavedInvoicesViewModel viewModel, IInvoiceService invoiceService, IAppSettingsService settingsService)
    {
        _invoiceId = invoiceId;
        _vm = viewModel;
        _invoiceService = invoiceService;
        _settingsService = settingsService;
        BackgroundColor = Color.FromArgb("#80000000");
    }
```

删除 BuildUI 中 `CreateCategoryPicker` 方法里通过反射获取服务的代码，直接使用 `_settingsService`:
```csharp
    private Picker CreateCategoryPicker()
    {
        var categories = _settingsService.Settings.Categories.ToList();
        return new Picker
        {
            ItemsSource = categories,
            SelectedItem = _invoice.Category,
            FontSize = 13
        };
    }
```

- [ ] **Step 2: 修改 SavedInvoicesWindow，传入服务到对话框**

在 SavedInvoicesWindow 类中添加字段:
```csharp
    private readonly IInvoiceService _invoiceService;
    private readonly IAppSettingsService _settingsService;
```

修改构造函数:
```csharp
    public SavedInvoicesWindow(SavedInvoicesViewModel viewModel, IInvoiceService invoiceService, IAppSettingsService settingsService)
    {
        _vm = viewModel;
        _invoiceService = invoiceService;
        _settingsService = settingsService;
        BindingContext = viewModel;

        Title = "已保存发票列表";
        BackgroundColor = ThemeManager.Background;

        BuildUI();
        WireEvents();

        _vm.LoadDataCommand.Execute(null);
    }
```

修改 OpenDetailDialog 方法:
```csharp
    private async Task OpenDetailDialog(SavedInvoiceRow row)
    {
        var dialog = new SavedInvoiceDetailDialog(row.Id, _vm, _invoiceService, _settingsService);
        await Navigation.PushModalAsync(dialog);
    }
```

- [ ] **Step 3: 修改 Task 5 中 MauiProgram 的注册，改为 Transient**

SavedInvoicesWindow 需要在创建时注入服务，不能直接注册为 Singleton。我们在 MainPage 中通过 DI 获取 ViewModel，但窗口本身是动态创建的。

MauiProgram 保持不变（只注册 ViewModel）。

- [ ] **Step 4: 修改 MainPage 的 OnSavedInvoicesClicked**

修改为:
```csharp
    private async void OnSavedInvoicesClicked(object? sender, EventArgs e)
    {
        try
        {
            var viewModel = _services.GetRequiredService<InvoiceAI.Core.ViewModels.SavedInvoicesViewModel>();
            var invoiceService = _services.GetRequiredService<InvoiceAI.Core.Services.IInvoiceService>();
            var settingsService = _services.GetRequiredService<InvoiceAI.Core.Services.IAppSettingsService>();
            var savedWindow = new SavedInvoicesWindow(viewModel, invoiceService, settingsService);
            var navPage = new NavigationPage(savedWindow);
            var window = new Window(navPage);
            await Application.Current!.OpenWindowAsync(window);
        }
        catch (Exception ex)
        {
            await this.DisplayAlert("错误", $"打开已保存列表失败:\n{ex.Message}", "OK");
        }
    }
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 6: Commit**

```bash
git add src/InvoiceAI.App/Pages/SavedInvoiceDetailDialog.cs src/InvoiceAI.App/Pages/SavedInvoicesWindow.cs src/InvoiceAI.App/Pages/MainPage.cs
git commit -m "fix: 修复详情对话框的服务注入问题"
```

---

### Task 10: 端到端测试和最终验证

- [ ] **Step 1: 清理并重新编译**

Run: `dotnet clean src/InvoiceAI.App/InvoiceAI.App.csproj`
Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 2: 运行程序并手动测试以下场景**

1. 主界面左栏显示: 导入 → 导出 → **已保存列表** → 设置
2. 主界面中间栏只显示**未导出**的发票（IsConfirmed=false）
3. 主界面无"确认"按钮、无"仅显示已确认"开关、无"✅"徽章
4. 点击"已保存列表"按钮 → 弹出独立窗口
5. 已保存窗口显示所有 IsConfirmed=true 的发票表格
6. 按分类筛选有效
7. 按日期范围筛选有效
8. 切换排序方式有效
9. 点击某行 → 弹出详情编辑对话框
10. 编辑详情 → 保存 → 数据库更新，表格刷新
11. 删除发票 → 确认后删除，表格移除该行
12. 关闭已保存窗口后回到主界面，数据一致

- [ ] **Step 3: 修复发现的任何问题**

- [ ] **Step 4: 最终提交**

```bash
git add -A
git commit -m "feat: 已保存发票列表管理功能完成"
```

---

## 自审检查

### Spec 覆盖检查
| Spec 需求 | 覆盖任务 |
|-----------|----------|
| 用户管理已导出的发票列表 | Task 2, 3, 4 |
| 主界面中间显示未导出发票 | Task 6, 8 |
| 已导出发票自动转入已保存类别 | 已有逻辑 (IsConfirmed=true)，Task 6 修改查询 |
| 已保存列表按分类/时间/导出时间筛选 | Task 2, 3 |
| 浏览识别详情可自行修正再保存 | Task 4 |
| 移除"确认"功能 | Task 7, 8 |
| 主界面中间仅显示已确认 — 不要 | Task 6, 8 (移除开关和徽章) |
| 识别结果中确认标志 — 不要 | Task 8 (移除✅徽章) |

### 占位符扫描
无 TBD、TODO 或不完整部分 ✅

### 类型一致性
- `SavedInvoiceRow` 在所有文件中定义一致
- `IInvoiceService.GetByCreateDateRangeAsync` 和 `GetDistinctCategories` 在 Task 1 定义，Task 2 使用
- 服务接口在所有任务中一致 ✅

### 范围和焦点
每个任务聚焦单一功能，无无关重构 ✅
