using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace InvoiceAI.App.Pages;

public class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel viewModel)
    {
        _vm = viewModel;
        BindingContext = viewModel;

        Title = "设置";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        Content = BuildContent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.ReloadFromSettings();
    }

    private ScrollView BuildContent()
    {
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
                    BuildEntryField("端点地址", nameof(_vm.BaiduEndpoint), "https://aistudio.baidu.com/...", isPassword: false),
                    BuildTestButton("测试 OCR 连接", nameof(_vm.TestBaiduConnectionCommand)),

                    // ─── GLM Settings ────────────────────────────
                    BuildSectionHeader("GLM API 设置"),
                    BuildEntryField("API Key", nameof(_vm.GlmApiKey), "GlmApiKey"),
                    BuildEntryField("端点地址", nameof(_vm.GlmEndpoint), "GlmEndpoint"),
                    BuildEntryField("模型", nameof(_vm.GlmModel), "GlmModel"),
                    BuildTestButton("测试 GLM 连接", nameof(_vm.TestGlmConnectionCommand)),

                    // ─── Language Settings ─────────────────────────
                    BuildSectionHeader("语言设置"),
                    BuildLanguageSelector(),

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
                                BackgroundColor = Color.FromArgb("#1976D2"),
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
                                BackgroundColor = Color.FromArgb("#757575"),
                                TextColor = Colors.White,
                                FontSize = 14,
                                HorizontalOptions = LayoutOptions.Fill,
                                MinimumHeightRequest = 44
                            }
                            .Invoke(btn => btn.Clicked += OnCloseClicked)
                        }
                    },

                    // ─── Status ──────────────────────────────────────────
                    new Label
                    {
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666"),
                        HorizontalOptions = LayoutOptions.Center
                    }
                    .Bind(Label.TextProperty, nameof(_vm.TestResult))
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
            TextColor = Color.FromArgb("#333"),
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
            BackgroundColor = Colors.White,
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
                        TextColor = Color.FromArgb("#666")
                    },
                    entry
                }
            }
        };
    }

    // ─── Helper: Test Button ─────────────────────────────────────

    private static Button BuildTestButton(string text, string commandName)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#388E3C"),
            TextColor = Colors.White,
            FontSize = 13,
            MinimumHeightRequest = 36,
            HorizontalOptions = LayoutOptions.End
        }
        .Bind(Button.CommandProperty, commandName);
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

        var categoryList = new CollectionView
        {
            ItemsSource = _vm.Categories,
            MinimumHeightRequest = 150,
            MaximumHeightRequest = 250,
            ItemTemplate = new DataTemplate(() =>
            {
                var catLabel = new Label
                {
                    FontSize = 14,
                    VerticalOptions = LayoutOptions.Center
                };
                catLabel.SetBinding(Label.TextProperty, ".");

                var removeBtn = new Button
                {
                    Text = "✕",
                    BackgroundColor = Color.FromArgb("#F44336"),
                    TextColor = Colors.White,
                    FontSize = 12,
                    MinimumWidthRequest = 32,
                    MinimumHeightRequest = 32,
                    Padding = new Thickness(0),
                    VerticalOptions = LayoutOptions.Center
                };

                var item = new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { catLabel, removeBtn }
                };

                // Wire remove button command
                removeBtn.SetBinding(Button.CommandParameterProperty, ".");
                removeBtn.SetBinding(Button.CommandProperty, nameof(_vm.RemoveCategoryCommand));

                return new Border
                {
                    Padding = new Thickness(8, 4),
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#E0E0E0"),
                    BackgroundColor = Colors.White,
                    Content = item
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

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
