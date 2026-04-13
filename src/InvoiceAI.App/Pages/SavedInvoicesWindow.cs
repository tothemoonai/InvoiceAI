using CommunityToolkit.Maui.Markup;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;
using InvoiceAI.App.Utils;
using Microsoft.Maui.Controls.Shapes;

namespace InvoiceAI.App.Pages;

public class SavedInvoicesWindow : ContentPage
{
    private readonly SavedInvoicesViewModel _vm;
    private readonly IInvoiceService _invoiceService;
    private readonly IAppSettingsService _settingsService;
    private CollectionView _invoiceTable = null!;
    private Picker _categoryPicker = null!;
    private DatePicker _startDatePicker = null!;
    private DatePicker _endDatePicker = null!;
    private Label _statusLabel = null!;

    public SavedInvoicesWindow(SavedInvoicesViewModel viewModel, IInvoiceService invoiceService, IAppSettingsService settingsService)
    {
        _vm = viewModel;
        _invoiceService = invoiceService;
        _settingsService = settingsService;
        BindingContext = viewModel;

        Title = "已导出发票列表";
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
                new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(1, GridUnitType.Auto))
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
        _categoryPicker = new Picker { WidthRequest = 120, VerticalOptions = LayoutOptions.Center };
        _categoryPicker.SetBinding(Picker.ItemsSourceProperty, nameof(_vm.Categories));
        _categoryPicker.SetBinding(Picker.SelectedItemProperty, nameof(_vm.SelectedCategory));

        _startDatePicker = new DatePicker { WidthRequest = 130, VerticalOptions = LayoutOptions.Center, Format = "yyyy-MM-dd" };
        _startDatePicker.SetBinding(DatePicker.DateProperty, nameof(_vm.FilterStartDate));

        _endDatePicker = new DatePicker { WidthRequest = 130, VerticalOptions = LayoutOptions.Center, Format = "yyyy-MM-dd" };
        _endDatePicker.SetBinding(DatePicker.DateProperty, nameof(_vm.FilterEndDate));

        var sortPicker = new Picker
        {
            WidthRequest = 120,
            VerticalOptions = LayoutOptions.Center,
            ItemsSource = new List<string> { "交易日期", "导出时间" }
        };
        sortPicker.SelectedIndexChanged += (s, e) =>
        {
            if (sortPicker.SelectedIndex >= 0)
            {
                _vm.SortMode = sortPicker.SelectedIndex == 1 ? "CreatedAt" : "TransactionDate";
                _vm.ApplyFiltersCommand.Execute(null);
            }
        };

        var refreshBtn = new Button { Text = "🔄 刷新", FontSize = 12, WidthRequest = 80, HeightRequest = 32 };
        refreshBtn.SetBinding(Button.CommandProperty, nameof(_vm.LoadDataCommand));

        var clearFilterBtn = new Button
        {
            Text = "✕ 清除筛选", FontSize = 12, WidthRequest = 90, HeightRequest = 32,
            BackgroundColor = ThemeManager.TextSecondary
        };
        clearFilterBtn.Clicked += (s, e) =>
        {
            _categoryPicker.SelectedItem = "全部";
            _vm.FilterStartDate = null;
            _vm.FilterEndDate = null;
            _startDatePicker.Date = DateTime.Now.AddMonths(-1);
            _endDatePicker.Date = DateTime.Now;
        };

