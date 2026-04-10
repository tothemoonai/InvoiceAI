using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Maui.Markup;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models;
using InvoiceAI.App.Utils;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Controls;
using IOPath = System.IO.Path;

namespace InvoiceAI.App.Pages;

public class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly InvoiceDetailViewModel _detailVm;
    private readonly ImportViewModel _importVm;
    private readonly IAppSettingsService _settingsService;
    private readonly IServiceProvider _services;

    private CollectionView _invoiceList = null!;
    private VerticalStackLayout _detailContent = null!;
    private Image _invoicePreviewImage = null!;
    private Border _importOverlay = null!;
    private ActivityIndicator _busyIndicator = null!;
    private Label _statusBar = null!;
    private Label _importStatusLabel = null!;
    private ProgressBar _importProgressBar = null!;

    private LayoutMode _layoutMode = LayoutMode.Standard;
    private enum LayoutMode { Expanded, Standard }

    public MainPage(
        MainViewModel viewModel,
        InvoiceDetailViewModel detailViewModel,
        ImportViewModel importViewModel,
        IAppSettingsService settingsService,
        IServiceProvider services)
    {
        _vm = viewModel;
        _detailVm = detailViewModel;
        _importVm = importViewModel;
        _settingsService = settingsService;
        _services = services;

        BindingContext = viewModel;

        BackgroundColor = ThemeManager.Background;

        BuildUI();
        WireEvents();

        _vm.LoadDataCommand.Execute(null);  // fire-and-forget, UI will update when data arrives
    }

    // ─── UI Construction ──────────────────────────────────────

    private void BuildUI()
    {
        _busyIndicator = new ActivityIndicator
        {
            IsRunning = false,
            Color = ThemeManager.BrandPrimary,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Scale = 1.5
        };
        _busyIndicator.SetBinding(IsVisibleProperty, nameof(_vm.IsBusy));
        _busyIndicator.SetBinding(ActivityIndicator.IsRunningProperty, nameof(_vm.IsBusy));

        _statusBar = new Label
        {
            FontSize = 12,
            TextColor = ThemeManager.TextSecondary,
            Padding = new Thickness(16, 4)
        };
        _statusBar.SetBinding(Label.TextProperty, nameof(_vm.StatusMessage));

        _importOverlay = BuildImportOverlay();

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
                new ColumnDefinition(150),  // Standard 默认
                new ColumnDefinition(250),
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
                _importOverlay.Row(1).ColumnSpan(3)
            }
        };
    }

    // ─── Title Bar ────────────────────────────────────────────

    private static View BuildTitleBar()
    {
        return new Border
        {
            BackgroundColor = ThemeManager.BrandPrimary,
            Padding = new Thickness(16, 8),
            Content = new HorizontalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    new Label
                    {
                        Text = "InvoiceAI - 発票智能管理",
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
                    Children = { nameLabel, new Label { FontSize = 12, TextColor = ThemeManager.TextTertiary } }
                };

                Grid.SetColumn(nameLabel, 0);

                return grid;
            })
        };

        categoryList.SelectionChanged += OnCategorySelected;

        return new Border
        {
            BackgroundColor = ThemeManager.CardBackground,
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
                        BackgroundColor = ThemeManager.BorderLight,
                        Margin = new Thickness(8, 4)
                    }.Row(1),

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
                    BackgroundColor = ThemeManager.Success,
                    HorizontalOptions = LayoutOptions.End,
                    Content = typeLabel
                };
                typeBadge.SetBinding(Border.BackgroundColorProperty, nameof(Invoice.InvoiceType));

                // Date + Amount (on the same line)
                var dateLabel = new Label
                {
                    FontSize = 12,
                    TextColor = ThemeManager.TextSecondary,
                    VerticalTextAlignment = TextAlignment.Center
                };
                dateLabel.SetBinding(Label.TextProperty, nameof(Invoice.TransactionDate), stringFormat: "{0:yyyy-MM-dd}");

                var amountLabel = new Label
                {
                    FontSize = 12,
                    TextColor = ThemeManager.BrandPrimary,
                    FontAttributes = FontAttributes.Bold,
                    VerticalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.NoWrap,
                    MaxLines = 1
                };
                amountLabel.SetBinding(Label.TextProperty, nameof(Invoice.TaxIncludedAmount), stringFormat: " ¥{0:N0}");

                // Category
                var catLabel = new Label
                {
                    FontSize = 11,
                    TextColor = ThemeManager.TextTertiary
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
                        new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                        new ColumnDefinition(new GridLength(1, GridUnitType.Auto))
                    },
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
                };

                var border = new Border
                {
                    BackgroundColor = ThemeManager.CardBackground,
                    Margin = new Thickness(8, 4),
                    Padding = new Thickness(12, 8),
                    StrokeShape = new RoundRectangle { CornerRadius = 6 },
                    StrokeThickness = 2,
                    Stroke = ThemeManager.BorderLight,
                    MinimumHeightRequest = 72,
                    Content = content
                };

                // Visual state for selected items — make it obvious
                VisualStateManager.SetVisualStateGroups(border, new VisualStateGroupList
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
                                    new Setter { Property = Border.BackgroundColorProperty, Value = ThemeManager.CardBackground },
                                    new Setter { Property = Border.StrokeProperty, Value = ThemeManager.BorderLight }
                                }
                            },
                            new VisualState
                            {
                                Name = "Selected",
                                Setters =
                                {
                                    new Setter { Property = Border.BackgroundColorProperty, Value = Color.FromArgb("#BBDEFB") },
                                    new Setter { Property = Border.StrokeProperty, Value = ThemeManager.BrandPrimary }
                                }
                            }
                        }
                    }
                });

                return border;
            }),
            EmptyView = BuildEmptyState(),
        };

        _invoiceList.SelectionChanged += OnInvoiceSelected;

        var searchBar = new SearchBar
        {
            Placeholder = "搜索发票...",
            Margin = new Thickness(8, 8, 8, 0)
        };
        searchBar.SetBinding(SearchBar.SearchCommandProperty, nameof(_vm.SearchCommand));
        searchBar.SetBinding(SearchBar.TextProperty, nameof(_vm.SearchText));

        // Filter row (empty, confirmed switch removed)
        var filterRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(8, 4, 8, 0),
            HorizontalOptions = LayoutOptions.End
        };

        // Saved invoices header
        var savedHeader = new Label
        {
            Text = "📋 未导出发票",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeManager.BrandPrimary,
            Margin = new Thickness(12, 8, 0, 2)
        };

        return new Border
        {
            BackgroundColor = ThemeManager.Get("BackgroundTertiary", "DarkBackgroundTertiary"),
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),   // search
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),   // filter
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),   // header
                    new RowDefinition(new GridLength(1, GridUnitType.Star))    // invoice list (scrollable)
                },
                Children =
                {
                    searchBar.Row(0),
                    filterRow.Row(1),
                    savedHeader.Row(2),
                    _invoiceList.Row(3)
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

        // Image preview
        _invoicePreviewImage = new Image
        {
            Aspect = Aspect.AspectFit,
            HeightRequest = 180,
            BackgroundColor = ThemeManager.Get("BackgroundTertiary", "DarkBackgroundTertiary"),
            HorizontalOptions = LayoutOptions.Fill,
            IsVisible = false
        };
        _invoicePreviewImage.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await ShowFullImagePreview())
        });

        var previewBorder = new Border
        {
            Padding = new Thickness(4),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            BackgroundColor = ThemeManager.CardBackground,
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                    new RowDefinition(new GridLength(1, GridUnitType.Star))
                },
                Children =
                {
                    new Label
                    {
                        Text = "📷 发票图片（点击放大）",
                        FontSize = 11,
                        TextColor = ThemeManager.TextTertiary,
                        Margin = new Thickness(4, 2)
                    }.Row(0),
                    _invoicePreviewImage.Row(1)
                }
            }
        };
        previewBorder.SetBinding(IsVisibleProperty, nameof(_invoicePreviewImage.IsVisible));

        // Items header
        var itemsHeader = new Label
        {
            Text = "明細項目",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeManager.TextPrimary,
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

                var rateLbl = new Label { FontSize = 13, TextColor = ThemeManager.TextSecondary };
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
                    BackgroundColor = ThemeManager.Background,
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
            TextColor = ThemeManager.Error
        };
        missingText.SetBinding(Label.TextProperty, nameof(_detailVm.MissingFieldsDisplay));

        var missingBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            Padding = new Thickness(12, 8),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            StrokeThickness = 1,
            Stroke = ThemeManager.Warning,
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

        // Detail ScrollView (visible when invoice selected, scrollable content below fixed buttons)
        var detailWrapper = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 0, 16, 16),
                Spacing = 12,
                Children =
                {
                    typeBadgeBorder,
                    previewBorder,
                    _detailContent,
                    itemsHeader,
                    itemsList,
                    missingBorder
                }
            }
        };
        detailWrapper.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));

        // Wrapper with fixed buttons at top and scrollable content below
        var detailContainer = new VerticalStackLayout
        {
            Children =
            {
                actions,
                detailWrapper
            }
        };
        detailContainer.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));

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
                    TextColor = ThemeManager.TextTertiary
                }
            }
        };
        // Show when CurrentInvoice is null — invert binding
        emptyState.SetBinding(IsVisibleProperty, nameof(_detailVm.CurrentInvoice));
        // We handle inversion in code-behind

        return new Border
        {
            BackgroundColor = ThemeManager.CardBackground,
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            StrokeThickness = 0,
            Content = new Grid
            {
                Children = { emptyState, detailContainer }
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

        _importStatusLabel = new Label
        {
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };

        _importProgressBar = new ProgressBar
        {
            WidthRequest = 300,
            ProgressColor = ThemeManager.BrandPrimary
        };

        var cancelBtn = new Button
        {
            Text = "取消",
            BackgroundColor = ThemeManager.Error,
            TextColor = Colors.White,
            WidthRequest = 100,
            HorizontalOptions = LayoutOptions.Center,
            Command = _importVm.CancelCommand
        };

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
                    _importStatusLabel,
                    _importProgressBar,
                    fileList,
                    cancelBtn
                }
            }
        };

        return overlay;
    }

    // ─── Helper: Detail Row ───────────────────────────────────

    private void RefreshDetailContent()
    {
        _detailContent.Children.Clear();

        var invoice = _detailVm.CurrentInvoice;
        if (invoice == null)
        {
            _invoicePreviewImage.IsVisible = false;
            return;
        }

        // Load invoice image preview
        LoadInvoiceImagePreview(invoice);

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

    private void LoadInvoiceImagePreview(Invoice invoice)
    {
        var imagePath = FindInvoiceImagePath(invoice);
        System.Diagnostics.Debug.WriteLine($"[IMG] FindInvoiceImagePath returned: {imagePath ?? "null"}");
        if (imagePath != null)
            System.Diagnostics.Debug.WriteLine($"[IMG] File.Exists: {File.Exists(imagePath)}");
        
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[IMG] Setting image source: {imagePath}");
                _invoicePreviewImage.Source = ImageSource.FromFile(imagePath);
                _invoicePreviewImage.IsVisible = true;
                System.Diagnostics.Debug.WriteLine($"[IMG] IsVisible set to true");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IMG] Exception: {ex.Message}");
                _invoicePreviewImage.IsVisible = false;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[IMG] No valid image path found");
            _invoicePreviewImage.IsVisible = false;
        }
    }

    private string? FindInvoiceImagePath(Invoice invoice)
    {
        // 1. Check if SourceFilePath exists and is an image
        if (!string.IsNullOrEmpty(invoice.SourceFilePath) &&
            File.Exists(invoice.SourceFilePath) &&
            IsImageFile(invoice.SourceFilePath))
        {
            return invoice.SourceFilePath;
        }

        // 2. Check archive path if configured
        var archivePath = _settingsService.Settings.InvoiceArchivePath;
        if (!string.IsNullOrWhiteSpace(archivePath) && Directory.Exists(archivePath))
        {
            var categoryDir = IOPath.Combine(archivePath, invoice.Category ?? "未分类");
            if (Directory.Exists(categoryDir))
            {
                var matches = Directory.GetFiles(categoryDir, $"*{invoice.IssuerName}*");
                if (matches.Length > 0)
                    return matches[0]; // Return first match
            }
        }

        // 3. Check TEMP OCR directory
        if (!string.IsNullOrEmpty(invoice.SourceFilePath))
        {
            var tempOcrDir = IOPath.Combine(System.IO.Path.GetTempPath(), "InvoiceAI", "ocr");
            if (Directory.Exists(tempOcrDir))
            {
                var fileName = IOPath.GetFileNameWithoutExtension(invoice.SourceFilePath);
                var matches = Directory.GetFiles(tempOcrDir, $"{fileName}*");
                if (matches.Length > 0 && IsImageFile(matches[0]))
                    return matches[0];
            }
        }

        return null;
    }

    private static bool IsImageFile(string path)
    {
        var ext = IOPath.GetExtension(path).ToLowerInvariant();
        return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif" || ext == ".webp";
    }

    private async Task ShowFullImagePreview()
    {
        var invoice = _detailVm.CurrentInvoice;
        if (invoice == null) return;

        var imagePath = FindInvoiceImagePath(invoice);
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            await this.DisplayAlert("提示", "发票图片不存在或无法找到", "OK");
            return;
        }

        var fullImage = new Image
        {
            Source = ImageSource.FromFile(imagePath),
            Aspect = Aspect.AspectFit,
            BackgroundColor = Colors.Black
        };

        var closeBtn = new Button
        {
            Text = "✕ 关闭",
            BackgroundColor = ThemeManager.TextPrimary,
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 8, 16, 0),
            WidthRequest = 120,
            MinimumHeightRequest = 40
        };

        var infoLabel = new Label
        {
            Text = $"📷 {IOPath.GetFileName(imagePath)}",
            FontSize = 12,
            TextColor = ThemeManager.TextSecondary,
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(16, 8, 0, 0)
        };

        var popupPage = new ContentPage
        {
            Title = "发票预览",
            BackgroundColor = Colors.Black,
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                    new RowDefinition(new GridLength(1, GridUnitType.Star))
                },
                Children =
                {
                    new Grid
                    {
                        BackgroundColor = ThemeManager.Get("DarkBackgroundTertiary", "DarkBackgroundTertiary"),
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                            new ColumnDefinition(new GridLength(1, GridUnitType.Auto))
                        },
                        Children =
                        {
                            infoLabel.Column(0),
                            closeBtn.Column(1)
                        }
                    }.Row(0),
                    fullImage.Row(1)
                }
            }
        };

        closeBtn.Clicked += async (s, e) => await popupPage.Navigation.PopModalAsync();

        await Navigation.PushModalAsync(popupPage);
    }

    private static Border BuildDetailRow(string label, string value)
    {
        return new Border
        {
            BackgroundColor = ThemeManager.CardBackground,
            Padding = new Thickness(12, 6),
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
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
                        TextColor = ThemeManager.TextSecondary,
                        VerticalOptions = LayoutOptions.Center
                    }.Column(0),
                    new Label
                    {
                        Text = value,
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        LineBreakMode = LineBreakMode.NoWrap,
                        MaxLines = 1,
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_importVm.IsProcessing):
                        // 仅在处理完成后隐藏覆盖层
                        if (!_importVm.IsProcessing)
                        {
                            _importOverlay.IsVisible = false;
                            _ = _vm.LoadDataCommand.ExecuteAsync(null);
                        }
                        break;
                    case nameof(_importVm.StatusMessage):
                        _importStatusLabel.Text = _importVm.StatusMessage;
                        break;
                    case nameof(_importVm.Progress):
                        _importProgressBar.Progress = _importVm.Progress / 100.0;
                        break;
                }
            });
        };
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        var newMode = width > 1200 ? LayoutMode.Expanded : LayoutMode.Standard;

        if (newMode != _layoutMode)
        {
            _layoutMode = newMode;
            MainThread.BeginInvokeOnMainThread(ApplyLayoutMode);
        }
    }

    private void ApplyLayoutMode()
    {
        if (Content is not Grid grid) return;

        switch (_layoutMode)
        {
            case LayoutMode.Expanded:
                grid.ColumnDefinitions[0] = new ColumnDefinition(180);
                grid.ColumnDefinitions[1] = new ColumnDefinition(320);
                grid.ColumnDefinitions[2] = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
                break;

            case LayoutMode.Standard:
                grid.ColumnDefinitions[0] = new ColumnDefinition(150);
                grid.ColumnDefinitions[1] = new ColumnDefinition(250);
                grid.ColumnDefinitions[2] = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
                break;
        }
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
        // Update selected invoices for multi-select
        _vm.SelectedInvoices.Clear();
        foreach (var item in e.CurrentSelection)
        {
            if (item is Invoice inv)
                _vm.SelectedInvoices.Add(inv);
        }

        // Update detail panel based on last selected or single selection
        if (e.CurrentSelection.LastOrDefault() is Invoice lastInvoice)
        {
            _vm.SelectedInvoice = lastInvoice;
            _detailVm.CurrentInvoice = lastInvoice;
        }
        else if (e.CurrentSelection.Count == 0)
        {
            _vm.SelectedInvoice = null;
            _detailVm.CurrentInvoice = null;
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var filePaths = await PickFilesAsync("选择发票文件");
            if (filePaths != null && filePaths.Length > 0)
            {
                // 手动显示导入覆盖层
                _importOverlay.IsVisible = true;
                await _importVm.ProcessFilesCommand.ExecuteAsync(filePaths);
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlert("导入错误", ex.Message, "OK");
            _importOverlay.IsVisible = false;
        }
    }

#if WINDOWS
    private async Task<string[]?> PickFilesAsync(string title)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".pdf");

        var win = this.Window;
        var platformWnd = win.Handler?.PlatformView;
        if (platformWnd is not Microsoft.UI.Xaml.Window xamlWindow)
            return null;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(xamlWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var files = await picker.PickMultipleFilesAsync();
        return files?.Select(f => f.Path).ToArray();
    }
