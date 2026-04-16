# Phase 1 UI 改进实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 InvoiceAI 建立统一的配色系统、多字体配对和完整的暗色主题支持。

**Architecture:** 扩展 XAML 资源字典定义语义化颜色键和字体样式，创建 ThemeManager.cs 提供主题感知颜色访问，在 C# 代码中替换所有硬编码颜色，添加设置页面主题切换控件。

**Tech Stack:** .NET MAUI 9, CommunityToolkit.Maui, XAML ResourceDictionary, AppThemeBinding

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `src/InvoiceAI.App/Resources/Fonts/*.ttf` | 新增 (8 个) | 字体文件 |
| `src/InvoiceAI.App/Resources/Styles/Colors.xaml` | 修改 | 添加语义化颜色键 |
| `src/InvoiceAI.App/Resources/Styles/Styles.xaml` | 修改 | 添加字体样式定义 |
| `src/InvoiceAI.App/MauiProgram.cs` | 修改 | 注册新字体 |
| `src/InvoiceAI.App/Utils/ThemeManager.cs` | 新增 | 主题管理助手 |
| `src/InvoiceAI.Core/Helpers/AppSettings.cs` | 修改 | 添加 ThemeMode 属性 |
| `src/InvoiceAI.App/App.cs` | 修改 | 应用主题设置 |
| `src/InvoiceAI.App/Pages/MainPage.cs` | 修改 | 替换硬编码颜色，应用字体样式 |
| `src/InvoiceAI.App/Pages/SettingsPage.cs` | 修改 | 替换硬编码颜色，添加主题切换控件 |

---

### Task 1: 下载并注册字体文件

**Files:**
- Create: `src/InvoiceAI.App/Resources/Fonts/Poppins-Bold.ttf`
- Create: `src/InvoiceAI.App/Resources/Fonts/Poppins-SemiBold.ttf`
- Create: `src/InvoiceAI.App/Resources/Fonts/NotoSansSC-Regular.ttf`
- Create: `src/InvoiceAI.App/Resources/Fonts/NotoSansSC-Medium.ttf`
- Create: `src/InvoiceAI.App/Resources/Fonts/NotoSansJP-Regular.ttf`
- Create: `src/InvoiceAI.App/Resources/Fonts/NotoSansJP-Medium.ttf`
- Create: `src/InvoiceAI.App/Resources/Fonts/JetBrainsMono-Medium.ttf`
- Create: `src/InvoiceAI.App/Resources/Fonts/Inter-Regular.ttf`
- Modify: `src/InvoiceAI.App/MauiProgram.cs`

- [ ] **Step 1: 下载字体文件**

从以下来源下载字体 TTF 文件到 `src/InvoiceAI.App/Resources/Fonts/`:

| 字体 | 下载链接 | 文件名 |
|------|----------|--------|
| Poppins Bold | https://fonts.google.com/download?family=Poppins → 解压取 `Poppins-Bold.ttf` | `Poppins-Bold.ttf` |
| Poppins SemiBold | 同上 | `Poppins-SemiBold.ttf` |
| Noto Sans SC Regular | https://fonts.google.com/download?family=Noto+Sans+SC → 解压取 `NotoSansSC-Regular.ttf` | `NotoSansSC-Regular.ttf` |
| Noto Sans SC Medium | 同上 | `NotoSansSC-Medium.ttf` |
| Noto Sans JP Regular | https://fonts.google.com/download?family=Noto+Sans+JP → 解压取 `NotoSansJP-Regular.ttf` | `NotoSansJP-Regular.ttf` |
| Noto Sans JP Medium | 同上 | `NotoSansJP-Medium.ttf` |
| JetBrains Mono Medium | https://www.jetbrains.com/lp/mono/ → 解压取 `JetBrainsMono-Medium.ttf` | `JetBrainsMono-Medium.ttf` |
| Inter Regular | https://fonts.google.com/download?family=Inter → 解压取 `Inter-Regular.ttf` | `Inter-Regular.ttf` |

**下载方式**: 使用 `agent-browser` 或手动从 Google Fonts 下载。

- [ ] **Step 2: 在 MauiProgram.cs 中注册字体**

修改 `src/InvoiceAI.App/MauiProgram.cs`，在 `ConfigureFonts` 中添加:

```csharp
builder.ConfigureFonts(fonts =>
{
    // 现有字体
    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

    // 新增: 标题字体 (Poppins)
    fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
    fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemiBold");

    // 新增: 中文 (Noto Sans SC)
    fonts.AddFont("NotoSansSC-Regular.ttf", "NotoSansSCRegular");
    fonts.AddFont("NotoSansSC-Medium.ttf", "NotoSansSCMedium");

    // 新增: 日文 (Noto Sans JP)
    fonts.AddFont("NotoSansJP-Regular.ttf", "NotoSansJPRegular");
    fonts.AddFont("NotoSansJP-Medium.ttf", "NotoSansJPMedium");

    // 新增: 数字/金额 (JetBrains Mono)
    fonts.AddFont("JetBrainsMono-Medium.ttf", "JetBrainsMonoMedium");

    // 新增: 辅助文字 (Inter)
    fonts.AddFont("Inter-Regular.ttf", "InterRegular");
});
```

