# InvoiceAI UI 问题诊断与改进方案

> **生成日期**: 2026-04-08  
> **技术栈**: .NET MAUI 9 (Windows Desktop)  
> **UI 构建方式**: 纯 C# Code-Behind (CommunityToolkit.Maui.Markup)  
> **审计范围**: MainPage.cs (1402行), SettingsPage.cs, 资源字典, 样式文件

---

## 📋 目录

- [技术栈概况](#技术栈概况)
- [严重问题 (P0-P1)](#严重问题)
- [中等问题 (P2)](#中等问题)
- [轻微问题 / 改进建议](#轻微问题--改进建议)
- [设计美学问题](#设计美学问题)
- [优先级排序](#优先级排序)
- [详细修复方案](#详细修复方案)

---

## 技术栈概况

| 项目 | 详情 |
|------|------|
| **框架** | .NET MAUI 9 (目标: `net9.0-windows10.0.19041.0`) |
| **UI 构建** | C# Code-Behind + CommunityToolkit.Maui.Markup 流式 API |
| **XAML 使用** | 仅用于全局资源字典 (Colors.xaml, Styles.xaml) 和 Windows 入口 |
| **字体** | OpenSans Regular / Semibold (TTF) |
| **配色** | 硬编码 Material Design 色系 (#1976D2, #4CAF50, #F44336 等) |
| **国际化** | 中文 (Strings.resx) / 日本語 (Strings.ja.resx) |
| **布局** | 三栏固定布局 (180px 分类 | 280px 列表 | Star 详情) |

---

## 🔴 严重问题

### 1. 配色方案混乱且不一致

**问题描述:**
- XAML 资源字典定义了 `Primary: #512BD4` (紫色系)
- C# 代码完全忽略资源字典，硬编码另一套配色:
  - 标题栏: `#1976D2` (Material Blue)
  - 成功色: `#4CAF50` (绿色)
  - 错误色: `#F44336` (红色)
  - 警告色: `#FF9800` (橙色)
  - 背景色: `#F5F5F5`, `#FAFAFA`, `#FFFFFF`
- **影响**: 资源字典形同虚设，修改主题需同时改多处，维护成本极高

**涉及文件:**
- `src/InvoiceAI.App/Resources/Styles/Colors.xaml`
- `src/InvoiceAI.App/Pages/MainPage.cs` (约 30+ 处硬编码)
- `src/InvoiceAI.App/Pages/SettingsPage.cs` (约 20+ 处硬编码)

---

### 2. 字体选择单调且缺乏视觉层级

**问题描述:**
- 全局仅使用 **OpenSans** (Google Fonts 最基础的无衬线体)
- 标题、正文、辅助文字、数字全部使用同一字体族
- 没有利用字重 (Regular vs Semibold)、字宽变化创造设计感
- **影响**: 界面缺乏品牌识别度，视觉层次扁平

**涉及文件:**
- `src/InvoiceAI.App/Resources/Fonts/OpenSans-Regular.ttf`
- `src/InvoiceAI.App/Resources/Fonts/OpenSans-Semibold.ttf`
- `src/InvoiceAI.App/Resources/Styles/Styles.xaml` (Label 样式)

---

### 3. 空间布局保守且缺少响应式设计

**问题描述:**
- 三栏布局使用固定宽度: `180px | 280px | Star`
- 不适应不同屏幕尺寸 (窗口缩放、多显示器、高分辨率)
- 大量重复的 `Padding = 12,8`、`Margin = 8,4` 等魔术数值
- 内容区拥挤，发票列表项缺少呼吸感
- **影响**: 窗口缩放时体验差，大屏浪费空间，小屏内容溢出

**涉及文件:**
- `src/InvoiceAI.App/Pages/MainPage.cs` (`BuildUI()` 方法)

---

## 🟡 中等问题

### 4. 视觉反馈不足

**问题描述:**
- **选中状态**: 仅改变背景色 (`#BBDEFB`) 和边框色 (`#1976D2`)，无动画过渡
- **悬停效果**: 完全未定义 `PointerOver` 视觉状态
- **按钮点击**: 无按下动画、缩放或涟漪效果
- **加载状态**: 仅有 `ActivityIndicator` 旋转，无骨架屏或占位符
- **影响**: 交互缺乏即时反馈，用户体验冷淡

**涉及组件:**
- 发票列表项 (`CollectionView.ItemTemplate`)
- 所有 `Button` 控件
- 加载状态 (`_busyIndicator`)

---

### 5. 卡片设计平庸，缺少视觉深度

**问题描述:**
- 发票列表项使用简单的 `Border` + `RoundRectangle { CornerRadius = 6 }`
- **阴影效果完全缺失** (MAUI 的 `Shadow` 未被使用)
- 层级关系不明显：所有卡片平铺，无悬浮感
- **影响**: 界面扁平到乏味，重要元素不突出

**涉及文件:**
- `src/InvoiceAI.App/Pages/MainPage.cs` (`BuildInvoiceListPanel()`)

---

### 6. 空状态设计简陋

**问题描述:**
- 仅显示 "暂无发票记录" / "← 选择一张发票查看详情" 文字
- 缺少引导性插图、动画或行动按钮 (CTA - Call to Action)
- **影响**: 首次用户体验冷淡，不知如何开始使用

**涉及位置:**
- `MainPage.cs` - `_invoiceList.EmptyView`
- `MainPage.cs` - 详情面板 `emptyState`

---

### 7. 弹出对话框粗糙

**问题描述:**
- `DisplayDateRangeDialog()` 使用 `PushModalAsync` 创建临时页面
- 背景色 `#F5F5F5` + 白色圆角卡片，但:
  - 无遮罩层模糊效果 (Backdrop Blur)
  - 无进入动画 (从底部滑入或淡入)
  - 无ESC键关闭支持
- **影响**: 弹窗突兀，与主界面割裂

**涉及文件:**
- `src/InvoiceAI.App/Pages/MainPage.cs` (`DisplayDateRangeDialog()`)

---

### 8. 图片预览体验差

**问题描述:**
- 点击放大后全屏黑色背景 + 图片
- 无缩放 (Pinch-to-Zoom)、旋转、裁剪等交互
- 关闭按钮在右上角，大屏用户点击困难
- 无图片信息展示 (文件名、尺寸、DPI)
- **影响**: 仅能查看原图，无法仔细辨认发票细节

**涉及文件:**
- `src/InvoiceAI.App/Pages/MainPage.cs` (`ShowFullImagePreview()`)

---

## 🟢 轻微问题 / 改进建议

### 9. 图标使用 Emoji 而非矢量图标

**问题描述:**
```csharp
// 当前代码
BuildActionButton("📥 导入", OnImportClicked, ...)
BuildActionButton("📤 导出", OnExportClicked, ...)
BuildActionButton("⚙ 设置", OnSettingsClicked, ...)
BuildActionButton("✅ 确认", OnSaveClicked, ...)
BuildActionButton("🗑 删除", OnDeleteClicked, ...)
```

**问题:**
- Emoji 在 Windows / macOS / Linux 渲染不一致
- 无法控制颜色、大小、旋转动画
- 专业感不足，像聊天软件而非企业工具

**建议:** 使用 Material Icons 字体图标或 SVG 矢量图标

---

### 10. 分类管理界面拥挤

**问题描述:**
- 3 列网格布局 (`GridItemsLayout(3, Horizontal)`)
- 每个分类 Chip 包含文字 + 删除按钮，但:
  - 文字过长时截断 (`LineBreakMode.TailTruncation`)
  - 删除按钮 `✕` 仅 20x20px，点击困难 (无障碍设计不达标)
- **建议:** 增加悬停显示删除按钮，支持键盘 Delete 键删除

---

### 11. 设置页面缺少分组视觉引导

**问题描述:**
- 所有设置项垂直堆叠在 `ScrollView` 中
- 虽然有 `BuildSectionHeader` 标题，但:
  - 缺少分隔线或背景色变化
  - 滚动时无法快速定位到某组设置
- **建议:** 添加左侧锚点导航或分组背景交替色

---

### 12. 状态栏信息过少

**问题描述:**
- 底部 `_statusBar` 仅绑定 `StatusMessage`
- 缺少辅助信息:
  - 发票总数 / 已确认数量
  - 当前过滤条件 (分类、时间范围)
  - 选中数量 (多选时)
- **建议:** 扩展为多列状态栏

---

### 13. 导入进度覆盖层设计简陋

**问题描述:**
- 半透明黑色背景 `#CC000000`
- 居中的 `VerticalStackLayout` 包含进度条 + 文件列表
- 缺少:
  - 取消确认对话框 (防止误触)
  - 剩余时间估算
  - 成功/失败统计摘要
- **建议:** 改为模态对话框，添加详细日志输出

---

### 14. 拖拽区域 (Drop Zone) 缺少动画

**问题描述:**
- 拖入文件时显示虚线框 + 文字提示
- 缺少:
  - 脉冲动画吸引注意 (Pulse Animation)
  - 文件图标跟随鼠标
  - 松开时的成功/失败反馈动画
- **建议:** 添加边框流动动画 + 背景色渐变

---

## 🎨 设计美学问题

### 15. 整体风格偏向"默认模板"

**问题描述:**
- 三栏布局 + Material 蓝 + OpenSans = 典型的 .NET 示例项目风格
- 缺少品牌识别：没有独特的设计语言
- 用户无法一眼认出这是 "InvoiceAI" 而非其他管理后台
- **核心问题:** 没有设计灵魂，像 AI 生成的代码

---

### 16. 缺乏微交互 (Micro-interactions)

**缺失场景:**
- 添加分类时无滑入动画
- 删除发票时无确认动画 (如卡片缩小消失)
- 保存成功时无提示动效 (如 ✅ 弹出 + 渐隐)
- 切换分类时无高亮过渡
- **建议:** 使用 `Animation` API 或 `VisualStateManager` 添加状态转换动画

---

### 17. 暗色主题支持不完整

**问题描述:**
- XAML 的 `Styles.xaml` 定义了 `AppThemeBinding` (Light/Dark 自适应)
- 但 C# 代码全部硬编码浅色颜色:
  ```csharp
  BackgroundColor = Color.FromArgb("#F5F5F5")  // 暗色主题下刺眼
  TextColor = Color.FromArgb("#333")           // 暗色主题下不可见
  ```
- **影响:** 用户切换系统暗色主题后，界面颜色不匹配

---

## 📊 优先级排序

| 优先级 | 问题编号 | 问题描述 | 影响范围 | 预估工作量 |
|--------|----------|----------|----------|------------|
| **P0** | #1 | 配色方案不统一 | 全局 | 2-3 小时 |
| **P0** | #2 | 字体单调无特色 | 全局 | 1-2 小时 |
| **P1** | #3 | 缺少响应式布局 | 主页面 | 3-4 小时 |
| **P1** | #4 | 视觉反馈不足 | 交互组件 | 4-5 小时 |
| **P2** | #5 | 卡片缺少视觉深度 | 列表组件 | 1-2 小时 |
| **P2** | #6 | 空状态设计简陋 | 多处 | 2-3 小时 |
| **P2** | #7 | 弹出对话框粗糙 | 导出/确认 | 2-3 小时 |
| **P2** | #8 | 图片预览体验差 | 详情面板 | 3-4 小时 |
| **P3** | #9 | Emoji 图标不专业 | 全局按钮 | 1-2 小时 |
| **P3** | #11 | 设置页缺少分组引导 | 设置页面 | 1-2 小时 |
| **P3** | #15 | 整体风格像模板 | 全局 | 8-12 小时 |
| **P3** | #17 | 暗色主题不支持 | 全局 | 3-4 小时 |

---

## 🛠 详细修复方案

### 修复方案 #1: 统一配色系统

**目标:** 创建主题资源字典，消除所有硬编码颜色

**步骤:**

#### 1.1 扩展 `Colors.xaml` 添加语义化颜色

```xml
<!-- src/InvoiceAI.App/Resources/Styles/Colors.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <!-- 现有颜色保留 -->
    <Color x:Key="Primary">#512BD4</Color>
    <Color x:Key="PrimaryDark">#ac99ea</Color>
    <Color x:Key="Secondary">#DFD8F7</Color>
    <Color x:Key="Tertiary">#2B0B98</Color>

    <!-- ✨ 新增: 语义化颜色 (品牌色) -->
    <Color x:Key="BrandPrimary">#6C5CE7</Color>       <!-- 紫罗兰主色 -->
    <Color x:Key="BrandPrimaryDark">#4834D4</Color>   <!-- 深紫变体 -->
    <Color x:Key="BrandAccent">#00CEC9</Color>        <!-- 青绿强调色 -->
    <Color x:Key="BrandAccentDark">#00B5B0</Color>

    <!-- ✨ 新增: 功能色 -->
    <Color x:Key="Success">#00B894</Color>            <!-- 成功 (替代 #4CAF50) -->
    <Color x:Key="Error">#FF6B6B</Color>              <!-- 错误 (替代 #F44336) -->
    <Color x:Key="Warning">#FECA57</Color>            <!-- 警告 (替代 #FF9800) -->
    <Color x:Key="Info">#54A0FF</Color>               <!-- 信息提示 -->

    <!-- ✨ 新增: 背景色 (Light 主题) -->
    <Color x:Key="BackgroundPrimary">#F8F9FA</Color>  <!-- 主背景 (替代 #F5F5F5) -->
    <Color x:Key="BackgroundSecondary">#FFFFFF</Color><!-- 卡片背景 -->
    <Color x:Key="BackgroundTertiary">#ECF0F1</Color> <!-- 辅助背景 -->

    <!-- ✨ 新增: 文字颜色 (Light 主题) -->
    <Color x:Key="TextPrimary">#2D3436</Color>        <!-- 主文字 (替代 #333) -->
    <Color x:Key="TextSecondary">#636E72</Color>      <!-- 次要文字 (替代 #666) -->
    <Color x:Key="TextTertiary">#B2BEC3</Color>       <!-- 辅助文字 (替代 #999) -->

    <!-- ✨ 新增: 边框与分割线 -->
    <Color x:Key="BorderLight">#E0E6ED</Color>        <!-- 浅色边框 -->
    <Color x:Key="BorderMedium">#CBD5E0</Color>       <!-- 中等边框 -->
    <Color x:Key="Divider">#F1F3F5</Color>            <!-- 分割线 -->

    <!-- ✨ 新增: 状态色 -->
    <Color x:Key="SelectedBackground">#E8EAF6</Color> <!-- 选中项背景 -->
    <Color x:Key="HoverBackground">#F5F3FF</Color>    <!-- 悬停背景 -->

    <!-- ✨ 新增: Dark 主题颜色 -->
    <Color x:Key="DarkBackgroundPrimary">#1A1A2E</Color>
    <Color x:Key="DarkBackgroundSecondary">#16213E</Color>
    <Color x:Key="DarkBackgroundTertiary">#0F3460</Color>
    <Color x:Key="DarkTextPrimary">#EAEAEA</Color>
    <Color x:Key="DarkTextSecondary">#B0B0B0</Color>
    <Color x:Key="DarkTextTertiary">#707070</Color>
    <Color x:Key="DarkBorder">#2D3748</Color>

    <!-- 渐变画刷 (可选) -->
    <!-- 注意: MAUI 不支持 LinearGradientBrush 直接用在所有控件，需代码处理 -->

</ResourceDictionary>
```

#### 1.2 在 C# 代码中使用资源颜色

```csharp
// 创建辅助方法获取主题颜色
public static class ThemeColors
{
    public static Color GetColor(ResourceDictionary resources, string key)
    {
        if (resources.TryGetValue(key, out var value) && value is Color color)
            return color;
        
        // 回退到硬编码 (防止资源未加载)
        return key switch
        {
            "BrandPrimary" => Color.FromArgb("#6C5CE7"),
            "Success" => Color.FromArgb("#00B894"),
            "Error" => Color.FromArgb("#FF6B6B"),
            _ => Colors.Transparent
        };
    }

    // 便捷属性
    public static Color BrandPrimary => GetCurrentThemeColor("BrandPrimary");
    public static Color Success => GetCurrentThemeColor("Success");
    public static Color Error => GetCurrentThemeColor("Error");
    public static Color BackgroundPrimary => GetCurrentThemeColor("BackgroundPrimary");
    public static Color TextPrimary => GetCurrentThemeColor("TextPrimary");
    public static Color TextSecondary => GetCurrentThemeColor("TextSecondary");

    private static Color GetCurrentThemeColor(string key)
    {
        // 从 Application.Current.Resources 获取
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true 
            && value is Color color)
            return color;
        
        // 回退值
        return key switch
        {
            "BrandPrimary" => Color.FromArgb("#6C5CE7"),
            "BackgroundPrimary" => Color.FromArgb("#F8F9FA"),
            "TextPrimary" => Color.FromArgb("#2D3436"),
            _ => Colors.Transparent
        };
    }
}
```

#### 1.3 替换 MainPage.cs 中的硬编码颜色

```csharp
// 修改前 ❌
private void BuildUI()
{
    BackgroundColor = Color.FromArgb("#F5F5F5");
    _busyIndicator.Color = Color.FromArgb("#1976D2");
}

private static View BuildTitleBar()
{
    return new Border
    {
        BackgroundColor = Color.FromArgb("#1976D2"),
        Content = new Label
        {
            TextColor = Colors.White,
        }
    };
}

// 修改后 ✅
private void BuildUI()
{
    this.SetBinding(BackgroundColorProperty, new Binding("BackgroundColor", 
        source: RelativeBindingSource.Self, 
        converter: new ThemeAwareBackgroundConverter()));
    
    _busyIndicator.Color = ThemeColors.BrandPrimary;
}

private static View BuildTitleBar()
{
    var titleBar = new Border();
    
    // 使用 AppThemeBinding 实现自动主题切换
    titleBar.SetBinding(BackgroundColorProperty, 
        new AppThemeBinding
        {
            Light = ThemeColors.BrandPrimary,
            Dark = ThemeColors.GetColor(Application.Current.Resources, "DarkBackgroundTertiary")
        });

    return titleBar;
}
```

**验证清单:**
- [ ] 搜索所有 `.cs` 文件中的 `Color.FromArgb`
- [ ] 替换为 `ThemeColors.*` 引用
- [ ] 测试 Light/Dark 主题切换
- [ ] 确保颜色对比度符合 WCAG AA 标准 (≥ 4.5:1)

---

### 修复方案 #2: 引入字体配对系统

**目标:** 使用多字体组合创造视觉层次，提升品牌识别度

**步骤:**

#### 2.1 下载并添加新字体

推荐字体配对方案 (全部免费开源):

| 用途 | 字体 | 来源 | 文件 |
|------|------|------|------|
| **标题/品牌** | Poppins (Bold/SemiBold) | Google Fonts | `Poppins-Bold.ttf`, `Poppins-SemiBold.ttf` |
| **中文正文** | Noto Sans SC (Regular/Medium) | Google Fonts | `NotoSansSC-Regular.ttf`, `NotoSansSC-Medium.ttf` |
| **数字/金额** | JetBrains Mono (Medium) | JetBrains | `JetBrainsMono-Medium.ttf` |
| **辅助文字** | Inter (Regular) | Google Fonts | `Inter-Regular.ttf` |

**下载方式:**
```bash
# 创建字体目录
mkdir -p src/InvoiceAI.App/Resources/Fonts

# 下载字体 (手动从 Google Fonts 下载 TTF 文件)
# Poppins: https://fonts.google.com/specimen/Poppins
# Noto Sans SC: https://fonts.google.com/noto/specimen/Noto+Sans+SC
# JetBrains Mono: https://www.jetbrains.com/lp/mono/
# Inter: https://fonts.google.com/specimen/Inter
```

#### 2.2 注册字体到 MauiProgram.cs

```csharp
// src/InvoiceAI.App/MauiProgram.cs
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .ConfigureFonts(fonts =>
        {
            // 现有字体
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

            // ✨ 新增: 标题字体 (Poppins)
            fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
            fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemiBold");

            // ✨ 新增: 中文字体 (Noto Sans SC)
            fonts.AddFont("NotoSansSC-Regular.ttf", "NotoSansSCRegular");
            fonts.AddFont("NotoSansSC-Medium.ttf", "NotoSansSCMedium");

            // ✨ 新增: 数字字体 (JetBrains Mono)
            fonts.AddFont("JetBrainsMono-Medium.ttf", "JetBrainsMonoMedium");

            // ✨ 新增: 辅助字体 (Inter)
            fonts.AddFont("Inter-Regular.ttf", "InterRegular");
        });

    // ... 其他配置
    return builder.Build();
}
```

#### 2.3 更新 Styles.xaml 定义字体样式

```xml
<!-- src/InvoiceAI.App/Resources/Styles/Styles.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

    <!-- ✨ 新增: 标题样式 -->
    <Style x:Key="BrandTitle" TargetType="Label">
        <Setter Property="FontFamily" Value="PoppinsBold" />
        <Setter Property="FontSize" Value="20" />
        <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource BrandPrimary}, Dark={StaticResource White}}" />
        <Setter Property="LetterSpacing" Value="0.5" />
    </Style>

    <Style x:Key="SectionHeader" TargetType="Label">
        <Setter Property="FontFamily" Value="PoppinsSemiBold" />
        <Setter Property="FontSize" Value="16" />
        <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
    </Style>

    <!-- ✨ 新增: 正文样式 -->
    <Style x:Key="BodyText" TargetType="Label">
        <Setter Property="FontFamily" Value="NotoSansSCRegular" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
    </Style>

    <Style x:Key="BodyTextSecondary" TargetType="Label">
        <Setter Property="FontFamily" Value="NotoSansSCRegular" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextSecondary}, Dark={StaticResource DarkTextSecondary}}" />
    </Style>

    <!-- ✨ 新增: 金额/数字样式 -->
    <Style x:Key="AmountDisplay" TargetType="Label">
        <Setter Property="FontFamily" Value="JetBrainsMonoMedium" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource BrandPrimary}, Dark={StaticResource White}}" />
        <Setter Property="FontAttributes" Value="Bold" />
    </Style>

    <Style x:Key="AmountLarge" TargetType="Label">
        <Setter Property="FontFamily" Value="JetBrainsMonoMedium" />
        <Setter Property="FontSize" Value="18" />
        <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
        <Setter Property="FontAttributes" Value="Bold" />
    </Style>

    <!-- 修改默认 Label 样式 -->
    <Style TargetType="Label">
        <Setter Property="FontFamily" Value="NotoSansSCRegular" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
    </Style>

    <!-- 修改默认 Button 样式 -->
    <Style TargetType="Button">
        <Setter Property="FontFamily" Value="NotoSansSCMedium" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="FontAttributes" Value="Bold" />
    </Style>

</ResourceDictionary>
```

#### 2.4 在代码中应用字体样式

```csharp
// 修改前 ❌
var titleLabel = new Label
{
    Text = "InvoiceAI",
    FontSize = 18,
    FontAttributes = FontAttributes.Bold,
    TextColor = Colors.White,
};

// 修改后 ✅
var titleLabel = new Label
{
    Text = "InvoiceAI",
    Style = Application.Current.Resources["BrandTitle"] as Style,
};

// 金额显示
var amountLabel = new Label
{
    Text = $"¥{invoice.TaxIncludedAmount:N0}",
    Style = Application.Current.Resources["AmountDisplay"] as Style,
};

// 次要文字
var dateLabel = new Label
{
    Text = invoice.TransactionDate.ToString("yyyy-MM-dd"),
    Style = Application.Current.Resources["BodyTextSecondary"] as Style,
};
```

---

### 修复方案 #3: 添加响应式布局支持

**目标:** 使三栏布局能够自适应窗口缩放

**步骤:**

#### 3.1 使用 GridLength.Auto 和 Star 替代固定宽度

```csharp
// 修改前 ❌
Content = new Grid
{
    RowDefinitions = { ... },
    ColumnDefinitions =
    {
        new ColumnDefinition(180),    // 固定 180px
        new ColumnDefinition(280),    // 固定 280px
        new ColumnDefinition(new GridLength(1, GridUnitType.Star))
    },
    Children = { ... }
};

// 修改后 ✅
Content = new Grid
{
    RowDefinitions =
    {
        new RowDefinition(new GridLength(1, GridUnitType.Auto)),
        new RowDefinition(new GridLength(1, GridUnitType.Star)),
        new RowDefinition(new GridLength(1, GridUnitType.Auto))
    },
    ColumnDefinitions =
    {
        // 分类面板: 最小 150px，最大 200px
        new ColumnDefinition(new GridLength(1, GridUnitType.Auto)) 
            { MinWidth = 150, MaxWidth = 200 },
        
        // 列表面板: 最小 250px，优先分配空间
        new ColumnDefinition(new GridLength(1.2, GridUnitType.Star))
            { MinWidth = 250 },
        
        // 详情面板: 占据剩余空间
        new ColumnDefinition(new GridLength(1.5, GridUnitType.Star))
    },
    Children = { ... }
};
```

#### 3.2 添加窗口大小变化监听

```csharp
// src/InvoiceAI.App/Pages/MainPage.cs
public class MainPage : ContentPage
{
    private double _lastWidth;
    
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        
        // 检测窗口大小变化
        if (Math.Abs(width - _lastWidth) > 10) // 阈值 10px 防止抖动
        {
            _lastWidth = width;
            UpdateLayoutForWindowSize(width);
        }
    }

    private void UpdateLayoutForWindowSize(double width)
    {
        if (width < 900)
        {
            // 小屏幕: 隐藏分类面板，仅显示列表 + 详情
            // 可通过汉堡菜单切换
            SetCompactLayout();
        }
        else if (width < 1200)
        {
            // 中等屏幕: 标准三栏
            SetStandardLayout();
        }
        else
        {
            // 大屏幕: 增加列表和详情面板宽度
            SetExpandedLayout();
        }
    }

    private void SetCompactLayout()
    {
        // 隐藏分类面板 (Column 0)
        // 详情面板改为弹窗
    }

    private void SetStandardLayout()
    {
        // 默认三栏
    }

    private void SetExpandedLayout()
    {
        // 增加 ColumnDefinitions 的宽度
    }
}
```

#### 3.3 使用 ScrollView 防止内容溢出

```csharp
// 确保所有面板内容可滚动
var categoryPanel = new Border
{
    Content = new ScrollView
    {
        Content = new Grid { ... }
    }
};

var listPanel = new Border
{
    Content = new Grid
    {
        RowDefinitions =
        {
            new RowDefinition(GridLength.Auto),  // 搜索栏
            new RowDefinition(GridLength.Star)   // 可滚动列表
        },
        Children =
        {
            searchBar.Row(0),
            new ScrollView 
            { 
                Content = _invoiceList 
            }.Row(1)
        }
    }
};
```

---

### 修复方案 #4: 增强视觉反馈

**目标:** 为交互添加动画和状态过渡

#### 4.1 添加按钮悬停和点击效果

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

    // ✨ 新增: 视觉状态管理器
    VisualStateManager.SetVisualStateGroups(btn, new VisualStateGroupList
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
                        new Setter { Property = Button.BackgroundColorProperty, Value = bgColor },
                        new Setter { Property = Button.ScaleProperty, Value = 1.0 }
                    }
                },
                new VisualState
                {
                    Name = "PointerOver",
                    Setters =
                    {
                        // 悬停时变亮 10%
                        new Setter { Property = Button.BackgroundColorProperty, Value = bgColor.MultiplyAlpha(0.9f) },
                        new Setter { Property = Button.ScaleProperty, Value = 1.05 }
                    }
                },
                new VisualState
                {
                    Name = "Pressed",
                    Setters =
                    {
                        // 按下时缩小
                        new Setter { Property = Button.BackgroundColorProperty, Value = bgColor.MultiplyAlpha(0.8f) },
                        new Setter { Property = Button.ScaleProperty, Value = 0.95 }
                    }
                },
                new VisualState
                {
                    Name = "Disabled",
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = Color.FromArgb("#CCCCCC") },
                        new Setter { Property = Button.OpacityProperty, Value = 0.6 }
                    }
                }
            }
        }
    });

    btn.Clicked += onClick;
    return btn;
}
```

#### 4.2 添加列表项选中动画

```csharp
// 在 BuildInvoiceListPanel() 的 ItemTemplate 中
var border = new Border { ... };

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
                    new Setter { Property = Border.BackgroundColorProperty, Value = Colors.White },
                    new Setter { Property = Border.StrokeProperty, Value = ThemeColors.BorderLight },
                    new Setter { Property = Border.ShadowProperty, Value = null }
                }
            },
            new VisualState
            {
                Name = "PointerOver",
                Setters =
                {
                    new Setter { Property = Border.BackgroundColorProperty, Value = ThemeColors.HoverBackground },
                    new Setter { Property = Border.StrokeProperty, Value = ThemeColors.BorderMedium },
                    new Setter 
                    { 
                        Property = Border.ShadowProperty, 
                        Value = new Shadow 
                        { 
                            Brush = Colors.Black.MultiplyAlpha(0.1f),
                            Offset = new Point(0, 2),
                            Radius = 8,
                            Opacity = 0.5f
                        }
                    }
                }
            },
            new VisualState
            {
                Name = "Selected",
                Setters =
                {
                    new Setter { Property = Border.BackgroundColorProperty, Value = ThemeColors.SelectedBackground },
                    new Setter { Property = Border.StrokeProperty, Value = ThemeColors.BrandPrimary },
                    new Setter { Property = Border.StrokeThicknessProperty, Value = 2.0 },
                    new Setter 
                    { 
                        Property = Border.ShadowProperty, 
                        Value = new Shadow 
                        { 
                            Brush = ThemeColors.BrandPrimary.MultiplyAlpha(0.3f),
                            Offset = new Point(0, 4),
                            Radius = 12,
                            Opacity = 0.6f
                        }
                    }
                }
            }
        }
    }
});
```

#### 4.3 添加加载骨架屏

```csharp
// 创建骨架屏组件
private static View BuildSkeletonLoader()
{
    var skeletonItem = new VerticalStackLayout
    {
        Spacing = 8,
        Padding = new Thickness(12, 8),
        Children =
        {
            // 标题占位
            new BoxView
            {
                HeightRequest = 16,
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                CornerRadius = 4
            },
            // 副标题占位
            new BoxView
            {
                HeightRequest = 12,
                WidthRequest = 200,
                BackgroundColor = Color.FromArgb("#F0F0F0"),
                CornerRadius = 3
            },
            // 金额占位
            new BoxView
            {
                HeightRequest = 14,
                WidthRequest = 100,
                BackgroundColor = Color.FromArgb("#E8E8E8"),
                CornerRadius = 3,
                HorizontalOptions = LayoutOptions.End
            }
        }
    };

    // 添加到 CollectionView.EmptyView
    return new VerticalStackLayout
    {
        Spacing = 4,
        Padding = new Thickness(8, 4),
        Children = Enumerable.Range(0, 5).Select(_ => skeletonItem).ToList()
    };
}