#else
    private async Task<string[]?> PickFilesAsync(string title)
    {
        var results = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = title });
        return results?.Select(f => f.FullPath).ToArray();
    }
#endif

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await DisplayDateRangeDialog();
            if (!result.HasValue) return;

            var (startDate, endDate, confirmedFilter) = result.Value;

            var configuredPath = _settingsService.Settings.ExportPath;
            string filePath;
            if (!string.IsNullOrWhiteSpace(configuredPath) && Directory.Exists(configuredPath))
            {
                filePath = IOPath.Combine(configuredPath, $"InvoiceAI_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            else
            {
                var folder = await PickFolderAsync();
                if (string.IsNullOrEmpty(folder)) return;
                filePath = IOPath.Combine(folder, $"InvoiceAI_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }

            await _vm.ExportCommand.ExecuteAsync((filePath, startDate, endDate, confirmedFilter));
            await this.DisplayAlert("导出完成", $"已导出到:\n{filePath}", "OK");
        }
        catch (Exception ex)
        {
            await this.DisplayAlert("导出错误", ex.Message, "OK");
        }
    }

#if WINDOWS
    private async Task<string?> PickFolderAsync()
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        folderPicker.FileTypeFilter.Add("*");

        var win = this.Window;
        var platformWnd = win.Handler?.PlatformView;
        if (platformWnd is not Microsoft.UI.Xaml.Window xamlWindow) return null;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(xamlWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        return folder?.Path;
    }
#else
    private async Task<string?> PickFolderAsync()
    {
        await this.DisplayAlert("提示", "请手动输入导出文件夹路径", "OK");
        return null;
    }
#endif

    private async Task<(DateTime? Start, DateTime? End, int ConfirmedFilter)?> DisplayDateRangeDialog()
    {
        var tcs = new TaskCompletionSource<(DateTime? Start, DateTime? End, int ConfirmedFilter)?>();

        var startPicker = new DatePicker
        {
            Date = DateTime.Now.AddMonths(-1),
            Format = "yyyy-MM-dd",
            Margin = new Thickness(0, 4)
        };

        var endPicker = new DatePicker
        {
            Date = DateTime.Now,
            Format = "yyyy-MM-dd",
            Margin = new Thickness(0, 4)
        };

        var useRangeCheckBox = new CheckBox
        {
            IsChecked = false,
            Margin = new Thickness(0, 4)
        };

        var useRangeLabel = new Label
        {
            Text = "启用时间范围过滤",
            FontSize = 13,
            VerticalOptions = LayoutOptions.Center
        };

        useRangeCheckBox.CheckedChanged += (s, e) =>
        {
            startPicker.IsEnabled = e.Value;
            endPicker.IsEnabled = e.Value;
        };

        startPicker.IsEnabled = false;
        endPicker.IsEnabled = false;

        var checkboxLayout = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { useRangeCheckBox, useRangeLabel }
        };

        // Confirmed filter radio buttons
        var allRadio = new RadioButton { Content = new Label { Text = "全部导出", FontSize = 13 }, Value = 0, IsChecked = true };
        var confirmedRadio = new RadioButton { Content = new Label { Text = "仅导出已确认", FontSize = 13 }, Value = 1 };
        var unconfirmedRadio = new RadioButton { Content = new Label { Text = "仅导出未确认", FontSize = 13 }, Value = 2 };

        var filterGroup = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = "确认状态过滤:", FontSize = 13, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 8, 0, 4) },
                allRadio,
                confirmedRadio,
                unconfirmedRadio
            }
        };

        var exportBtn = new Button
        {
            Text = "导出",
            BackgroundColor = ThemeManager.Success,
            TextColor = Colors.White,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var cancelBtn = new Button
        {
            Text = "取消",
            BackgroundColor = ThemeManager.TextSecondary,
            TextColor = Colors.White,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var layout = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 16),
            Children =
            {
                new Label
                {
                    Text = "选择导出选项",
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center
                },
                checkboxLayout,
                new Label { Text = "开始日期:", FontSize = 13 },
                startPicker,
                new Label { Text = "结束日期:", FontSize = 13 },
                endPicker,
                new BoxView { HeightRequest = 1, BackgroundColor = ThemeManager.BorderLight, Margin = new Thickness(0, 4) },
                filterGroup,
                exportBtn,
                cancelBtn
            }
        };

        ContentPage? popupPage = null;

        exportBtn.Clicked += async (s, e) =>
        {
            DateTime? start = useRangeCheckBox.IsChecked ? startPicker.Date : null;
            DateTime? end = useRangeCheckBox.IsChecked ? endPicker.Date : null;
            int confirmedFilter = 0;
            if (confirmedRadio.IsChecked == true) confirmedFilter = 1;
            else if (unconfirmedRadio.IsChecked == true) confirmedFilter = 2;

            tcs.SetResult((start, end, confirmedFilter));
            if (popupPage != null)
                await popupPage.Navigation.PopModalAsync();
        };

        cancelBtn.Clicked += async (s, e) =>
        {
            tcs.SetResult(null);
            if (popupPage != null)
                await popupPage.Navigation.PopModalAsync();
        };

        popupPage = new ContentPage
        {
            Title = "导出选项",
            BackgroundColor = ThemeManager.Background,
            Content = new Border
            {
                BackgroundColor = ThemeManager.CardBackground,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                StrokeThickness = 1,
                Stroke = ThemeManager.BorderLight,
                Margin = new Thickness(20),
                Content = new ScrollView { Content = layout }
            }
        };

        await Navigation.PushModalAsync(popupPage);
        return await tcs.Task;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settingsPage = _services.GetRequiredService<Pages.SettingsPage>();
        await Navigation.PushAsync(settingsPage);
    }

    private async void OnSavedInvoicesClicked(object? sender, EventArgs e)
    {
        try
        {
            var viewModel = _services.GetRequiredService<InvoiceAI.Core.ViewModels.SavedInvoicesViewModel>();
            var invoiceService = _services.GetRequiredService<IInvoiceService>();
            var settingsService = _services.GetRequiredService<IAppSettingsService>();
            var savedWindow = new SavedInvoicesWindow(viewModel, invoiceService, settingsService);
            var navPage = new NavigationPage(savedWindow);
            var window = new Window(navPage);
            Application.Current!.OpenWindow(window);
        }
        catch (Exception ex)
        {
            await this.DisplayAlert("错误", $"打开已保存列表失败:\n{ex.Message}", "OK");
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var selectedCount = _vm.SelectedInvoices.Count;

        // If nothing multi-selected, fall back to single selected invoice
        if (selectedCount == 0 && _vm.SelectedInvoice == null) return;

        var count = selectedCount > 0 ? selectedCount : 1;
        var confirmMsg = count == 1
            ? $"确定要删除 {_vm.SelectedInvoice?.IssuerName} 的发票吗？"
            : $"确定要删除选中的 {count} 张发票吗？";

        var confirm = await this.DisplayAlert("确认删除", confirmMsg, "删除", "取消");
        if (confirm)
        {
            // Pass null to trigger batch deletion from SelectedInvoices
            await _vm.DeleteInvoiceCommand.ExecuteAsync(null);
            _detailVm.CurrentInvoice = null;
        }
    }

    private static Style? GetStyle(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Style style)
        {
            return style;
        }
        return null;
    }

    private View BuildEmptyState()
    {
        return new VerticalStackLayout
        {
            Padding = new Thickness(40, 60),
            Spacing = 16,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "📄",
                    FontSize = 64,
                    HorizontalOptions = LayoutOptions.Center,
                    Opacity = 0.3
                },
                new Label
                {
                    Text = "暂无发票记录",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = ThemeManager.TextSecondary,
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = "点击左下角「导入」按钮添加您的第一张发票",
                    FontSize = 14,
                    TextColor = ThemeManager.TextTertiary,
                    HorizontalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };
    }
}