- [ ] **Step 3: 确保 .csproj 包含字体文件**

检查 `src/InvoiceAI.App/InvoiceAI.App.csproj`，确保有 `<MauiFont>` 条目包含新字体:

```xml
<ItemGroup>
    <!-- 现有字体 -->
    <MauiFont Include="Resources\Fonts\*" />
</ItemGroup>
```

如果使用的是通配符 `*`，新字体会自动包含。否则手动添加。

- [ ] **Step 4: 验证字体注册**

运行命令:
```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
```

预期: 编译成功，无错误。

- [ ] **Step 5: Commit**

```bash
git add src/InvoiceAI.App/Resources/Fonts/*.ttf src/InvoiceAI.App/MauiProgram.cs
git commit -m "feat: add Poppins, Noto Sans SC/JP, JetBrains Mono, Inter fonts"
```

---

### Task 2: 扩展 Colors.xaml 语义化颜色

**Files:**
- Modify: `src/InvoiceAI.App/Resources/Styles/Colors.xaml`

- [ ] **Step 1: 读取现有 Colors.xaml**

当前文件包含 Primary, Secondary, Gray 系列颜色。保留这些，在其后添加语义化颜色。

- [ ] **Step 2: 添加语义化颜色键**

在 `</ResourceDictionary>` 之前添加:

```xml
<!-- ====== 品牌色 ====== -->
<Color x:Key="BrandPrimary">#6C5CE7</Color>
<Color x:Key="BrandPrimaryDark">#4834D4</Color>
<Color x:Key="BrandAccent">#00CEC9</Color>
<Color x:Key="BrandAccentDark">#00B5B0</Color>

<!-- ====== 功能色 ====== -->
<Color x:Key="Success">#00B894</Color>
<Color x:Key="Error">#FF6B6B</Color>
<Color x:Key="Warning">#FECA57</Color>
<Color x:Key="Info">#54A0FF</Color>

<!-- ====== Light 主题 - 背景色 ====== -->
<Color x:Key="BackgroundPrimary">#F8F9FA</Color>
<Color x:Key="BackgroundSecondary">#FFFFFF</Color>
<Color x:Key="BackgroundTertiary">#ECF0F1</Color>

<!-- ====== Light 主题 - 文字色 ====== -->
<Color x:Key="TextPrimary">#2D3436</Color>
<Color x:Key="TextSecondary">#636E72</Color>
<Color x:Key="TextTertiary">#B2BEC3</Color>

<!-- ====== Light 主题 - 边框/分割线 ====== -->
<Color x:Key="BorderLight">#E0E6ED</Color>
<Color x:Key="BorderMedium">#CBD5E0</Color>
<Color x:Key="Divider">#F1F3F5</Color>

<!-- ====== Light 主题 - 状态色 ====== -->
<Color x:Key="SelectedBackground">#E8EAF6</Color>
<Color x:Key="HoverBackground">#F5F3FF</Color>

<!-- ====== Dark 主题 - 背景色 ====== -->
<Color x:Key="DarkBackgroundPrimary">#1A1A2E</Color>
<Color x:Key="DarkBackgroundSecondary">#16213E</Color>
<Color x:Key="DarkBackgroundTertiary">#0F3460</Color>

<!-- ====== Dark 主题 - 文字色 ====== -->
<Color x:Key="DarkTextPrimary">#EAEAEA</Color>
<Color x:Key="DarkTextSecondary">#B0B0B0</Color>
<Color x:Key="DarkTextTertiary">#707070</Color>

<!-- ====== Dark 主题 - 边框/分割线 ====== -->
<Color x:Key="DarkBorder">#2D3748</Color>
<Color x:Key="DarkDivider">#2D3748</Color>

<!-- ====== Dark 主题 - 状态色 ====== -->
<Color x:Key="DarkSelectedBackground">#1E3A5F</Color>
<Color x:Key="DarkHoverBackground">#2D2B55</Color>
```

- [ ] **Step 3: 验证 XAML 语法**

运行:
```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
```

预期: 编译成功。

- [ ] **Step 4: Commit**

```bash
git add src/InvoiceAI.App/Resources/Styles/Colors.xaml
git commit -m "feat: add semantic color keys for brand, functional, and theme colors"
```

---

### Task 3: 创建 ThemeManager.cs

**Files:**
- Create: `src/InvoiceAI.App/Utils/ThemeManager.cs`