// 在 LoadDataCommand 执行时显示骨架屏
_invoiceList.EmptyView = _vm.IsBusy ? BuildSkeletonLoader() : BuildEmptyState();
```

---

### 修复方案 #5: 改进卡片视觉深度

**目标:** 使用阴影创造层级关系

```csharp
// 修改发票列表卡片
var border = new Border
{
    BackgroundColor = Colors.White,
    Margin = new Thickness(8, 4),
    Padding = new Thickness(12, 8),
    StrokeShape = new RoundRectangle { CornerRadius = 8 },
    StrokeThickness = 1,
    Stroke = ThemeColors.BorderLight,
    MinimumHeightRequest = 72,
    Content = contentWithBadge,
    
    // ✨ 新增: 阴影
    Shadow = new Shadow
    {
        Brush = Colors.Black.MultiplyAlpha(0.08f),
        Offset = new Point(0, 2),
        Radius = 8,
        Opacity = 0.5f
    }
};

// 悬停时增强阴影 (在 VisualStateManager 中)
new VisualState
{
    Name = "PointerOver",
    Setters =
    {
        new Setter 
        { 
            Property = Border.ShadowProperty, 
            Value = new Shadow
            {
                Brush = Colors.Black.MultiplyAlpha(0.15f),
                Offset = new Point(0, 4),
                Radius = 12,
                Opacity = 0.7f
            }
        }
    }
}
```

---

### 修复方案 #6: 设计精美的空状态

**目标:** 为空状态添加引导性插图和 CTA 按钮

```csharp
private static View BuildEmptyState(string title, string subtitle, string actionText, Command? actionCommand = null)
{
    var actionBtn = actionCommand != null 
        ? new Button
        {
            Text = actionText,
            BackgroundColor = ThemeColors.BrandPrimary,
            TextColor = Colors.White,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(24, 12),
            MinimumHeightRequest = 44,
            CornerRadius = 8,
            Command = actionCommand
        }
        : null;

    return new VerticalStackLayout
    {
        Padding = new Thickness(40, 60),
        Spacing = 16,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        Children =
        {
            // ✨ 使用 ASCII 艺术或简单图形替代插图
            new Label
            {
                Text = "📄",
                FontSize = 64,
                HorizontalOptions = LayoutOptions.Center,
                Opacity = 0.3
            },
            
            new Label
            {
                Text = title,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = ThemeColors.TextSecondary,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            },
            
            new Label
            {
                Text = subtitle,
                FontSize = 14,
                TextColor = ThemeColors.TextTertiary,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap,
                WidthRequest = 300
            },
            
            // 可选的行动按钮
            actionBtn ?? new BoxView { HeightRequest = 0 }
        }
    };
}

