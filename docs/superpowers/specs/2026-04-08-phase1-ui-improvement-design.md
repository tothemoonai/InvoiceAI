# Phase 1 UI 改进设计：配色系统 + 字体配对 + 暗色主题

> **日期**: 2026-04-08  
> **阶段**: Phase 1 (基础修复)  
> **优先级**: P0  
> **预估工作量**: 2-3 小时

---

## 1. 概述

### 1.1 目标

消除 InvoiceAI 项目中硬编码的颜色和单调的字体，建立统一的、可维护的视觉设计系统，并支持完整的 Light/Dark 双主题。

### 1.2 范围

| 包含 | 不包含 |
|------|--------|
| Colors.xaml 扩展（语义化颜色键） | 响应式布局调整（Phase 2） |
| ThemeManager.cs 主题助手类 | 微交互动画（Phase 2） |
| 下载并注册 5 个新字体 | 矢量图标替换 Emoji（Phase 3） |
| MainPage.cs / SettingsPage.cs 颜色替换 | 空状态/弹窗设计改进（Phase 2） |
| 设置页面主题切换控件 | 骨架屏/加载动画（Phase 2） |
| 系统主题自动跟随 | |

### 1.3 用户决策确认

| 决策项 | 选择 |
|--------|------|
| 改进范围 | Phase 1 分阶段迭代 |
| 品牌主色 | 紫罗兰色系 `#6C5CE7` |
| 字体方案 | Noto 家族统一方案（Poppins + Noto Sans SC + Noto Sans JP + JetBrains Mono + Inter） |
| 暗色主题 | 跟随系统 + 手动覆盖（设置页面切换） |
| 字体许可证 | SIL OFL 1.1 (全部免费商用) |

---

## 2. 配色系统设计

### 2.1 颜色架构

采用**语义化颜色键**替代硬编码 hex 值：

```
Colors.xaml (扩展)
├── 品牌色
│   ├── BrandPrimary: #6C5CE7        (紫罗兰主色)
│   ├── BrandPrimaryDark: #4834D4    (深紫变体)
│   ├── BrandAccent: #00CEC9         (青绿强调色)
│   └── BrandAccentDark: #00B5B0
│
├── 功能色
│   ├── Success: #00B894             (成功)
│   ├── Error: #FF6B6B               (错误)
│   ├── Warning: #FECA57             (警告)
│   └── Info: #54A0FF                (信息)
│
├── Light 主题
│   ├── BackgroundPrimary: #F8F9FA
│   ├── BackgroundSecondary: #FFFFFF
│   ├── TextPrimary: #2D3436
│   ├── TextSecondary: #636E72
│   ├── TextTertiary: #B2BEC3
│   ├── BorderLight: #E0E6ED
│   └── BorderMedium: #CBD5E0
│
├── Dark 主题
│   ├── DarkBackgroundPrimary: #1A1A2E
│   ├── DarkBackgroundSecondary: #16213E
│   ├── DarkBackgroundTertiary: #0F3460
│   ├── DarkTextPrimary: #EAEAEA
│   ├── DarkTextSecondary: #B0B0B0
│   ├── DarkTextTertiary: #707070
│   └── DarkBorder: #2D3748
│
└── 状态色
    ├── SelectedBackground: #E8EAF6 (Light) / #1E3A5F (Dark)
    └── HoverBackground: #F5F3FF (Light) / #2D2B55 (Dark)
```

### 2.2 ThemeManager.cs

新建 `src/InvoiceAI.App/Utils/ThemeManager.cs`，提供:

```csharp
public static class ThemeManager
{
    // 当前是否暗色主题
    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    // 获取主题感知颜色
    public static Color GetThemeColor(string lightHex, string darkHex);
    
    // 便捷属性
    public static Color Background { get; }
    public static Color CardBackground { get; }
    public static Color TextPrimary { get; }
    public static Color TextSecondary { get; }
    public static Color BorderLight { get; }
    public static Color BrandPrimary { get; }
    public static Color Success { get; }
    public static Color Error { get; }
    public static Color Warning { get; }
}
```