- [ ] **Step 1: 创建 ThemeManager.cs**

创建文件 `src/InvoiceAI.App/Utils/ThemeManager.cs`:

```csharp
namespace InvoiceAI.App.Utils;

/// <summary>
/// 提供主题感知的颜色访问。所有颜色从 XAML 资源字典读取，
/// 自动根据 Light/Dark 主题返回对应值。
/// </summary>
public static class ThemeManager
{
    /// <summary>当前是否为暗色主题</summary>
    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    /// <summary>获取 Light/Dark 颜色</summary>
    public static Color Get(string lightKey, string darkKey)
    {
        var key = IsDark ? darkKey : lightKey;
        return GetColor(key);
    }

    /// <summary>获取单一颜色键 (自动主题)</summary>
    public static Color Get(string key) => GetColor(key);

    // ─── 便捷属性 ─────────────────────────────────────────

    public static Color Background => Get("BackgroundPrimary", "DarkBackgroundPrimary");
    public static Color CardBackground => Get("BackgroundSecondary", "DarkBackgroundSecondary");
    public static Color TextPrimary => Get("TextPrimary", "DarkTextPrimary");
    public static Color TextSecondary => Get("TextSecondary", "DarkTextSecondary");
    public static Color TextTertiary => Get("TextTertiary", "DarkTextTertiary");
    public static Color BorderLight => Get("BorderLight", "DarkBorder");
    public static Color BorderMedium => Get("BorderMedium", "DarkBorder");
    public static Color BrandPrimary => Get("BrandPrimary", "BrandPrimary");
    public static Color BrandAccent => Get("BrandAccent", "BrandAccent");
    public static Color Success => Get("Success", "Success");
    public static Color Error => Get("Error", "Error");
    public static Color Warning => Get("Warning", "Warning");
    public static Color Info => Get("Info", "Info");
    public static Color SelectedBackground => Get("SelectedBackground", "DarkSelectedBackground");
    public static Color HoverBackground => Get("HoverBackground", "DarkHoverBackground");

    // ─── 内部实现 ─────────────────────────────────────────

    private static Color GetColor(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        // 回退值 (防止资源未加载时崩溃)
        return key switch
        {
            "BackgroundPrimary" or "DarkBackgroundPrimary" => Color.FromArgb("#F8F9FA"),
            "BackgroundSecondary" or "DarkBackgroundSecondary" => Colors.White,
            "TextPrimary" or "DarkTextPrimary" => Color.FromArgb("#2D3436"),
            "TextSecondary" or "DarkTextSecondary" => Color.FromArgb("#636E72"),
            "TextTertiary" or "DarkTextTertiary" => Color.FromArgb("#B2BEC3"),
            "BorderLight" or "DarkBorder" => Color.FromArgb("#E0E6ED"),
            "BrandPrimary" => Color.FromArgb("#6C5CE7"),
            "BrandAccent" => Color.FromArgb("#00CEC9"),
            "Success" => Color.FromArgb("#00B894"),
            "Error" => Color.FromArgb("#FF6B6B"),
            "Warning" => Color.FromArgb("#FECA57"),
            "Info" => Color.FromArgb("#54A0FF"),
            "SelectedBackground" or "DarkSelectedBackground" => Color.FromArgb("#E8EAF6"),
            _ => Colors.Transparent
        };
    }
}
```

- [ ] **Step 2: 验证编译**

运行:
```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
```

预期: 编译成功。

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.App/Utils/ThemeManager.cs
git commit -m "feat: add ThemeManager for theme-aware color access"
```

---

### Task 4: 更新 Styles.xaml 字体样式

**Files:**
- Modify: `src/InvoiceAI.App/Resources/Styles/Styles.xaml`

- [ ] **Step 1: 在 Styles.xaml 中添加字体样式**

在 `</ResourceDictionary>` 之前添加:

```xml
<!-- ====== 字体样式 ====== -->

<!-- 标题样式 (品牌展示) -->
<Style x:Key="BrandTitle" TargetType="Label">
    <Setter Property="FontFamily" Value="PoppinsBold" />
    <Setter Property="FontSize" Value="20" />
    <Setter Property="TextColor" Value="White" />
    <Setter Property="LetterSpacing" Value="0.5" />
</Style>

<Style x:Key="BrandTitleSmall" TargetType="Label">
    <Setter Property="FontFamily" Value="PoppinsSemiBold" />
    <Setter Property="FontSize" Value="16" />
    <Setter Property="TextColor" Value="White" />
</Style>

<!-- 章节标题 -->
<Style x:Key="SectionHeader" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCMedium" />
    <Setter Property="FontSize" Value="16" />
    <Setter Property="FontAttributes" Value="Bold" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
</Style>