// 使用示例
_invoiceList.EmptyView = BuildEmptyState(
    title: "暂无发票记录",
    subtitle: "点击左下角「导入」按钮添加您的第一张发票\n支持 JPG、PNG、PDF 格式",
    actionText: "📥 导入发票",
    actionCommand: _vm.ImportCommand
);
```

---

### 修复方案 #7: 改进弹出对话框

**目标:** 添加模糊遮罩和滑入动画

```csharp
private async Task<(DateTime? Start, DateTime? End, int ConfirmedFilter)?> DisplayDateRangeDialog()
{
    var tcs = new TaskCompletionSource<(DateTime? Start, DateTime? End, int ConfirmedFilter)?>();

    // 构建对话框内容 (省略控件定义)
    var layout = new VerticalStackLayout { ... };

    ContentPage? popupPage = null;

    // ✨ 新增: 遮罩层 (半透明黑色)
    var backdrop = new BoxView
    {
        BackgroundColor = Colors.Black.MultiplyAlpha(0.5f),
        Opacity = 0,
        InputTransparent = false
    };

    // 对话框容器 (居中)
    var dialogContainer = new Border
    {
        BackgroundColor = Colors.White,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        StrokeThickness = 0,
        Padding = new Thickness(0),
        Shadow = new Shadow
        {
            Brush = Colors.Black.MultiplyAlpha(0.3f),
            Offset = new Point(0, 8),
            Radius = 24,
            Opacity = 0.8f
        },
        Content = new ScrollView { Content = layout },
        WidthRequest = 400,
        MaximumHeightRequest = 600,
        Scale = 0.8,
        Opacity = 0
    };

    // 关闭函数
    async Task CloseDialog()
    {
        // 退出动画
        await Task.WhenAll(
            backdrop.FadeTo(0, 200, Easing.CubicIn),
            dialogContainer.ScaleTo(0.8, 200, Easing.CubicIn),
            dialogContainer.FadeTo(0, 200, Easing.CubicIn)
        );
        
        if (popupPage != null)
            await popupPage.Navigation.PopModalAsync();
    }

    // 导出按钮
    exportBtn.Clicked += async (s, e) =>
    {
        // ... 收集数据
        tcs.SetResult((start, end, confirmedFilter));
        await CloseDialog();
    };

    // 取消按钮
    cancelBtn.Clicked += async (s, e) =>
    {
        tcs.SetResult(null);
        await CloseDialog();
    };

    popupPage = new ContentPage
    {
        BackgroundColor = Colors.Transparent,
        Content = new Grid
        {
            BackgroundColor = Colors.Transparent,
            Children =
            {
                backdrop,
                dialogContainer
            }
        }
    };

    await Navigation.PushModalAsync(popupPage);

    // ✨ 新增: 进入动画
    await Task.WhenAll(
        backdrop.FadeTo(1, 250, Easing.CubicOut),
        dialogContainer.ScaleTo(1, 250, Easing.CubicOut),
        dialogContainer.FadeTo(1, 250, Easing.CubicOut)
    );

    return await tcs.Task;
}
```

---

### 修复方案 #8: 增强图片预览

**目标:** 添加缩放、旋转、图片信息

```csharp
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

    // 图片控件 (支持缩放)
    var previewImage = new Image
    {
        Source = ImageSource.FromFile(imagePath),
        Aspect = Aspect.AspectFit,
        BackgroundColor = Color.FromArgb("#1A1A1A"),
        Scale = 1.0
    };

    // 缩放控制
    var zoomSlider = new Slider(0.5, 3.0, 1.0)
    {
        WidthRequest = 200,
        HorizontalOptions = LayoutOptions.Center
    };
    zoomSlider.ValueChanged += (s, e) => previewImage.Scale = e.NewValue;

    // 旋转按钮
    double currentRotation = 0;
    var rotateBtn = new Button
    {
        Text = "🔄 旋转",
        BackgroundColor = Color.FromArgb("#333"),
        TextColor = Colors.White,
        FontSize = 12,
        WidthRequest = 80,
        MinimumHeightRequest = 36
    };
    rotateBtn.Clicked += (s, e) =>
    {
        currentRotation += 90;
        previewImage.RotateTo(currentRotation, 300, Easing.CubicOut);
    };

    // 信息标签
    var fileInfo = new FileInfo(imagePath);
    var infoLabel = new Label
    {
        Text = $"📷 {Path.GetFileName(imagePath)} | {fileInfo.Length / 1024} KB",
        FontSize = 12,
        TextColor = Color.FromArgb("#CCC"),
        HorizontalOptions = LayoutOptions.Start
    };

    // 关闭按钮
    var closeBtn = new Button
    {
        Text = "✕ 关闭",
        BackgroundColor = Color.FromArgb("#F44336"),
        TextColor = Colors.White,
        FontSize = 12,
        WidthRequest = 80,
        MinimumHeightRequest = 36
    };

    // 底部工具栏
    var toolbar = new HorizontalStackLayout
    {
        Spacing = 12,
        Padding = new Thickness(16, 12),
        BackgroundColor = Color.FromArgb("#2D2D2D"),
        Children =
        {
            infoLabel,
            new BoxView { WidthRequest = 1, BackgroundColor = Color.FromArgb("#555") },
            zoomSlider,
            new BoxView { WidthRequest = 1, BackgroundColor = Color.FromArgb("#555") },
            rotateBtn,
            new Label { Text = " ", HorizontalOptions = LayoutOptions.Fill },
            closeBtn
        }
    };

    var popupPage = new ContentPage
    {
        Title = "发票预览",
        BackgroundColor = Colors.Black,
        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(1, GridUnitType.Auto))
            },
            Children =
            {
                previewImage.Row(0),
                toolbar.Row(1)
            }
        }
    };

    closeBtn.Clicked += async (s, e) => await popupPage.Navigation.PopModalAsync();

    await Navigation.PushModalAsync(popupPage);
}
```

---

### 修复方案 #9: 替换 Emoji 为矢量图标

**目标:** 使用字体图标替代 Emoji

#### 9.1 添加 Material Icons 字体

```bash
# 下载 Material Icons
# https://fonts.google.com/icons
# 下载 TTF 文件到: src/InvoiceAI.App/Resources/Fonts/MaterialIcons-Regular.ttf
```

#### 9.2 注册字体图标

```csharp
// MauiProgram.cs
builder.ConfigureFonts(fonts =>
{
    // ... 其他字体
    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
});
```

#### 9.3 创建图标常量类

```csharp
public static class MaterialIcons
{
    public const string Import = "\uE2C6";   // file_upload
    public const string Export = "\uE2D4";   // file_download
    public const string Settings = "\uE8B8"; // settings
    public const string Check = "\uE5CA";    // check_box
    public const string Delete = "\uE872";   // delete
    public const string Search = "\uE8B6";   // search
    public const string Filter = "\uE152";   // filter_list
    public const string Folder = "\uE2C7";   // folder_open
    public const string Image = "\uE3F4";    // image
    public const string Close = "\uE5CD";    // close
    public const string Rotate = "\uE832";   // rotate_right
}
```

#### 9.4 使用图标

```csharp
// 修改前 ❌
BuildActionButton("📥 导入", OnImportClicked, ...)

