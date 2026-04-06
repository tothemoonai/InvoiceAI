using System.ComponentModel;
using CommunityToolkit.Maui.Markup;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls;
using IOPath = System.IO.Path;

namespace InvoiceAI.App.Pages;

public class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly InvoiceDetailViewModel _detailVm;
    private readonly ImportViewModel _importVm;
    private readonly IServiceProvider _services;

    private CollectionView _invoiceList = null!;
    private VerticalStackLayout _detailContent = null!;
    private Border _importOverlay = null!;
    private Border _dropZone = null!;
    private ActivityIndicator _busyIndicator = null!;
    private Label _statusBar = null!;

    public MainPage(
        MainViewModel viewModel,
        InvoiceDetailViewModel detailViewModel,
        ImportViewModel importViewModel,
        IServiceProvider services)
    {
        _vm = viewModel;
        _detailVm = detailViewModel;
        _importVm = importViewModel;
        _services = services;

        BindingContext = viewModel;

        Title = "InvoiceAI";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
        WireEvents();

        _vm.LoadDataCommand.Execute(null);
    }

    // ─── UI Construction ──────────────────────────────────────

    private void BuildUI()
    {
        _busyIndicator = new ActivityIndicator
        {
            IsRunning = false,
            Color = Color.FromArgb("#1976D2"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Scale = 1.5
        };
        _busyIndicator.SetBinding(IsVisibleProperty, nameof(_vm.IsBusy));
        _busyIndicator.SetBinding(ActivityIndicator.IsRunningProperty, nameof(_vm.IsBusy));

        _statusBar = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            Padding = new Thickness(16, 4)
        };
        _statusBar.SetBinding(Label.TextProperty, nameof(_vm.StatusMessage));

        _importOverlay = BuildImportOverlay();
        _dropZone = BuildDropZone();

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(1, GridUnitType.Auto)),   // title bar
                new RowDefinition(new GridLength(1, GridUnitType.Star)),   // main content
                new RowDefinition(new GridLength(1, GridUnitType.Auto))    // status bar
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(180),
                new ColumnDefinition(280),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            },
            Children =
            {
                BuildTitleBar().Row(0).ColumnSpan(3),
                BuildCategoryPanel().Row(1).Column(0),
                BuildInvoiceListPanel().Row(1).Column(1),
                BuildDetailPanel().Row(1).Column(2),
                _statusBar.Row(2).ColumnSpan(3),
                _busyIndicator.Row(1).ColumnSpan(3),
                _importOverlay.Row(1).ColumnSpan(3),
                _dropZone.Row(1).ColumnSpan(3)
            }
        };
    }

    // ─── Title Bar ────────────────────────────────────────────

    private static View BuildTitleBar()
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb("#1976D2"),
            Padding = new Thickness(16, 8),
            Content = new HorizontalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "InvoiceAI",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White,
                        VerticalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "- 発票智能管理",
                        FontSize = 14,
                        TextColor = Color.FromArgb("#BBDEFB"),
                        VerticalOptions = LayoutOptions.Center
                    }
                }
            }
        };
    }

    // ─── Left Panel: Categories ───────────────────────────────

    private View BuildCategoryPanel()
    {
        var categoryList = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsSource = _vm.Categories,
            ItemTemplate = new DataTemplate(() =>
            {
                var nameLabel = new Label
                {
                    FontSize = 14,
                    VerticalOptions = LayoutOptions.Center
                };
                nameLabel.SetBinding(Label.TextProperty, ".");

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition(new GridLength(1, GridUnitType.Auto))
                    },
                    Padding = new Thickness(12, 8),
                    MinimumHeightRequest = 40,
                    Children = { nameLabel, new Label { FontSize = 12, TextColor = Color.FromArgb("#888") } }
                };

                Grid.SetColumn(nameLabel, 0);

                return grid;
            })
        };

        categoryList.SelectionChanged += OnCategorySelected;

        return new Border
        {
            BackgroundColor = Colors.White,
            Margin = new Thickness(0, 0, 1, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(1, GridUnitType.Star)),
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                    new RowDefinition(new GridLength(1, GridUnitType.Auto))
                },
                Children =
                {
                    new VerticalStackLayout { Children = { categoryList } }.Row(0),

                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#E0E0E0"),
                        Margin = new Thickness(8, 4)
                    }.Row(1),

                    new VerticalStackLayout
                    {
                        Padding = 8,
                        Spacing = 6,
                        Children =
                        {
                            BuildActionButton("📥 导入", OnImportClicked, Color.FromArgb("#1976D2")),
                            BuildActionButton("📤 导出", OnExportClicked, Color.FromArgb("#388E3C")),
                            BuildActionButton("⚙ 设置", OnSettingsClicked, Color.FromArgb("#757575"))
                        }
                    }.Row(2)
                }
            }
        };
    }

    // ─── Middle Panel: Invoice List ───────────────────────────

    private View BuildInvoiceListPanel()
    {
        _invoiceList = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsSource = _vm.Invoices,
            ItemTemplate = new DataTemplate(() =>
            {
                // Issuer name
                var issuerLabel = new Label
                {
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                issuerLabel.SetBinding(Label.TextProperty, nameof(Invoice.IssuerName));

                // Type badge label
                var typeLabel = new Label
                {
                    FontSize = 10,
                    TextColor = Colors.White
                };
                typeLabel.SetBinding(Label.TextProperty, nameof(Invoice.InvoiceType));

                var typeBadge = new Border
                {
                    Padding = new Thickness(6, 2),
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    StrokeThickness = 0,
                    BackgroundColor = Color.FromArgb("#4CAF50"),
                    HorizontalOptions = LayoutOptions.End,
                    Content = typeLabel
                };
                typeBadge.SetBinding(Border.BackgroundColorProperty, nameof(Invoice.InvoiceType));

                // Date
                var dateLabel = new Label
                {
                    FontSize = 12,
                    TextColor = Color.FromArgb("#666")
                };
                dateLabel.SetBinding(Label.TextProperty, nameof(Invoice.TransactionDate), stringFormat: "{0:yyyy-MM-dd}");

                // Amount
                var amountLabel = new Label
                {
                    FontSize = 12,
                    TextColor = Color.FromArgb("#1976D2"),
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.End
                };
                amountLabel.SetBinding(Label.TextProperty, nameof(Invoice.TaxIncludedAmount), stringFormat: "¥{0:N0}");

                // Category
                var catLabel = new Label
                {
                    FontSize = 11,
                    TextColor = Color.FromArgb("#999")
                };
                catLabel.SetBinding(Label.TextProperty, nameof(Invoice.Category));

                var content = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                        new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                        new RowDefinition(new GridLength(1, GridUnitType.Auto))
                    },
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(1, GridUnitType.Auto)),
                        new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                    },
                    Children =
                    {
                        issuerLabel.Row(0).Column(0),
                        typeBadge.Row(0).Column(1),
                        dateLabel.Row(1).Column(0),
                        amountLabel.Row(1).Column(1),
                        catLabel.Row(2).Column(0).ColumnSpan(2)
                    }
                };

                return new Border
                {
                    BackgroundColor = Colors.White,
                    Margin = new Thickness(8, 4),
                    Padding = new Thickness(12, 8),
                    StrokeShape = new RoundRectangle { CornerRadius = 6 },
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#E0E0E0"),
                    MinimumHeightRequest = 72,
                    Content = content
                };
            }),
            EmptyView = new VerticalStackLayout
            {
                Padding = 20,
                Children =
                {
                    new Label
                    {
                        Text = "暂无发票记录",
                        FontSize = 16,
                        TextColor = Color.FromArgb("#999"),
                        HorizontalOptions = LayoutOptions.Center
                    }
                }
            }
        };

        _invoiceList.SelectionChanged += OnInvoiceSelected;

        var searchBar = new SearchBar
        {
            Placeholder = "搜索发票...",
            Margin = new Thickness(8, 8, 8, 0)
        };
        searchBar.SetBinding(SearchBar.SearchCommandProperty, nameof(_vm.SearchCommand));
        searchBar.SetBinding(SearchBar.TextProperty, nameof(_vm.SearchText));

        var recentHeader = new Label
        {
            Text = "🔵 本次识别",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1976D2"),
            Margin = new Thickness(12, 8, 0, 2)
        };

        var recentList = new CollectionView
        {
            ItemsSource = _vm.RecentImports,
            ItemTemplate = new DataTemplate(() =>
            {
                var nameLbl = new Label
                {
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaximumWidthRequest = 180
                };
                nameLbl.SetBinding(Label.TextProperty, nameof(Invoice.IssuerName));

                var amtLbl = new Label
                {
                    FontSize = 12,
                    TextColor = Color.FromArgb("#1976D2")
                };
                amtLbl.SetBinding(Label.TextProperty, nameof(Invoice.TaxIncludedAmount), stringFormat: "¥{0:N0}");

                return new Border
                {
                    BackgroundColor = Color.FromArgb("#E3F2FD"),
                    Margin = new Thickness(8, 2),
                    Padding = new Thickness(10, 6),
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    StrokeThickness = 0,
                    Content = new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { nameLbl, amtLbl }
                    }
                };
            })
        };

        var historyHeader = new Label
        {
            Text = "历史记录",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(12, 8, 0, 2)
        };

        return new Border
        {
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new VerticalStackLayout
            {
                Children =
                {
                    searchBar,
                    recentHeader,
                    recentList,
                    historyHeader,
                    _invoiceList
                }
            }
        };
    }

    // ─── Right Panel: Detail ──────────────────────────────────

    private View BuildDetailPanel()
    {
        _detailContent = new VerticalStackLayout { Spacing = 4 };

        var statusLabel = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        statusLabel.SetBinding(Label.TextProperty, nameof(_detailVm.InvoiceTypeDisplay));
        statusLabel.SetBinding(Label.TextColorProperty, nameof(_detailVm.InvoiceTypeColor));

        var typeBadgeBorder = new Border
        {
            Padding = new Thickness(16, 10),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            HorizontalOptions = LayoutOptions.Fill,
            Content = statusLabel
        };
        typeBadgeBorder.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));
        typeBadgeBorder.SetBinding(Border.StrokeProperty, nameof(_detailVm.InvoiceTypeColor));

        // Items header
        var itemsHeader = new Label
        {
            Text = "明細項目",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 8, 0, 4)
        };
        itemsHeader.SetBinding(IsVisibleProperty, nameof(_detailVm.InvoiceItems));

        // Items CollectionView
        var itemsList = new CollectionView
        {
            ItemsSource = _detailVm.InvoiceItems,
            ItemTemplate = new DataTemplate(() =>
            {
                var nameLbl = new Label { FontSize = 13 };
                nameLbl.SetBinding(Label.TextProperty, nameof(InvoiceItem.Name));

                var rateLbl = new Label { FontSize = 13, TextColor = Color.FromArgb("#666") };
                rateLbl.SetBinding(Label.TextProperty, nameof(InvoiceItem.TaxRate), stringFormat: "{0}%");

                var amtLbl = new Label
                {
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.End
                };
                amtLbl.SetBinding(Label.TextProperty, nameof(InvoiceItem.Amount), stringFormat: "¥{0:N0}");

                return new Border
                {
                    BackgroundColor = Color.FromArgb("#F5F5F5"),
                    Margin = new Thickness(0, 2),
                    Padding = new Thickness(10, 6),
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    StrokeThickness = 0,
                    Content = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                            new ColumnDefinition(new GridLength(1, GridUnitType.Auto)),
                            new ColumnDefinition(new GridLength(1, GridUnitType.Auto))
                        },
                        Children =
                        {
                            nameLbl.Column(0),
                            rateLbl.Column(1),
                            amtLbl.Column(2)
                        }
                    }
                };
            })
        };
        itemsList.SetBinding(IsVisibleProperty, nameof(_detailVm.InvoiceItems));

        // Missing fields
        var missingText = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#E65100")
        };
        missingText.SetBinding(Label.TextProperty, nameof(_detailVm.MissingFieldsDisplay));

        var missingBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            Padding = new Thickness(12, 8),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#FF9800"),
            Content = new HorizontalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = "⚠", FontSize = 14 },
                    missingText
                }
            }
        };
        missingBorder.SetBinding(IsVisibleProperty, nameof(_detailVm.MissingFieldsDisplay));

        // Action buttons
        var actions = new HorizontalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
            Children =
            {
                BuildActionButton("💾 保存确认", OnSaveClicked, Color.FromArgb("#4CAF50")),
                BuildActionButton("🗑 删除", OnDeleteClicked, Color.FromArgb("#F44336"))
            }
        };
        actions.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));

        // Detail ScrollView (visible when invoice selected)
        var detailWrapper = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children =
                {
                    typeBadgeBorder,
                    _detailContent,
                    itemsHeader,
                    itemsList,
                    missingBorder,
                    actions
                }
            }
        };
        detailWrapper.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));

        // Empty state (visible when no invoice selected)
        var emptyState = new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "← 选择一张发票查看详情",
                    FontSize = 16,
                    TextColor = Color.FromArgb("#BBB")
                }
            }
        };
        // Show when CurrentInvoice is null — invert binding
        emptyState.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));
        // We handle inversion in code-behind

        return new Border
        {
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new Grid
            {
                Children = { emptyState, detailWrapper }
            }
        };
    }

    // ─── Import Overlay ───────────────────────────────────────

    private Border BuildImportOverlay()
    {
        var fileList = new CollectionView
        {
            ItemsSource = _importVm.ImportItems,
            ItemTemplate = new DataTemplate(() =>
            {
                var nameLbl = new Label { FontSize = 13 };
                nameLbl.SetBinding(Label.TextProperty, nameof(ImportItem.FileName));

                var statusLbl = new Label { FontSize = 13 };
                statusLbl.SetBinding(Label.TextProperty, nameof(ImportItem.Status));

                return new HorizontalStackLayout
                {
                    Spacing = 8,
                    Padding = new Thickness(4, 2),
                    Children = { nameLbl, statusLbl }
                };
            })
        };

        var cancelBtn = new Button
        {
            Text = "取消",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            WidthRequest = 100,
            HorizontalOptions = LayoutOptions.Center
        };
        cancelBtn.SetBinding(Button.CommandProperty, nameof(_importVm.CancelCommand));

        var overlay = new Border
        {
            BackgroundColor = Color.FromArgb("#CC000000"),
            IsVisible = false,
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Padding = 32,
                Spacing = 12,
                Children =
                {
                    new ActivityIndicator
                    {
                        IsRunning = true,
                        Color = Colors.White,
                        Scale = 1.5
                    },
                    new Label
                    {
                        TextColor = Colors.White,
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalOptions = LayoutOptions.Center
                    }
                    .Bind(Label.TextProperty, nameof(_importVm.StatusMessage)),
                    new ProgressBar
                    {
                        WidthRequest = 300,
                        ProgressColor = Color.FromArgb("#1976D2")
                    }
                    .Bind(ProgressBar.ProgressProperty, nameof(_importVm.Progress)),
                    fileList,
                    cancelBtn
                }
            }
        };
        overlay.SetBinding(IsVisibleProperty, nameof(_importVm.IsProcessing));

        return overlay;
    }

    // ─── Drop Zone (visual overlay for OS file drag-drop) ───

    private Border BuildDropZone()
    {
        return new Border
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#33000000"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 3,
            Stroke = Color.FromArgb("#1976D2"),
            Margin = new Thickness(8),
            Content = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "📥",
                        FontSize = 48,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "拖拽发票文件到此处",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "支持 JPG、PNG、PDF 格式",
                        FontSize = 14,
                        TextColor = Color.FromArgb("#CCCCCC"),
                        HorizontalOptions = LayoutOptions.Center
                    }
                }
            }
        };
    }

    public void ShowDropZone(bool visible)
    {
        MainThread.BeginInvokeOnMainThread(() => _dropZone.IsVisible = visible);
    }

    // ─── Helper: Detail Row ───────────────────────────────────

    private void RefreshDetailContent()
    {
        _detailContent.Children.Clear();

        var invoice = _detailVm.CurrentInvoice;
        if (invoice == null) return;

        _detailContent.Children.Add(BuildDetailRow("発行事業者", invoice.IssuerName));
        _detailContent.Children.Add(BuildDetailRow("登録番号", invoice.RegistrationNumber));

        if (invoice.TransactionDate.HasValue)
            _detailContent.Children.Add(BuildDetailRow("取引年月日", invoice.TransactionDate.Value.ToString("yyyy-MM-dd")));

        _detailContent.Children.Add(BuildDetailRow("内容", invoice.Description));
        _detailContent.Children.Add(BuildDetailRow("分类", invoice.Category));

        if (invoice.TaxExcludedAmount.HasValue)
            _detailContent.Children.Add(BuildDetailRow("税抜金額", $"¥{invoice.TaxExcludedAmount.Value:N0}"));

        if (invoice.TaxIncludedAmount.HasValue)
            _detailContent.Children.Add(BuildDetailRow("税込金額", $"¥{invoice.TaxIncludedAmount.Value:N0}"));

        if (invoice.TaxAmount.HasValue)
            _detailContent.Children.Add(BuildDetailRow("消費税額", $"¥{invoice.TaxAmount.Value:N0}"));

        if (!string.IsNullOrEmpty(invoice.RecipientName))
            _detailContent.Children.Add(BuildDetailRow("交付先", invoice.RecipientName));
    }

    private static Border BuildDetailRow(string label, string value)
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            Padding = new Thickness(12, 6),
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E8E8E8"),
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(120),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                },
                Children =
                {
                    new Label
                    {
                        Text = label,
                        FontSize = 13,
                        TextColor = Color.FromArgb("#666"),
                        VerticalOptions = LayoutOptions.Center
                    }.Column(0),
                    new Label
                    {
                        Text = value,
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        VerticalOptions = LayoutOptions.Center
                    }.Column(1)
                }
            }
        };
    }

    // ─── Helper: Action Button ────────────────────────────────

    private static Button BuildActionButton(string text, EventHandler onClick, Color bgColor)
    {
        var btn = new Button
        {
            Text = text,
            BackgroundColor = bgColor,
            TextColor = Colors.White,
            FontSize = 13,
            Padding = new Thickness(12, 6),
            MinimumHeightRequest = 36
        };
        btn.Clicked += onClick;
        return btn;
    }

    // ─── Event Handlers ───────────────────────────────────────

    private void WireEvents()
    {
        _detailVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_detailVm.CurrentInvoice))
                MainThread.BeginInvokeOnMainThread(RefreshDetailContent);
        };

        _importVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_importVm.IsProcessing) && !_importVm.IsProcessing)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await _vm.LoadDataCommand.ExecuteAsync(null);
                });
            }
        };
    }

    private void OnCategorySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is string category)
        {
            _vm.FilterByCategoryCommand.Execute(category);
        }
    }

    private void OnInvoiceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Invoice invoice)
        {
            _vm.SelectedInvoice = invoice;
            _detailVm.CurrentInvoice = invoice;
        }
        else
        {
            _vm.SelectedInvoice = null;
            _detailVm.CurrentInvoice = null;
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "选择发票文件",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".pdf" } }
                    })
            });

            if (result != null)
            {
                await _importVm.ProcessFilesCommand.ExecuteAsync(new[] { result.FullPath });
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlert("导入错误", ex.Message, "OK");
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            var filePath = IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"InvoiceAI_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            await _vm.ExportCommand.ExecuteAsync(filePath);
            await this.DisplayAlert("导出完成", $"已导出到:\n{filePath}", "OK");
        }
        catch (Exception ex)
        {
            await this.DisplayAlert("导出错误", ex.Message, "OK");
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settingsPage = _services.GetRequiredService<Pages.SettingsPage>();
        await Navigation.PushAsync(settingsPage);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        await _detailVm.SaveCommand.ExecuteAsync(null);
        await this.DisplayAlert("保存", "发票已确认保存", "OK");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_vm.SelectedInvoice == null) return;

        var confirm = await this.DisplayAlert("确认删除",
            $"确定要删除 {_vm.SelectedInvoice.IssuerName} 的发票吗？", "删除", "取消");
        if (confirm)
        {
            await _vm.DeleteInvoiceCommand.ExecuteAsync(_vm.SelectedInvoice);
            _detailVm.CurrentInvoice = null;
        }
    }
}