<!-- 金额/数字显示 (等宽字体) -->
<Style x:Key="AmountDisplay" TargetType="Label">
    <Setter Property="FontFamily" Value="JetBrainsMonoMedium" />
    <Setter Property="FontSize" Value="14" />
    <Setter Property="FontAttributes" Value="Bold" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource BrandPrimary}, Dark={StaticResource White}}" />
</Style>

<Style x:Key="AmountLarge" TargetType="Label">
    <Setter Property="FontFamily" Value="JetBrainsMonoMedium" />
    <Setter Property="FontSize" Value="18" />
    <Setter Property="FontAttributes" Value="Bold" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
</Style>

<!-- 正文 (次要) -->
<Style x:Key="BodyTextSecondary" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCRegular" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextSecondary}, Dark={StaticResource DarkTextSecondary}}" />
</Style>

<!-- 辅助文字 (小号) -->
<Style x:Key="CaptionText" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCRegular" />
    <Setter Property="FontSize" Value="11" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextTertiary}, Dark={StaticResource DarkTextTertiary}}" />
</Style>

<!-- 状态文字 (成功) -->
<Style x:Key="StatusSuccess" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCMedium" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="TextColor" Value="{StaticResource Success}" />
</Style>

<!-- 状态文字 (错误) -->
<Style x:Key="StatusError" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCMedium" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="TextColor" Value="{StaticResource Error}" />
</Style>

<!-- 状态文字 (信息) -->
<Style x:Key="StatusInfo" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCMedium" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="TextColor" Value="{StaticResource Info}" />
</Style>

<!-- 状态文字 (警告) -->
<Style x:Key="StatusWarning" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCMedium" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="TextColor" Value="{StaticResource Warning}" />
</Style>
```

- [ ] **Step 2: 修改默认 Label 样式字体**

找到现有的 `<Style TargetType="Label">`，将 `FontFamily` 改为:

```xml
<Style TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCRegular" />
    <Setter Property="FontSize" Value="14" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource Gray900}, Dark={StaticResource White}}" />
    ...
</Style>
```

- [ ] **Step 3: 修改默认 Button 样式字体**

找到现有的 `<Style TargetType="Button">`，将 `FontFamily` 改为:

```xml
<Style TargetType="Button">
    <Setter Property="FontFamily" Value="NotoSansSCMedium" />
    <Setter Property="FontSize" Value="14" />
    <Setter Property="FontAttributes" Value="Bold" />
    ...
</Style>
```

- [ ] **Step 4: 验证编译**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
```

- [ ] **Step 5: Commit**

```bash
git add src/InvoiceAI.App/Resources/Styles/Styles.xaml
git commit -m "feat: add font styles for titles, amounts, body text, and status"
```

---

### Task 5: 添加 AppSettings ThemeMode 属性

**Files:**
- Modify: `src/InvoiceAI.Core/Helpers/AppSettings.cs`
- Modify: `src/InvoiceAI.App/App.cs`

- [ ] **Step 1: 在 AppSettings.cs 中添加 ThemeMode**

修改 `src/InvoiceAI.Core/Helpers/AppSettings.cs`，在 `Categories` 属性前添加:

```csharp
public class AppSettings
{
    public BaiduOcrSettings BaiduOcr { get; set; } = new();
    public GlmSettings Glm { get; set; } = new();
    public string Language { get; set; } = "zh";
    
    // 新增: 主题模式 ("Auto" | "Light" | "Dark")
    public string ThemeMode { get; set; } = "Auto";
    
    public bool AutoSaveAfterExport { get; set; } = false;
    public string ExportPath { get; set; } = string.Empty;
    public string InvoiceArchivePath { get; set; } = string.Empty;
    public List<string> Categories { get; set; } =
    [
        "電気・ガス", "食料品", "オフィス用品",
        "交通費", "通信費", "接待費", "その他"
    ];
}
```

- [ ] **Step 2: 在 App.cs 中应用主题设置**

修改 `src/InvoiceAI.App/App.cs` 的 `CreateWindow` 方法，在创建窗口之前设置主题:

```csharp
protected override Window CreateWindow(IActivationState? activationState)
{
    // 应用主题设置
    ApplyThemeMode();

    try
    {
        var page = Handler.MauiContext.Services.GetRequiredService<Pages.MainPage>();
        var navPage = new NavigationPage(page);
        var window = new Window(navPage);

#if WINDOWS
        SetupWindowsDragDrop(window);
#endif

        return window;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"CreateWindow error: {ex}");
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InvoiceAI", "startup_crash.log"),
            $"{DateTime.Now:O}\n{ex}");
        throw;
    }
}

/// <summary>
/// 根据 AppSettings.ThemeMode 设置应用主题
/// </summary>
private void ApplyThemeMode()
{
    UserAppTheme = _settingsService.Settings.ThemeMode switch
    {
        "Light" => AppTheme.Light,
        "Dark" => AppTheme.Dark,
        _ => AppTheme.Unspecified  // Auto: 跟随系统
    };
}
```