// 修改后 ✅
BuildActionButtonWithIcon($"{MaterialIcons.Import} 导入", OnImportClicked, ...)

private static Button BuildActionButtonWithIcon(string text, EventHandler onClick, Color bgColor)
{
    var btn = new Button
    {
        Text = text,
        FontFamily = "NotoSansSCMedium", // 按钮文字使用常规字体
        BackgroundColor = bgColor,
        TextColor = Colors.White,
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        Padding = new Thickness(12, 6),
        MinimumHeightRequest = 36
    };

    // 为图标单独设置字体 (通过 Span 实现混合字体)
    // 注意: MAUI Button 不支持混合字体，需要使用 HorizontalStackLayout 替代
    // 或者使用图标字体作为按钮文字，设置 FontFamily = "MaterialIcons"
    
    btn.Clicked += onClick;
    return btn;
}

// 替代方案: 使用 StackLayout 实现图文按钮
private static View BuildIconButton(string icon, string text, EventHandler onClick, Color bgColor)
{
    var iconLabel = new Label
    {
        Text = icon,
        FontFamily = "MaterialIcons",
        FontSize = 18,
        TextColor = Colors.White,
        VerticalOptions = LayoutOptions.Center,
        WidthRequest = 24
    };

    var textLabel = new Label
    {
        Text = text,
        FontFamily = "NotoSansSCMedium",
        FontSize = 13,
        TextColor = Colors.White,
        FontAttributes = FontAttributes.Bold,
        VerticalOptions = LayoutOptions.Center
    };

    var content = new HorizontalStackLayout
    {
        Spacing = 6,
        Children = { iconLabel, textLabel }
    };

    var border = new Border
    {
        BackgroundColor = bgColor,
        StrokeShape = new RoundRectangle { CornerRadius = 6 },
        StrokeThickness = 0,
        Padding = new Thickness(12, 8),
        Content = content
    };

    var tapGesture = new TapGestureRecognizer();
    tapGesture.Tapped += onClick;
    border.GestureRecognizers.Add(tapGesture);

    return border;
}
```

---

### 修复方案 #17: 实现完整的暗色主题

**目标:** 移除所有硬编码，使用主题感知颜色

#### 17.1 创建主题助手类

```csharp
public static class ThemeManager
{
    public static bool IsDarkTheme => Application.Current!.RequestedTheme == AppTheme.Dark;

