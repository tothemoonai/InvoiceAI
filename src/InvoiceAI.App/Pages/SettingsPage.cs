using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.App.Utils;
using InvoiceAI.Core.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace InvoiceAI.App.Pages;

public class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;
    private Label _ocrTestResult = null!;
    private Label _glmTestResult = null!;
    private Label _saveResult = null!;

    public SettingsPage(SettingsViewModel viewModel)
    {
        _vm = viewModel;
        BindingContext = viewModel;

        Title = "设置";
        BackgroundColor = ThemeManager.Background;

        Content = BuildContent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.ReloadFromSettings();
    }

    private ScrollView BuildContent()
    {
        _ocrTestResult = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#388E3C"),
            HorizontalOptions = LayoutOptions.Fill,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _glmTestResult = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#388E3C"),
            HorizontalOptions = LayoutOptions.Fill,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _saveResult = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#388E3C"),
            HorizontalOptions = LayoutOptions.Fill,
            LineBreakMode = LineBreakMode.WordWrap
        }.Bind(Label.TextProperty, nameof(_vm.TestResult));

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24),
                Spacing = 16,
                Children =
                {
                    // ─── PaddleOCR Settings ──────────────────────
                    BuildSectionHeader("PaddleOCR 设置"),
                    BuildEntryField("Token", nameof(_vm.BaiduToken), "PaddleOCR Token"),
                    BuildEntryField("端点地址", nameof(_vm.BaiduEndpoint), "https://aistudio.baidu.com/..."),
                    new Button
                    {
                        Text = "测试 OCR 连接",
                        BackgroundColor = Color.FromArgb("#388E3C"),
                        TextColor = Colors.White,
                        FontSize = 13,
                        MinimumHeightRequest = 36,
                        HorizontalOptions = LayoutOptions.End
                    }
                    .Invoke(btn => btn.Clicked += OnTestOcrClicked),
                    _ocrTestResult,

                    // ─── LLM Settings ────────────────────────────
                    BuildSectionHeader("LLM API 设置"),
                    BuildProviderSelector(),
                    BuildModelPicker(),
                    BuildEntryField("API Key", nameof(_vm.GlmApiKey), "API Key", isPassword: true),
                    new Button
                    {
                        Text = "测试 LLM 连接",
                        BackgroundColor = Color.FromArgb("#388E3C"),
                        TextColor = Colors.White,
                        FontSize = 13,
                        MinimumHeightRequest = 36,
                        HorizontalOptions = LayoutOptions.End
                    }
                    .Invoke(btn => btn.Clicked += OnTestGlmClicked),
                    _glmTestResult,

                    // ─── Language Settings ─────────────────────────
                    BuildSectionHeader("语言设置"),
                    BuildLanguageSelector(),

                    // ─── Theme Settings ─────────────────────────
                    BuildSectionHeader("主题设置"),
                    BuildThemeSelector(),

                    // ─── Export Settings ───────────────────────────
                    BuildSectionHeader("导出设置"),
                    BuildSwitchField("导出后自动保存确认", nameof(_vm.AutoSaveAfterExport), "导出后自动将发票标记为「已确认」"),
                    BuildPathField("Excel 导出路径", nameof(_vm.ExportPath), "选择 Excel 导出文件的默认保存目录"),

                    // ─── Archive Settings ──────────────────────────
                    BuildSectionHeader("发票归档设置"),
                    BuildPathField("发票文件保存路径", nameof(_vm.InvoiceArchivePath), "导入后发票文件（压缩/重命名）的归档目录"),

                    // ─── Category Management ───────────────────────
                    BuildSectionHeader("分类管理"),
                    BuildCategoryManager(),

                    // ─── Action Buttons ─────────────────────────────────
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        HorizontalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            new Button
                            {
                                Text = "保存",
                                BackgroundColor = ThemeManager.BrandPrimary,
                                TextColor = Colors.White,
                                FontSize = 14,
                                FontAttributes = FontAttributes.Bold,
                                HorizontalOptions = LayoutOptions.Fill,
                                MinimumHeightRequest = 44
                            }
                            .Bind(Button.CommandProperty, nameof(_vm.SaveCommand)),
                            new Button
                            {
                                Text = "关闭",
                                BackgroundColor = ThemeManager.TextSecondary,
                                TextColor = Colors.White,
                                FontSize = 14,
                                HorizontalOptions = LayoutOptions.Fill,
                                MinimumHeightRequest = 44
                            }
                            .Invoke(btn => btn.Clicked += OnCloseClicked)
                        }
                    },
                    _saveResult
                }
            }
        };
    }

    // ─── Helper: Section Header ────────────────────────────────

    private static Label BuildSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeManager.TextPrimary,
            Margin = new Thickness(0, 16, 0, 4)
        };
    }

    // ─── Helper: Entry Field with Label + Entry ─────────────────

    private static Border BuildEntryField(string label, string bindingPath, string placeholder, bool isPassword = false)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,
            FontSize = 14,
            BackgroundColor = ThemeManager.CardBackground,
            MinimumHeightRequest = 40
        };
        entry.SetBinding(Entry.TextProperty, bindingPath);

        if (isPassword)
        {
            entry.IsPassword = true;
        }

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E0E0E0"),
            Padding = new Thickness(0),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = label,
                        FontSize = 12,
                        TextColor = ThemeManager.TextSecondary
                    },
                    entry
                }
            }
        };
    }

    // ─── Helper: Switch Field ─────────────────────────────────

    private static Border BuildSwitchField(string label, string bindingPath, string description)
    {
        var switchCtrl = new Switch
        {
            HorizontalOptions = LayoutOptions.Start
        };
        switchCtrl.SetBinding(Switch.IsToggledProperty, bindingPath);

        var descLabel = new Label
        {
            Text = description,
            FontSize = 11,
            TextColor = ThemeManager.TextTertiary
        };

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            Padding = new Thickness(12, 10),
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Auto))
                },
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                    new RowDefinition(new GridLength(1, GridUnitType.Auto))
                },
                Children =
                {
                    new Label
                    {
                        Text = label,
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = ThemeManager.TextPrimary,
                        VerticalOptions = LayoutOptions.Center
                    }.Column(0).Row(0),
                    switchCtrl.Column(1).Row(0).RowSpan(2),
                    descLabel.Column(0).Row(1)
                }
            }
        };
    }

    // ─── Helper: Path Field with Browse Button ────────────────

    private Border BuildPathField(string label, string bindingPath, string description)
    {
        var pathEntry = new Entry
        {
            Placeholder = "点击右侧按钮选择文件夹",
            FontSize = 13,
            BackgroundColor = Colors.White,
            MinimumHeightRequest = 36,
            IsReadOnly = true
        };
        pathEntry.SetBinding(Entry.TextProperty, bindingPath);

        var browseBtn = new Button
        {
            Text = "📁 选择",
            BackgroundColor = ThemeManager.BrandPrimary,
            TextColor = Colors.White,
            FontSize = 12,
            MinimumWidthRequest = 80,
            MinimumHeightRequest = 36,
            Padding = new Thickness(8, 4)
        };

        var descLabel = new Label
        {
            Text = description,
            FontSize = 11,
            TextColor = ThemeManager.TextTertiary
        };

        // Extract property name from binding path (e.g. "_vm.ExportPath" -> "ExportPath")
        var propName = bindingPath.StartsWith("_vm.") ? bindingPath.Substring(4) : bindingPath;

        browseBtn.Clicked += async (s, e) =>
        {
#if WINDOWS
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var win = this.Window;
            var platformWnd = win.Handler?.PlatformView;
            if (platformWnd is not Microsoft.UI.Xaml.Window xamlWindow) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(xamlWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                pathEntry.Text = folder.Path;
                var vmProp = BindingContext?.GetType().GetProperty(propName);
                if (vmProp != null && BindingContext != null)
                    vmProp.SetValue(BindingContext, folder.Path);
            }
#else
            await this.DisplayAlert("提示", "当前平台不支持文件夹选择，请手动输入路径", "OK");
#endif
        };

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            Padding = new Thickness(12, 10),
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Auto))
                },
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                    new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                    new RowDefinition(new GridLength(1, GridUnitType.Auto))
                },
                Children =
                {
                    new Label
                    {
                        Text = label,
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold
                    }.Column(0).Row(0).ColumnSpan(2),
                    pathEntry.Column(0).Row(1),
                    browseBtn.Column(1).Row(1),
                    descLabel.Column(0).Row(2).ColumnSpan(2)
                }
            }
        };
    }

    // ─── Helper: Provider Selector ───────────────────────────

    private View BuildProviderSelector()
    {
        var zhipu = new RadioButton
        {
            Content = new Label { Text = "智谱 (Zhipu)", FontSize = 14 },
            Value = "zhipu",
            HorizontalOptions = LayoutOptions.Start
        };
        zhipu.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsZhipuProvider));

        var nvidia = new RadioButton
        {
            Content = new Label { Text = "NVIDIA NIM", FontSize = 14 },
            Value = "nvidia",
            HorizontalOptions = LayoutOptions.Start
        };
        nvidia.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsNvidiaProvider));

        var cerebras = new RadioButton
        {
            Content = new Label { Text = "Cerebras", FontSize = 14 },
            Value = "cerebras",
            HorizontalOptions = LayoutOptions.Start
        };
        cerebras.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsCerebrasProvider));

        return new HorizontalStackLayout
        {
            Spacing = 16,
            Children = { zhipu, nvidia, cerebras }
        };
    }

    // ─── Helper: Model Picker ────────────────────────────────

    private View BuildModelPicker()
    {
        var picker = new Picker
        {
            FontSize = 14,
            MinimumHeightRequest = 40,
            Title = "选择模型"
        };
        picker.SetBinding(Picker.ItemsSourceProperty, nameof(_vm.AvailableModels));
        picker.SetBinding(Picker.SelectedIndexProperty, nameof(_vm.SelectedModelIndex));

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            StrokeThickness = 1,
            Stroke = ThemeManager.BorderLight,
            Padding = new Thickness(0),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = "模型",
                        FontSize = 12,
                        TextColor = ThemeManager.TextSecondary
                    },
                    picker
                }
            }
        };
    }

    // ─── Helper: Language Selector ───────────────────────────

    private View BuildLanguageSelector()
    {
        var zh = new RadioButton
        {
            Content = new Label { Text = "中文", FontSize = 14 },
            Value = "zh",
            HorizontalOptions = LayoutOptions.Start
        };
        zh.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsChineseLanguage));

        var ja = new RadioButton
        {
            Content = new Label { Text = "日本語", FontSize = 14 },
            Value = "ja",
            HorizontalOptions = LayoutOptions.Start
        };
        ja.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsJapaneseLanguage));

        return new HorizontalStackLayout
        {
            Spacing = 16,
            Children = { zh, ja }
        };
    }

    // ─── Helper: Theme Selector ─────────────────────────

    private View BuildThemeSelector()
    {
        var autoRadio = new RadioButton
        {
            Content = new Label { Text = "跟随系统", FontSize = 14 },
            Value = "Auto"
        };
        autoRadio.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsAutoTheme));

        var lightRadio = new RadioButton
        {
            Content = new Label { Text = "浅色", FontSize = 14 },
            Value = "Light"
        };
        lightRadio.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsLightTheme));

        var darkRadio = new RadioButton
        {
            Content = new Label { Text = "暗色", FontSize = 14 },
            Value = "Dark"
        };
        darkRadio.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsDarkTheme));

        return new VerticalStackLayout
        {
            Spacing = 4,
            Children = { autoRadio, lightRadio, darkRadio }
        };
    }

    // ─── Helper: Category Manager ───────────────────────────

    private View BuildCategoryManager()
    {
        var newCategoryEntry = new Entry
        {
            Placeholder = "新分类名称",
            FontSize = 14,
            MinimumHeightRequest = 40,
            WidthRequest = 200
        };
        newCategoryEntry.SetBinding(Entry.TextProperty, nameof(_vm.NewCategory));

        var addBtn = new Button
        {
            Text = "添加",
            BackgroundColor = Color.FromArgb("#1976D2"),
            TextColor = Colors.White,
            FontSize = 13,
            MinimumHeightRequest = 36
        };
        addBtn.SetBinding(Button.CommandProperty, nameof(_vm.AddCategoryCommand));

        // Use CollectionView with GridItemsLayout for 3 columns
        var categoryList = new CollectionView
        {
            ItemsSource = _vm.Categories,
            MinimumHeightRequest = 60,
            MaximumHeightRequest = 300,
            ItemsLayout = new GridItemsLayout(3, ItemsLayoutOrientation.Horizontal)
            {
                HorizontalItemSpacing = 8,
                VerticalItemSpacing = 8
            },
            ItemTemplate = new DataTemplate(() =>
            {
                var catLabel = new Label
                {
                    FontSize = 13,
                    TextColor = ThemeManager.TextPrimary,
                    VerticalOptions = LayoutOptions.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                catLabel.SetBinding(Label.TextProperty, ".");

                var removeBtn = new Button
                {
                    Text = "✕",
                    BackgroundColor = Colors.Transparent,
                    TextColor = ThemeManager.Error,
                    FontSize = 12,
                    MinimumWidthRequest = 20,
                    MinimumHeightRequest = 20,
                    Padding = new Thickness(0),
                    VerticalOptions = LayoutOptions.Center
                };
                removeBtn.SetBinding(Button.CommandParameterProperty, ".");
                removeBtn.SetBinding(Button.CommandProperty, nameof(_vm.RemoveCategoryCommand));

                var chipContent = new HorizontalStackLayout
                {
                    Spacing = 4,
                    Children = { catLabel, removeBtn }
                };

                return new Border
                {
                    Padding = new Thickness(8, 0),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    StrokeThickness = 1,
                    Stroke = ThemeManager.BrandPrimary,
                    BackgroundColor = Color.FromArgb("#E3F2FD"),
                    HorizontalOptions = LayoutOptions.Start,
                    Content = chipContent
                };
            })
        };

        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { newCategoryEntry, addBtn }
                },
                categoryList
            }
        };
    }

    // ─── Event Handlers ──────────────────────────────────────────

    private async void OnTestOcrClicked(object? sender, EventArgs e)
    {
        _ocrTestResult.Text = "正在测试...";
        _ocrTestResult.TextColor = ThemeManager.Info;
        try
        {
            await _vm.TestBaiduConnectionCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _vm.TestResult = $"异常: {ex.Message}";
        }
        _ocrTestResult.Text = _vm.TestResult;
        _ocrTestResult.TextColor = _vm.TestResult.Contains("成功")
            ? ThemeManager.Success
            : ThemeManager.Error;
    }

    private async void OnTestGlmClicked(object? sender, EventArgs e)
    {
        _glmTestResult.Text = "正在测试...";
        _glmTestResult.TextColor = ThemeManager.Info;
        try
        {
            await _vm.TestGlmConnectionCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _vm.TestResult = $"异常: {ex.Message}";
        }
        _glmTestResult.Text = _vm.TestResult;
        _glmTestResult.TextColor = _vm.TestResult.Contains("成功")
            ? ThemeManager.Success
            : ThemeManager.Error;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