- [ ] **Step 3: 监听主题变化**

在 `App.cs` 的构造函数中添加主题变化监听:

```csharp
public App(IAppSettingsService settingsService, AppDbContext dbContext, IServiceProvider services)
{
    _settingsService = settingsService;
    _dbContext = dbContext;
    _services = services;

    // 监听系统主题变化
    RequestedThemeChanged += (s, e) =>
    {
        // 如果用户设置为 Auto，则自动跟随系统
        if (_settingsService.Settings.ThemeMode == "Auto")
        {
            // MAUI 会自动处理
        }
    };
}
```

- [ ] **Step 4: 验证编译**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj
```

- [ ] **Step 5: Commit**

```bash
git add src/InvoiceAI.Core/Helpers/AppSettings.cs src/InvoiceAI.App/App.cs
git commit -m "feat: add ThemeMode setting and apply on startup"
```

---

### Task 6: MainPage.cs 颜色替换

**Files:**
- Modify: `src/InvoiceAI.App/Pages/MainPage.cs`

这是最大的改动，约 30+ 处硬编码颜色需要替换。

- [ ] **Step 1: 在 MainPage.cs 顶部添加 using**

在文件顶部添加:

```csharp
using InvoiceAI.App.Utils;
```

- [ ] **Step 2: 替换 BuildUI() 中的颜色**

```csharp
// 修改前:
BackgroundColor = Color.FromArgb("#F5F5F5");
_busyIndicator.Color = Color.FromArgb("#1976D2");

// 修改后:
this.SetBinding(BackgroundColorProperty, new AppThemeBinding
{
    Light = ThemeManager.Background,
    Dark = ThemeManager.Get("DarkBackgroundPrimary", "DarkBackgroundPrimary")
});
_busyIndicator.Color = ThemeManager.BrandPrimary;
```

**注意**: 由于 `SetBinding` 在构造函数中调用时资源可能未加载，更安全的做法是:

```csharp
BackgroundColor = ThemeManager.Background;
_busyIndicator.Color = ThemeManager.BrandPrimary;
```

- [ ] **Step 3: 替换 BuildTitleBar() 中的颜色**

```csharp
private static View BuildTitleBar()
{
    return new Border
    {
        BackgroundColor = ThemeManager.BrandPrimary,
        Padding = new Thickness(16, 8),
        Content = new HorizontalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "InvoiceAI",
                    Style = GetStyle("BrandTitle"),
                    VerticalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = "- 発票智能管理",
                    FontSize = 14,
                    FontFamily = "NotoSansSCRegular",
                    TextColor = Color.FromArgb("#BBDEFB"),  // 保留，标题栏上的浅色文字
                    VerticalOptions = LayoutOptions.Center
                }
            }
        }
    };
}
```

- [ ] **Step 4: 创建 GetStyle 辅助方法**

在 MainPage 类中添加:

```csharp
private static Style? GetStyle(string key)
{
    if (Application.Current?.Resources.TryGetValue(key, out var value) == true
        && value is Style style)
    {
        return style;
    }
    return null;
}
```

- [ ] **Step 5: 替换 BuildCategoryPanel() 中的颜色**

```csharp
// 修改前:
return new Border
{
    BackgroundColor = Colors.White,
    ...
    Children = { ..., new BoxView { BackgroundColor = Color.FromArgb("#E0E0E0") } }
};

// 修改后:
return new Border
{
    BackgroundColor = ThemeManager.CardBackground,
    ...
    Children = { ..., new BoxView { BackgroundColor = ThemeManager.BorderLight } }
};
```

按钮颜色替换:

```csharp
// 修改前:
BuildActionButton("📥 导入", OnImportClicked, Color.FromArgb("#1976D2")),
BuildActionButton("📤 导出", OnExportClicked, Color.FromArgb("#388E3C")),
BuildActionButton("⚙ 设置", OnSettingsClicked, Color.FromArgb("#757575"))

// 修改后:
BuildActionButton("📥 导入", OnImportClicked, ThemeManager.BrandPrimary),
BuildActionButton("📤 导出", OnExportClicked, ThemeManager.Success),
BuildActionButton("⚙ 设置", OnSettingsClicked, ThemeManager.TextSecondary)
```

- [ ] **Step 6: 替换 BuildInvoiceListPanel() 中的颜色**

关键替换:

```csharp
// 面板背景
BackgroundColor = ThemeManager.Get("BackgroundTertiary", "DarkBackgroundTertiary")

// 搜索栏
Placeholder = "搜索发票...",
// SearchBar 默认样式即可

// 类型徽章 (typeBadge)
BackgroundColor = ThemeManager.Success  // 替代 #4CAF50

// 金额文字
TextColor = ThemeManager.BrandPrimary,  // 替代 #1976D2
Style = GetStyle("AmountDisplay")