    public static Color GetThemeColor(string lightColorHex, string darkColorHex)
    {
        return IsDarkTheme 
            ? Color.FromArgb(darkColorHex) 
            : Color.FromArgb(lightColorHex);
    }

    // 便捷方法
    public static Color Background => GetThemeColor("#F8F9FA", "#1A1A2E");
    public static Color CardBackground => GetThemeColor("#FFFFFF", "#16213E");
    public static Color TextPrimary => GetThemeColor("#2D3436", "#EAEAEA");
    public static Color TextSecondary => GetThemeColor("#636E72", "#B0B0B0");
    public static Color Border => GetThemeColor("#E0E6ED", "#2D3748");
    public static Color SelectedBackground => GetThemeColor("#E8EAF6", "#1E3A5F");
}
```

#### 17.2 监听主题变化

```csharp
// App.cs
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        
        // 监听系统主题变化
        RequestedThemeChanged += (s, e) =>
        {
            // 通知所有页面更新颜色
            MainPage?.OnThemeChanged();
        };
    }
}

// MainPage.cs
public partial class MainPage : ContentPage
{
    public void OnThemeChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // 更新背景色
            BackgroundColor = ThemeManager.Background;
            
            // 触发 UI 重新构建 (或通过 Binding 自动更新)
            // 如果使用 Binding，则无需手动更新
        });
    }
}
```

#### 17.3 使用 Binding 替代硬编码

```csharp
// 最佳实践: 为所有颜色创建 ViewModel 属性
public class ThemeViewModel : INotifyPropertyChanged
{
    public Color BackgroundColor => ThemeManager.Background;
    public Color CardBackgroundColor => ThemeManager.CardBackground;
    public Color PrimaryTextColor => ThemeManager.TextPrimary;
    public Color SecondaryTextColor => ThemeManager.TextSecondary;
    public Color BorderColor => ThemeManager.Border;