**设计原则**: 静态便捷属性，零运行时开销，直接从 `Application.Current.Resources` 读取。

### 2.3 颜色替换策略

**优先级**: 使用 `AppThemeBinding` 实现自动主题切换 > 使用 `ThemeManager` 获取 > 硬编码回退

**搜索关键词** (所有 `.cs` 文件):
- `Color.FromArgb("#1976D2")` → `ThemeManager.BrandPrimary`
- `Color.FromArgb("#4CAF50")` → `ThemeManager.Success`
- `Color.FromArgb("#F44336")` → `ThemeManager.Error`
- `Color.FromArgb("#FF9800")` → `ThemeManager.Warning`
- `Color.FromArgb("#F5F5F5")` → `ThemeManager.Background`
- `Color.FromArgb("#FAFAFA")` → `ThemeManager.CardBackground`
- `Color.FromArgb("#333")` → `ThemeManager.TextPrimary`
- `Color.FromArgb("#666")` → `ThemeManager.TextSecondary`
- `Color.FromArgb("#999")` → `ThemeManager.TextTertiary`
- `Color.FromArgb("#E0E0E0")` → `ThemeManager.BorderLight`

**受影响文件**:
- `Pages/MainPage.cs` — 约 30+ 处
- `Pages/SettingsPage.cs` — 约 20+ 处

---

## 3. 字体系统设计

### 3.1 字体清单

| 用途 | 字体 | 文件名 | 字重 | 许可证 | 预估大小 |
|------|------|--------|------|--------|----------|
| 英文标题 | Poppins | `Poppins-Bold.ttf` | Bold (700) | SIL OFL 1.1 | ~150KB |
| 英文副标题 | Poppins | `Poppins-SemiBold.ttf` | SemiBold (600) | SIL OFL 1.1 | ~150KB |
| 中文正文 | Noto Sans SC | `NotoSansSC-Regular.ttf` | Regular (400) | SIL OFL 1.1 | ~8MB |
| 中文强调 | Noto Sans SC | `NotoSansSC-Medium.ttf` | Medium (500) | SIL OFL 1.1 | ~8MB |
| 日文正文 | Noto Sans JP | `NotoSansJP-Regular.ttf` | Regular (400) | SIL OFL 1.1 | ~8MB |
| 日文强调 | Noto Sans JP | `NotoSansJP-Medium.ttf` | Medium (500) | SIL OFL 1.1 | ~8MB |
| 数字/金额 | JetBrains Mono | `JetBrainsMono-Medium.ttf` | Medium (500) | SIL OFL 1.1 | ~200KB |
| 辅助文字 | Inter | `Inter-Regular.ttf` | Regular (400) | SIL OFL 1.1 | ~250KB |

**总预估大小**: ~35MB (Noto Sans CJK 字体较大)

**缓解方案**: 
- 优先下载 Regular/Medium 字重，不使用 Bold/Light
- 如果文件过大，可使用**字体子集化工具** (如 pyftsubset) 仅保留常用中日文字符

### 3.2 字体注册 (MauiProgram.cs)

```csharp
builder.ConfigureFonts(fonts =>
{
    // 现有字体
    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

    // 新增字体
    fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
    fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemiBold");
    fonts.AddFont("NotoSansSC-Regular.ttf", "NotoSansSCRegular");
    fonts.AddFont("NotoSansSC-Medium.ttf", "NotoSansSCMedium");
    fonts.AddFont("NotoSansJP-Regular.ttf", "NotoSansJPRegular");
    fonts.AddFont("NotoSansJP-Medium.ttf", "NotoSansJPMedium");
    fonts.AddFont("JetBrainsMono-Medium.ttf", "JetBrainsMonoMedium");
    fonts.AddFont("Inter-Regular.ttf", "InterRegular");
});
```