// 次要文字 (日期、分类)
TextColor = ThemeManager.TextSecondary,  // 替代 #666
FontFamily = "NotoSansSCRegular"

// 空状态
EmptyView = BuildEmptyState()

// 列表边框
Stroke = ThemeManager.BorderLight  // 替代 #E0E0E0
BackgroundColor = ThemeManager.CardBackground
```

VisualStateManager 中的选中状态:

```csharp
new VisualState
{
    Name = "Selected",
    Setters =
    {
        new Setter { Property = Border.BackgroundColorProperty, Value = ThemeManager.SelectedBackground },
        new Setter { Property = Border.StrokeProperty, Value = ThemeManager.BrandPrimary }
    }
}
```

- [ ] **Step 7: 替换 BuildDetailPanel() 中的颜色**

关键替换:

```csharp
// 面板背景
BackgroundColor = ThemeManager.CardBackground

// 缺失字段警告框
BackgroundColor = Color.FromArgb("#FFF3E0"),  // 保留特殊警告色
Stroke = ThemeManager.Warning,

// 明细项目背景
BackgroundColor = ThemeManager.Get("BackgroundTertiary", "DarkBackgroundTertiary")

// 图片预览背景
BackgroundColor = ThemeManager.Get("BackgroundTertiary", "DarkBackgroundTertiary")
Stroke = ThemeManager.BorderLight
```

- [ ] **Step 8: 替换 BuildImportOverlay() 中的颜色**

```csharp
BackgroundColor = Color.FromArgb("#CC000000"),  // 半透明遮罩，保留
ProgressColor = ThemeManager.BrandPrimary  // 替代 #1976D2
```

- [ ] **Step 9: 替换 BuildDropZone() 中的颜色**

```csharp
BackgroundColor = Color.FromArgb("#33000000"),  // 半透明遮罩，保留
Stroke = ThemeManager.BrandPrimary  // 替代 #1976D2
```

- [ ] **Step 10: 替换 BuildDetailRow() 中的颜色**

```csharp
private static Border BuildDetailRow(string label, string value)
{
    return new Border
    {
        BackgroundColor = ThemeManager.Get("BackgroundTertiary", "DarkBackgroundTertiary"),
        Padding = new Thickness(12, 6),
        StrokeShape = new RoundRectangle { CornerRadius = 4 },
        StrokeThickness = 1,
        Stroke = ThemeManager.BorderLight,
        Content = new Grid
        {
            ...
            Children =
            {
                new Label
                {
                    Text = label,
                    FontSize = 13,
                    TextColor = ThemeManager.TextSecondary,  // 替代 #666
                    VerticalOptions = LayoutOptions.Center
                }.Column(0),
                new Label
                {
                    Text = value,
                    FontSize = 13,
                    FontFamily = "JetBrainsMonoMedium",  // 数字使用等宽字体
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    VerticalOptions = LayoutOptions.Center
                }.Column(1)
            }
        }
    };
}
```

- [ ] **Step 11: 替换 BuildActionButton() 中的颜色**

```csharp
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
```

这个函数本身接收 bgColor 参数，无需修改内部。调用方传入 `ThemeManager.BrandPrimary` 等。

- [ ] **Step 12: 添加空状态 BuildEmptyState()**

在 MainPage 中添加:

```csharp
private static View BuildEmptyState()
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
```

并在 `_invoiceList.EmptyView` 中使用:

```csharp
EmptyView = BuildEmptyState()
```

- [ ] **Step 13: 替换 DisplayDateRangeDialog() 中的颜色**

```csharp
// 弹窗背景
BackgroundColor = ThemeManager.Background,

// 卡片边框
BackgroundColor = ThemeManager.CardBackground,
Stroke = ThemeManager.BorderLight,

// 按钮
BackgroundColor = ThemeManager.Success,  // 导出按钮
BackgroundColor = ThemeManager.TextSecondary,  // 取消按钮
```

- [ ] **Step 14: 替换 OnTestOcrClicked / OnTestGlmClicked 中的颜色**

```csharp
// 修改前:
_ocrTestResult.TextColor = Color.FromArgb("#388E3C");  // 成功
_ocrTestResult.TextColor = Color.FromArgb("#F44336");  // 失败

// 修改后:
_ocrTestResult.TextColor = ThemeManager.Success;
_ocrTestResult.TextColor = ThemeManager.Error;
```

- [ ] **Step 15: 验证编译**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
```

预期: 编译成功，无错误。

- [ ] **Step 16: Commit**

```bash
git add src/InvoiceAI.App/Pages/MainPage.cs
git commit -m "refactor: replace hardcoded colors with ThemeManager in MainPage"
```

---

### Task 7: SettingsPage.cs 颜色替换 + 主题切换