    public void Refresh()
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(CardBackgroundColor));
        OnPropertyChanged(nameof(PrimaryTextColor));
        OnPropertyChanged(nameof(SecondaryTextColor));
        OnPropertyChanged(nameof(BorderColor));
    }
}

// 在 XAML 或代码中 Binding
label.SetBinding(Label.TextColorProperty, nameof(ThemeViewModel.PrimaryTextColor));
border.SetBinding(Border.BackgroundColorProperty, nameof(ThemeViewModel.CardBackgroundColor));
```

---

## 📝 实施路线图

### Phase 1: 基础修复 (1-2 天)
- [ ] 统一配色系统 (方案 #1)
- [ ] 引入字体配对 (方案 #2)
- [ ] 实现暗色主题 (方案 #17)

### Phase 2: 交互增强 (2-3 天)
- [ ] 添加视觉反馈 (方案 #4)
- [ ] 改进卡片深度 (方案 #5)
- [ ] 替换 Emoji 图标 (方案 #9)

### Phase 3: 体验优化 (2-3 天)
- [ ] 响应式布局 (方案 #3)
- [ ] 空状态设计 (方案 #6)
- [ ] 弹出对话框 (方案 #7)
- [ ] 图片预览增强 (方案 #8)

### Phase 4: 品牌塑造 (3-5 天)
- [ ] 设计独特视觉语言
- [ ] 添加微交互动画
- [ ] 用户测试与迭代

---

## 🔗 相关资源

- [.NET MAUI 文档](https://learn.microsoft.com/zh-cn/dotnet/maui/)
- [CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui)
- [Material Design 配色](https://m2.material.io/design/color/)
- [Google Fonts](https://fonts.google.com/)
- [WCAG 颜色对比度标准](https://www.w3.org/TR/WCAG21/#contrast-minimum)

---

*文档版本: v1.0 | 最后更新: 2026-04-08*