### 3.3 字体样式定义 (Styles.xaml)

```xml
<!-- 标题样式 (品牌展示) -->
<Style x:Key="BrandTitle" TargetType="Label">
    <Setter Property="FontFamily" Value="PoppinsBold" />
    <Setter Property="FontSize" Value="20" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light=White, Dark=White}" />
    <Setter Property="LetterSpacing" Value="0.5" />
</Style>

<!-- 章节标题 -->
<Style x:Key="SectionHeader" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCMedium" />
    <Setter Property="FontSize" Value="16" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
    <Setter Property="FontAttributes" Value="Bold" />
</Style>

<!-- 金额/数字 -->
<Style x:Key="AmountDisplay" TargetType="Label">
    <Setter Property="FontFamily" Value="JetBrainsMonoMedium" />
    <Setter Property="FontSize" Value="14" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource BrandPrimary}, Dark={StaticResource White}}" />
    <Setter Property="FontAttributes" Value="Bold" />
</Style>

<!-- 正文 (默认) -->
<Style TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCRegular" />
    <Setter Property="FontSize" Value="14" />
</Style>

<!-- 正文 (次要) -->
<Style x:Key="BodyTextSecondary" TargetType="Label">
    <Setter Property="FontFamily" Value="NotoSansSCRegular" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextSecondary}, Dark={StaticResource DarkTextSecondary}}" />
</Style>

<!-- 按钮文字 -->
<Style TargetType="Button">
    <Setter Property="FontFamily" Value="NotoSansSCMedium" />
    <Setter Property="FontSize" Value="14" />
    <Setter Property="FontAttributes" Value="Bold" />
</Style>
```

### 3.4 多语言字体回退策略

MAUI 的字体回退机制:
- 如果当前字体不支持某些字符 (如 Poppins 不支持中文/日文)，MAUI 会自动使用系统字体回退
- **关键**: 对于混合内容 (英文+中文/日文)，需要:
  - 英文为主的控件: 使用 Poppins
  - 中文/日文为主的控件: 使用 Noto Sans SC/JP
  - 不确定内容的控件: 使用 Noto Sans (覆盖全语言)

**实际应用中**: 将默认 Label 的 FontFamily 改为 `NotoSansSCRegular`，它能正确处理英文、中文、日文。

---

## 4. 暗色主题实现

### 4.1 架构

```
暗色主题架构
├── AppThemeBinding (XAML)
│   └── 自动跟随系统主题
│
├── 设置页面主题切换
│   ├── RadioButtons: 浅色 / 暗色 / 跟随系统
│   └── 保存到 AppSettings.json
│
└── 主题变化响应
    ├── Application.RequestedThemeChanged 事件
    └── 通知页面刷新 (仅对硬编码颜色需要)
```

### 4.2 设置页面主题切换控件

在 SettingsPage 的「语言设置」后添加「主题设置」:

```csharp
// 主题选择 RadioButtons
var themeAuto = new RadioButton 
{ 
    Content = new Label { Text = "跟随系统", FontSize = 14 },
    Value = "Auto"
};

var themeLight = new RadioButton 
{ 
    Content = new Label { Text = "浅色", FontSize = 14 },
    Value = "Light"
};

var themeDark = new RadioButton 
{ 
    Content = new Label { Text = "暗色", FontSize = 14 },
    Value = "Dark"
};
```

### 4.3 主题持久化

将用户选择保存到 `AppSettingsService` 中:

```json
// appsettings.json (新增字段)
{
  "ThemeMode": "Auto"  // "Auto" | "Light" | "Dark"
}
```

应用启动时读取此设置并应用:

```csharp
// App.xaml.cs
protected override Window CreateWindow(IActivationState? activationState)
{
    var themeMode = _settingsService.Settings.ThemeMode;
    UserAppTheme = themeMode switch
    {
        "Light" => AppTheme.Light,
        "Dark" => AppTheme.Dark,
        _ => AppTheme.Unspecified  // 跟随系统
    };
    
    return new Window(new AppShell(...));
}
```