        return new Border
        {
            BackgroundColor = ThemeManager.CardBackground,
            Padding = new Thickness(12, 8),
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = "分类:", FontSize = 12, VerticalOptions = LayoutOptions.Center },
                    _categoryPicker,
                    new Label { Text = "开始:", FontSize = 12, VerticalOptions = LayoutOptions.Center },
                    _startDatePicker,
                    new Label { Text = "结束:", FontSize = 12, VerticalOptions = LayoutOptions.Center },
                    _endDatePicker,
                    new Label { Text = "排序:", FontSize = 12, VerticalOptions = LayoutOptions.Center },
                    sortPicker,
                    refreshBtn,
                    clearFilterBtn
                }
            }
        };
    }

    private View BuildTable()
    {
        var header = BuildTableHeader();

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
                    new Label { Text = "暂无已导出的发票记录", FontSize = 16, TextColor = ThemeManager.TextSecondary }
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
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                    new RowDefinition(new GridLength(1, GridUnitType.Star))
                },
                Children = { header.Row(0), _invoiceTable.Row(1) }
            }
        };
    }

    private static View BuildTableHeader()
    {
        var lblStyle = new Style(typeof(Label))
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
                    new Label { Text = "日期", Style = lblStyle }.Column(0),
                    new Label { Text = "发行方", Style = lblStyle }.Column(1),
                    new Label { Text = "登録番号", Style = lblStyle }.Column(2),
                    new Label { Text = "内容", Style = lblStyle }.Column(3),
                    new Label { Text = "分类", Style = lblStyle }.Column(4),
                    new Label { Text = "税抜金額", Style = lblStyle, HorizontalOptions = LayoutOptions.End }.Column(5),
                    new Label { Text = "税込金額", Style = lblStyle, HorizontalOptions = LayoutOptions.End }.Column(6),
                    new Label { Text = "类型", Style = lblStyle }.Column(7),
                    new Label { Text = "创建时间", Style = lblStyle }.Column(8)
                }
            }
        };
    }

    private static View BuildTableRow()
    {
        var dateLbl = new Label { FontSize = 12, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
        dateLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.TransactionDate), stringFormat: "{0:yyyy-MM-dd}");

        var issuerLbl = new Label { FontSize = 12, TextColor = ThemeManager.TextPrimary, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
        issuerLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.IssuerName));

        var regLbl = new Label { FontSize = 11, TextColor = ThemeManager.TextTertiary, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
        regLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.RegistrationNumber));

        var descLbl = new Label { FontSize = 12, TextColor = ThemeManager.TextPrimary, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
        descLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.Description));

        var catLbl = new Label { FontSize = 11, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center };
        catLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.Category));

        var exclLbl = new Label { FontSize = 12, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
        exclLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.TaxExcludedAmount), stringFormat: "¥{0:N0}");

        var inclLbl = new Label { FontSize = 12, TextColor = ThemeManager.BrandPrimary, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
        inclLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.TaxIncludedAmount), stringFormat: "¥{0:N0}");

        var typeLbl = new Label { FontSize = 11, TextColor = ThemeManager.TextSecondary, VerticalOptions = LayoutOptions.Center };
        typeLbl.SetBinding(Label.TextProperty, nameof(SavedInvoiceRow.InvoiceTypeDisplay));

        var createdLbl = new Label { FontSize = 11, TextColor = ThemeManager.TextTertiary, VerticalOptions = LayoutOptions.Center };
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
                    new ColumnDefinition(100), new ColumnDefinition(150), new ColumnDefinition(120),
                    new ColumnDefinition(200), new ColumnDefinition(80), new ColumnDefinition(100),
                    new ColumnDefinition(100), new ColumnDefinition(80), new ColumnDefinition(140)
                },
                Children =
                {
                    dateLbl.Column(0), issuerLbl.Column(1), regLbl.Column(2), descLbl.Column(3),
                    catLbl.Column(4), exclLbl.Column(5), inclLbl.Column(6), typeLbl.Column(7), createdLbl.Column(8)
                }
            }
        };

        VisualStateManager.SetVisualStateGroups(row, new VisualStateGroupList
        {
            new VisualStateGroup
            {
                Name = "CommonStates",
                States =
                {
                    new VisualState { Name = "Normal", Setters = { new Setter { Property = Border.BackgroundColorProperty, Value = ThemeManager.CardBackground } } },
                    new VisualState { Name = "Selected", Setters = { new Setter { Property = Border.BackgroundColorProperty, Value = Color.FromArgb("#BBDEFB") } } }
                }
            }
        });

        return row;
    }

    private void WireEvents()
    {
        _categoryPicker.SelectedIndexChanged += (s, e) =>
        {
            if (_categoryPicker.SelectedItem is string cat)
                _vm.SelectedCategory = cat;
        };
    }

    private async void OnInvoiceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is SavedInvoiceRow row)
        {
            _invoiceTable.SelectedItem = null;
            await OpenDetailDialog(row);
        }
    }

    private async Task OpenDetailDialog(SavedInvoiceRow row)
    {
        var dialog = new SavedInvoiceDetailDialog(row.Id, _vm, _invoiceService, _settingsService);
        await Navigation.PushModalAsync(dialog);
    }
}