**Files:**
- Modify: `src/InvoiceAI.App/Pages/SettingsPage.cs`

- [ ] **Step 1: 在 SettingsPage.cs 顶部添加 using**

```csharp
using InvoiceAI.App.Utils;
```

- [ ] **Step 2: 替换构造函数中的颜色**

```csharp
// 修改前:
BackgroundColor = Color.FromArgb("#F5F5F5");

// 修改后:
this.SetBinding(BackgroundColorProperty, new Binding
{
    Source = ThemeManager.Background,
    Mode = BindingMode.OneTime  // 启动时设置一次
});
// 或直接:
BackgroundColor = ThemeManager.Background;
```

- [ ] **Step 3: 替换 BuildSectionHeader() 中的颜色**

```csharp
private static Label BuildSectionHeader(string text)
{
    return new Label
    {
        Text = text,
        FontSize = 16,
        FontAttributes = FontAttributes.Bold,
        FontFamily = "NotoSansSCMedium",
        TextColor = ThemeManager.TextPrimary,
        Margin = new Thickness(0, 16, 0, 4)
    };
}
```

- [ ] **Step 4: 替换 BuildEntryField() 中的颜色**

```csharp
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
        Stroke = ThemeManager.BorderLight,
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
                    FontFamily = "NotoSansSCRegular",
                    TextColor = ThemeManager.TextSecondary
                },
                entry
            }
        }
    };
}
```

- [ ] **Step 5: 替换 BuildSwitchField() 中的颜色**

```csharp
// 边框
Stroke = ThemeManager.BorderLight,

// 描述文字
TextColor = ThemeManager.TextTertiary
```

- [ ] **Step 6: 替换 BuildPathField() 中的颜色**

```csharp
// 浏览按钮
BackgroundColor = ThemeManager.BrandPrimary,
TextColor = Colors.White,

// 边框
Stroke = ThemeManager.BorderLight,
```

- [ ] **Step 7: 替换 BuildProviderSelector() / BuildModelPicker() / BuildLanguageSelector() 中的颜色**

这些控件使用默认 RadioButton/Picker 样式即可，主要确保字体正确:

```csharp
// RadioButton 文字
FontFamily = "NotoSansSCRegular"
```

- [ ] **Step 8: 替换 BuildCategoryManager() 中的颜色**

```csharp
// 分类 Chip 背景
BackgroundColor = Color.FromArgb("#E3F2FD"),  // 保留浅蓝底色
Stroke = ThemeManager.BrandPrimary,  // 边框使用品牌色

// 分类文字
TextColor = ThemeManager.TextPrimary,  // 替代 #333

// 删除按钮
TextColor = ThemeManager.Error,  // 替代 #F44336
```

- [ ] **Step 9: 添加主题设置控件**

在 `BuildContent()` 方法中，在「语言设置」之后、「导出设置」之前添加:

```csharp
// ─── Theme Settings ────────────────────────────
BuildSectionHeader("主题设置"),
BuildThemeSelector(),
```

然后添加 `BuildThemeSelector()` 方法:

```csharp
private View BuildThemeSelector()
{
    var autoRadio = new RadioButton
    {
        Content = new Label { Text = "跟随系统", FontSize = 14, FontFamily = "NotoSansSCRegular" },
        Value = "Auto"
    };
    autoRadio.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsAutoTheme));

    var lightRadio = new RadioButton
    {
        Content = new Label { Text = "浅色", FontSize = 14, FontFamily = "NotoSansSCRegular" },
        Value = "Light"
    };
    lightRadio.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsLightTheme));

    var darkRadio = new RadioButton
    {
        Content = new Label { Text = "暗色", FontSize = 14, FontFamily = "NotoSansSCRegular" },
        Value = "Dark"
    };
    darkRadio.SetBinding(RadioButton.IsCheckedProperty, nameof(_vm.IsDarkTheme));

    return new VerticalStackLayout
    {
        Spacing = 4,
        Children = { autoRadio, lightRadio, darkRadio }
    };
}
```

- [ ] **Step 10: 在 SettingsViewModel 中添加主题属性**

修改 `src/InvoiceAI.Core/ViewModels/SettingsViewModel.cs` (如果该文件不存在则创建)，添加:

```csharp
// 主题模式
private string _themeMode = "Auto";
public string ThemeMode
{
    get => _themeMode;
    set => SetProperty(ref _themeMode, value);
}

public bool IsAutoTheme
{
    get => ThemeMode == "Auto";
    set
    {
        if (value)
        {
            ThemeMode = "Auto";
            ApplyTheme();
        }
    }
}

public bool IsLightTheme
{
    get => ThemeMode == "Light";
    set
    {
        if (value)
        {
            ThemeMode = "Light";
            ApplyTheme();
        }
    }
}

public bool IsDarkTheme
{
    get => ThemeMode == "Dark";
    set
    {
        if (value)
        {
            ThemeMode = "Dark";
            ApplyTheme();
        }
    }
}

private void ApplyTheme()
{
    Application.Current.UserAppTheme = ThemeMode switch
    {
        "Light" => AppTheme.Light,
        "Dark" => AppTheme.Dark,
        _ => AppTheme.Unspecified
    };
}
```

