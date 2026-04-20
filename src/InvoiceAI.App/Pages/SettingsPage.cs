using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.App.Utils;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models.Auth;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace InvoiceAI.App.Pages;

public class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;
    private readonly AuthViewModel _authVm;
    private Label _ocrTestResult = null!;
    private Label _glmTestResult = null!;
    private Label _saveResult = null!;

    public SettingsPage(SettingsViewModel viewModel, AuthViewModel authViewModel)
    {
        _vm = viewModel;
        _authVm = authViewModel;
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
                    // ─── Account Section ─────────────────────────────
                    BuildAccountSection(),

                    // ─── PaddleOCR Settings ──────────────────────
                    BuildSectionHeaderWithButton("PaddleOCR 设置", "测试连接", Color.FromArgb("#388E3C"), OnTestOcrClicked),
                    _ocrTestResult,
                    BuildEntryField("Token", nameof(_vm.BaiduToken), "PaddleOCR Token"),
                    BuildEntryField("端点地址", nameof(_vm.BaiduEndpoint), "https://aistudio.baidu.com/..."),

                    // ─── LLM Settings ────────────────────────────
                    BuildSectionHeaderWithButton("LLM API 设置", "测试连接", Color.FromArgb("#388E3C"), OnTestGlmClicked),
                    _glmTestResult,
                    BuildProviderSelector(),
                    BuildModelPicker(),
                    BuildEntryField("API Key", nameof(_vm.GlmApiKey), "API Key", isPassword: true),

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

    private static HorizontalStackLayout BuildSectionHeaderWithButton(string text, string buttonText, Color buttonColor, EventHandler onClick)
    {
        return new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 16, 0, 4),
            Children =
            {
                new Label
                {
                    Text = text,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = ThemeManager.TextPrimary,
                    VerticalOptions = LayoutOptions.Center
                },
                new Button
                {
                    Text = buttonText,
                    BackgroundColor = buttonColor,
                    TextColor = Colors.White,
                    FontSize = 12,
                    MinimumHeightRequest = 28,
                    Padding = new Thickness(10, 0),
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.End
                }
                .Invoke(btn => btn.Clicked += onClick)
            }
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

        var google = new RadioButton
        {
            Content = new Label { Text = "Google", FontSize = 14 },
            Value = "google",
            HorizontalOptions = LayoutOptions.Start
        };
        google.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsGoogleProvider));

        return new HorizontalStackLayout
        {
            Spacing = 16,
            Children = { zhipu, nvidia, cerebras, google }
        };
    }

    // ─── Helper: Model Picker ────────────────────────────────

    private View BuildModelPicker()
    {
        var picker = new Picker
        {
            FontSize = 14,
            MinimumHeightRequest = 40,
            Title = "选择起始模型（失败时自动切换到下一个）"
        };
        picker.SetBinding(Picker.ItemsSourceProperty, nameof(_vm.AvailableModels));
        picker.SetBinding(Picker.SelectedIndexProperty, nameof(_vm.SelectedModelIndex));

        // 模型数量提示标签
        var modelInfoLabel = new Label
        {
            FontSize = 11,
            TextColor = ThemeManager.TextTertiary
        };
        modelInfoLabel.SetBinding(Label.TextProperty, nameof(_vm.AvailableModels), converter: new FuncConverter<System.Collections.IList, string>(models =>
            models != null && models.Count > 1
                ? $"当前提供商有 {models.Count} 个模型，失败时自动切换"
                : "当前提供商仅有 1 个模型"
        ));

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
                        Text = "模型 (起始模型)",
                        FontSize = 12,
                        TextColor = ThemeManager.TextSecondary
                    },
                    picker,
                    modelInfoLabel
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

        return new HorizontalStackLayout
        {
            Spacing = 16,
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

        var categoryFlex = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start,
            VerticalOptions = LayoutOptions.Start
        };

        RebuildCategoryChips(categoryFlex);

        if (_vm.Categories is System.Collections.Specialized.INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += (s, e) =>
                MainThread.BeginInvokeOnMainThread(() => RebuildCategoryChips(categoryFlex));
        }

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
                categoryFlex
            }
        };
    }

    private void RebuildCategoryChips(FlexLayout flex)
    {
        flex.Children.Clear();
        foreach (var cat in _vm.Categories)
        {
            flex.Children.Add(new Border
            {
                HeightRequest = 32,
                Margin = new Thickness(0, 0, 8, 4),
                Padding = new Thickness(10, 0, 4, 0),
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                StrokeThickness = 1,
                Stroke = ThemeManager.BrandPrimary,
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Content = new HorizontalStackLayout
                {
                    Spacing = 2,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = cat,
                            FontSize = 13,
                            TextColor = ThemeManager.TextPrimary,
                            VerticalOptions = LayoutOptions.Center,
                            VerticalTextAlignment = TextAlignment.Center,
                            LineBreakMode = LineBreakMode.TailTruncation
                        },
                        new Button
                        {
                            Text = "✕",
                            BackgroundColor = Colors.Transparent,
                            TextColor = ThemeManager.Error,
                            FontSize = 11,
                            WidthRequest = 18,
                            HeightRequest = 18,
                            Padding = new Thickness(0),
                            Margin = new Thickness(0),
                            VerticalOptions = LayoutOptions.Center,
                            HorizontalOptions = LayoutOptions.Center,
                            Command = _vm.RemoveCategoryCommand,
                            CommandParameter = cat
                        }
                    }
                }
            });
        }
    }

    // ─── Account Section (auto-login, status only) ────────────────

    private View BuildAccountSection()
    {
        var cloudStatusLabel = new Label
        {
            Text = "✅ 云端已连接",
            FontSize = 13,
            TextColor = ThemeManager.Success,
            VerticalOptions = LayoutOptions.Center
        };

        var cloudIndicator = new ContentView
        {
            Content = _authVm.AuthState.IsAuthenticated ? cloudStatusLabel : null
        };

        _authVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_authVm.AuthState))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    cloudIndicator.Content = _authVm.AuthState.IsAuthenticated
                        ? cloudStatusLabel
                        : null;
                });
            }
        };

        return cloudIndicator;
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

// ─── Helper: FuncConverter for value converters ─────────────────────

public class FuncConverter<TIn, TOut> : IValueConverter
{
    private readonly Func<TIn, TOut> _func;

    public FuncConverter(Func<TIn, TOut> func)
    {
        _func = func;
    }

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is TIn input)
            return _func(input);
        return default(TOut);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