### 4.4 颜色绑定策略

对于 C# 代码创建的控件，使用 `SetBinding` + `AppThemeBinding`:

```csharp
// 方式 1: 直接创建 AppThemeBinding
label.SetBinding(Label.TextColorProperty, new AppThemeBinding
{
    Light = ThemeManager.TextPrimary,
    Dark = ThemeManager.DarkTextPrimary
});

// 方式 2: 使用 Setter (用于 Style)
new Style(TargetType: typeof(Label))
{
    Setters =
    {
        new Setter 
        { 
            Property = Label.TextColorProperty, 
            Value = new AppThemeBinding 
            { 
                Light = ThemeManager.TextPrimary, 
                Dark = ThemeManager.DarkTextPrimary 
            } 
        }
    }
}
```

---

## 5. 数据流

```
┌─────────────────────────────────────────────────────────────┐
│                        App Startup                          │
│                                                             │
│  1. 读取 AppSettings.json → ThemeMode                      │
│  2. 设置 Application.UserAppTheme                          │
│  3. MAUI 根据主题加载对应颜色                               │
│  4. 页面使用 ThemeManager/AppThemeBinding 获取颜色          │
│  5. 字体从 MauiProgram.cs 注册，Styles.xaml 定义样式        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     用户切换主题                            │
│                                                             │
│  1. 设置页面 RadioButton 变更                                │
│  2. 保存到 AppSettings.json                                 │
│  3. 更新 Application.UserAppTheme                           │
│  4. AppThemeBinding 自动刷新颜色 (XAML 控件)                 │
│  5. C# 控件: 监听 RequestedThemeChanged，手动更新颜色        │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. 错误处理

| 场景 | 处理 |
|------|------|
| 字体文件不存在 | 回退到 OpenSans，记录 Warning 日志 |
| 主题颜色键未定义 | 回退到硬编码默认值 (防止崩溃) |
| 系统主题切换时页面未响应 | 对 C# 创建的控件手动调用更新方法 |
| 用户手动设置主题与系统冲突 | 以用户设置优先，显示提示 |

---

## 7. 测试清单

### 7.1 功能测试

- [ ] Light 主题下所有页面颜色正确
- [ ] Dark 主题下所有页面颜色正确
- [ ] 切换主题后颜色实时更新
- [ ] 应用重启后主题设置持久化
- [ ] 系统主题切换时应用自动跟随

### 7.2 视觉测试

- [ ] 字体渲染清晰，无锯齿或模糊
- [ ] 中日文显示正常，无方块或乱码
- [ ] 金额数字使用等宽字体
- [ ] 标题使用 Poppins，粗体明显
- [ ] 颜色对比度符合 WCAG AA (≥ 4.5:1)

### 7.3 性能测试

- [ ] 应用启动时间无明显增加 (< 100ms)
- [ ] 主题切换无闪烁
- [ ] 字体文件加载无内存泄漏

---

## 8. 实施顺序

```
Step 1: 下载字体文件 (5 个 TTF)
  ↓
Step 2: 注册字体 (MauiProgram.cs)
  ↓
Step 3: 扩展 Colors.xaml (语义化颜色键)
  ↓
Step 4: 创建 ThemeManager.cs
  ↓
Step 5: 更新 Styles.xaml (字体样式)
  ↓
Step 6: MainPage.cs 颜色替换 (30+ 处)
  ↓
Step 7: SettingsPage.cs 颜色替换 (20+ 处) + 添加主题切换
  ↓
Step 8: App.cs 添加主题监听
  ↓
Step 9: 测试验证 (Light/Dark 切换、多语言显示)
  ↓
Step 10: 提交代码 + 更新文档
```

---

*文档版本: v1.0 | 2026-04-08*