并在 `SaveCommand` 中保存:

```csharp
// 在 SaveCommand 中:
_settingsService.Settings.ThemeMode = ThemeMode;
await _settingsService.SaveAsync();
```

在 `ReloadFromSettings()` 中加载:

```csharp
// 在 ReloadFromSettings() 中:
ThemeMode = _settingsService.Settings.ThemeMode;
```

- [ ] **Step 11: 替换测试连接结果的颜色**

```csharp
// 正在测试
_ocrTestResult.TextColor = ThemeManager.Info;

// 成功
_ocrTestResult.TextColor = ThemeManager.Success;

// 失败
_ocrTestResult.TextColor = ThemeManager.Error;
```

- [ ] **Step 12: 替换保存/关闭按钮颜色**

```csharp
// 保存按钮
BackgroundColor = ThemeManager.BrandPrimary,

// 关闭按钮
BackgroundColor = ThemeManager.TextSecondary,
```

- [ ] **Step 13: 验证编译**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj
```

- [ ] **Step 14: Commit**

```bash
git add src/InvoiceAI.App/Pages/SettingsPage.cs src/InvoiceAI.Core/ViewModels/SettingsViewModel.cs
git commit -m "feat: replace hardcoded colors and add theme selector in SettingsPage"
```

---

### Task 8: 最终测试与验证

**Files:**
- 无文件修改，仅测试

- [ ] **Step 1: 完整编译**

```bash
dotnet build InvoiceAI.sln
```

预期: 0 errors, 0 warnings (或仅有已有的 warnings)

- [ ] **Step 2: 运行应用 (Light 主题)**

```bash
dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj
```

验证:
- [ ] 标题栏显示紫罗兰色 (`#6C5CE7`)
- [ ] 文字使用 Noto Sans SC 渲染
- [ ] 金额数字使用 JetBrains Mono 等宽字体
- [ ] "InvoiceAI" 标题使用 Poppins Bold
- [ ] 页面背景为 `#F8F9FA` (非原来的 `#F5F5F5`)
- [ ] 按钮颜色统一 (BrandPrimary / Success)

- [ ] **Step 3: 切换到 Dark 主题**

在 Windows 设置中切换系统主题为暗色，或应用内设置页面选择「暗色」。

验证:
- [ ] 背景变为 `#1A1A2E`
- [ ] 卡片背景变为 `#16213E`
- [ ] 文字变为 `#EAEAEA`
- [ ] 所有颜色适配暗色主题，无刺眼白色区域

- [ ] **Step 4: 测试主题切换持久化**

1. 设置主题为 Dark
2. 关闭应用
3. 重新启动
4. 验证: 主题仍为 Dark

- [ ] **Step 5: 测试多语言显示**

1. 在设置中切换语言为日本語
2. 验证: 日文使用 Noto Sans JP 显示，无方块或乱码

- [ ] **Step 6: Commit (如有修改)**

```bash
git add .
git commit -m "test: verify Phase 1 UI changes - colors, fonts, theme"
```

---

## 自审查

### 1. 规范覆盖检查

| 规范章节 | 对应 Task |
|----------|-----------|
| 配色系统 (Colors.xaml) | Task 2 |
| ThemeManager.cs | Task 3 |
| 字体下载与注册 | Task 1, Task 4 |
| 字体样式定义 | Task 4 |
| AppSettings.ThemeMode | Task 5 |
| App.cs 主题应用 | Task 5 |
| MainPage.cs 颜色替换 | Task 6 |
| SettingsPage.cs 颜色替换 + 主题切换 | Task 7 |
| 测试验证 | Task 8 |

所有规范需求都有对应 Task 实现。

### 2. 占位符扫描

- 无 "TBD", "TODO", "implement later"
- 所有代码步骤包含实际代码
- 无 "similar to Task N" 引用

### 3. 类型一致性

- `ThemeManager` 的所有属性名在 Tasks 6/7 中一致使用
- `GetStyle()` 辅助方法返回 `Style?`，调用方正确处理 null
- 字体名称 (`PoppinsBold`, `NotoSansSCRegular` 等) 与 MauiProgram.cs 注册一致

### 4. 范围检查

本计划仅包含 Phase 1 的 3 个目标:
- ✅ 配色系统统一
- ✅ 字体配对引入
- ✅ 暗色主题支持

不包含 Phase 2 的响应式布局、微交互、空状态深度改进等。

---

*计划版本: v1.0 | 2026-04-08*
