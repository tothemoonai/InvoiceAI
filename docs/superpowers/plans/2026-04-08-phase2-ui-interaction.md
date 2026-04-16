# Phase 2 UI 改进实施计划：响应式布局 + 视觉反馈

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 为 MainPage 添加响应式断点布局和按钮/列表项视觉反馈动画。

**Architecture:** 通过 OnSizeAllocated 监听窗口大小变化，动态切换 Grid ColumnDefinitions 和组件可见性；通过 VisualStateManager 添加悬停/按下/选中的视觉状态和动画。

**Tech Stack:** .NET MAUI 9, VisualStateManager, Animation API, Grid

---

### Task 1: 响应式布局断点切换

**Files:**
- Modify: `src/InvoiceAI.App/Pages/MainPage.cs`

- [ ] **Step 1: 在 MainPage 类中添加布局状态字段**

在现有字段声明后添加:

```csharp
private LayoutMode _layoutMode = LayoutMode.Standard;
private Button _categoryToggleButton = null!;

private enum LayoutMode { Expanded, Standard, Compact }
```

- [ ] **Step 2: 添加 OnSizeAllocated 重写**

在 MainPage 类中添加（WireEvents 方法之后）:

```csharp
protected override void OnSizeAllocated(double width, double height)
{
    base.OnSizeAllocated(width, height);

    var newMode = width > 1200 ? LayoutMode.Expanded
                  : width >= 900 ? LayoutMode.Standard
                  : LayoutMode.Compact;

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
            SetCategoryPanelVisible(true);
            SetDetailPanelVisible(true);
            break;

        case LayoutMode.Standard:
            grid.ColumnDefinitions[0] = new ColumnDefinition(150);
            grid.ColumnDefinitions[1] = new ColumnDefinition(250);
            grid.ColumnDefinitions[2] = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
            SetCategoryPanelVisible(true);
            SetDetailPanelVisible(true);
            break;

        case LayoutMode.Compact:
            grid.ColumnDefinitions[0] = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
            grid.ColumnDefinitions[1] = new ColumnDefinition(0);
            grid.ColumnDefinitions[2] = new ColumnDefinition(0);
            SetCategoryPanelVisible(false);
            SetDetailPanelVisible(true); // 详情通过按钮/选择切换
            break;
    }
}

private void SetCategoryPanelVisible(bool visible)
{
    // 找到 Grid 中 Column 0 的子元素 (分类面板)
    foreach (var child in (Content as Grid)?.Children ?? [])
    {
        if (child is View v && Grid.GetColumn(v) == 0 && Grid.GetRow(v) == 1)
        {
            v.IsVisible = visible;
        }
    }
    _categoryToggleButton.IsVisible = !visible;
}

private void SetDetailPanelVisible(bool visible)
{
    foreach (var child in (Content as Grid)?.Children ?? [])
    {
        if (child is View v && Grid.GetColumn(v) == 2 && Grid.GetRow(v) == 1)
        {
            v.IsVisible = visible;
        }
    }
}
```

- [ ] **Step 3: 在 BuildUI 的 Grid 中添加分类切换按钮**

在 Grid 的 Children 中添加:

```csharp
// 分类切换按钮 (Compact 模式下显示)
_categoryToggleButton = new Button
{
    Text = "☰",
    FontSize = 20,
    BackgroundColor = ThemeManager.BrandPrimary,
    TextColor = Colors.White,
    WidthRequest = 40,
    HeightRequest = 40,
    CornerRadius = 20,
    Padding = new Thickness(0),
    HorizontalOptions = LayoutOptions.Start,
    VerticalOptions = LayoutOptions.Start,
    Margin = new Thickness(8, 8, 0, 0),
    IsVisible = false
};
_categoryToggleButton.Clicked += (s, e) =>
{
    // 切换分类面板显示
    var catPanel = (Content as Grid)?.Children
        .OfType<View>()
        .FirstOrDefault(c => Grid.GetColumn(c) == 0 && Grid.GetRow(c) == 1);
    if (catPanel != null)
    {
        catPanel.IsVisible = !catPanel.IsVisible;
    }
};

// 添加到 Grid
Children =
{
    BuildTitleBar().Row(0).ColumnSpan(3),
    _categoryToggleButton.Row(1).Column(0),
    BuildCategoryPanel().Row(1).Column(0),
    // ... 其余不变
}
```

- [ ] **Step 4: 修改 BuildUI 的 Grid ColumnDefinitions 为动态**

将固定 ColumnDefinitions 改为标准模式默认值:

```csharp
ColumnDefinitions =
{
    new ColumnDefinition(150),  // Standard 默认
    new ColumnDefinition(250),
    new ColumnDefinition(new GridLength(1, GridUnitType.Star))
},
```

- [ ] **Step 5: 编译验证**

```bash
dotnet build C:\ClaudeCodeProject\InvoiceAI\src\InvoiceAI.App\InvoiceAI.App.csproj
```

- [ ] **Step 6: Commit**

```bash
cd C:\ClaudeCodeProject\InvoiceAI
git add src/InvoiceAI.App/Pages/MainPage.cs
git commit -m "feat: add responsive layout breakpoints (expanded/standard/compact)"
```

---

### Task 2: 按钮悬停/按下视觉反馈

**Files:**
- Modify: `src/InvoiceAI.App/Pages/MainPage.cs` (BuildActionButton)
- Modify: `src/InvoiceAI.App/Pages/SettingsPage.cs` (按钮)

- [ ] **Step 1: 修改 BuildActionButton 添加 VisualStateManager**

替换 MainPage.cs 中的 `BuildActionButton` 方法:

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
        MinimumHeightRequest = 36,
        CornerRadius = 6
    };

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
                        new Setter { Property = Button.ScaleProperty, Value = 1.0 },
                        new Setter { Property = Button.OpacityProperty, Value = 1.0 }
                    }
                },
                new VisualState
                {
                    Name = "PointerOver",
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = bgColor.WithAlpha(0.9f) },
                        new Setter { Property = Button.ScaleProperty, Value = 1.03 },
                        new Setter { Property = Button.ShadowProperty, Value = new Shadow
                        {
                            Brush = Colors.Black.WithAlpha(0.15f),
                            Offset = new Point(0, 2),
                            Radius = 6,
                            Opacity = 0.4f
                        }}
                    }
                },
                new VisualState
                {
                    Name = "Pressed",
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = bgColor.WithAlpha(0.8f) },
                        new Setter { Property = Button.ScaleProperty, Value = 0.97 },
                        new Setter { Property = Button.ShadowProperty, Value = null }
                    }
                },
                new VisualState
                {
                    Name = "Disabled",
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = ThemeManager.BorderLight },
                        new Setter { Property = Button.OpacityProperty, Value = 0.5 }
                    }
                }
            }
        }
    });

    btn.Clicked += onClick;
    return btn;
}
```

- [ ] **Step 2: 为 SettingsPage 的按钮添加相同效果**

SettingsPage 中的按钮也使用相同的 VisualStateManager 模式。可以提取为一个静态辅助方法，或者在 SettingsPage 中也创建一个类似的 BuildActionButton 方法。

- [ ] **Step 3: 编译验证**

```bash
dotnet build C:\ClaudeCodeProject\InvoiceAI\src\InvoiceAI.App\InvoiceAI.App.csproj
```

- [ ] **Step 4: Commit**

```bash
cd C:\ClaudeCodeProject\InvoiceAI
git add src/InvoiceAI.App/Pages/MainPage.cs src/InvoiceAI.App/Pages/SettingsPage.cs
git commit -m "feat: add hover/press visual states to action buttons"
```

---

### Task 3: 列表项悬停/选中视觉反馈

**Files:**
- Modify: `src/InvoiceAI.App/Pages/MainPage.cs` (BuildInvoiceListPanel)

- [ ] **Step 1: 增强发票列表项的 VisualStateManager**

在 `BuildInvoiceListPanel()` 的 item border 定义中，替换现有 VisualStateManager:

```csharp
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
                    new Setter { Property = Border.StrokeProperty, Value = ThemeManager.BorderLight },
                    new Setter { Property = Border.StrokeThicknessProperty, Value = 1.0 },
                    new Setter { Property = Border.ShadowProperty, Value = null }
                }
            },
            new VisualState
            {
                Name = "PointerOver",
                Setters =
                {
                    new Setter { Property = Border.BackgroundColorProperty, Value = ThemeManager.HoverBackground },
                    new Setter { Property = Border.StrokeProperty, Value = ThemeManager.BorderMedium },
                    new Setter { Property = Border.StrokeThicknessProperty, Value = 1.5 },
                    new Setter { Property = Border.ShadowProperty, Value = new Shadow
                    {
                        Brush = Colors.Black.WithAlpha(0.08f),
                        Offset = new Point(0, 2),
                        Radius = 8,
                        Opacity = 0.4f
                    }}
                }
            },
            new VisualState
            {
                Name = "Selected",
                Setters =
                {
                    new Setter { Property = Border.BackgroundColorProperty, Value = ThemeManager.SelectedBackground },
                    new Setter { Property = Border.StrokeProperty, Value = ThemeManager.BrandPrimary },
                    new Setter { Property = Border.StrokeThicknessProperty, Value = 2.0 },
                    new Setter { Property = Border.ShadowProperty, Value = new Shadow
                    {
                        Brush = ThemeManager.BrandPrimary.WithAlpha(0.2f),
                        Offset = new Point(0, 3),
                        Radius = 10,
                        Opacity = 0.5f
                    }}
                }
            }
        }
    }
});
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build C:\ClaudeCodeProject\InvoiceAI\src\InvoiceAI.App\InvoiceAI.App.csproj
```

- [ ] **Step 3: Commit**

```bash
cd C:\ClaudeCodeProject\InvoiceAI
git add src/InvoiceAI.App/Pages/MainPage.cs
git commit -m "feat: add hover/selected visual states to invoice list items"
```

---

### Task 4: 测试验证

**Files:**
- 无文件修改，仅测试

- [ ] **Step 1: 完整编译**

```bash
dotnet build C:\ClaudeCodeProject\InvoiceAI\InvoiceAI.sln
```

- [ ] **Step 2: 运行测试**

```bash
dotnet test C:\ClaudeCodeProject\InvoiceAI\tests\InvoiceAI.Core.Tests\InvoiceAI.Core.Tests.csproj
```

- [ ] **Step 3: 手动验证清单**

- [ ] 窗口宽度 > 1200px: 三栏布局，分类 180px，列表 320px
- [ ] 窗口宽度 900-1200px: 三栏布局，分类 150px，列表 250px
- [ ] 窗口宽度 < 900px: 仅列表面板，分类切换按钮可见
- [ ] 点击分类切换按钮: 分类面板显示/隐藏
- [ ] 按钮悬停: 背景变亮，轻微放大，有阴影
- [ ] 按钮按下: 背景变暗，轻微缩小
- [ ] 列表项悬停: 背景变色，边框加深，有阴影
- [ ] 列表项选中: 品牌色边框，选中背景，彩色阴影

- [ ] **Step 4: Commit (如有修改)**

```bash
cd C:\ClaudeCodeProject\InvoiceAI
git add .
git commit -m "test: verify Phase 2 responsive layout and visual feedback"
```

---

## 自审查

### 1. 规范覆盖检查

| 规范需求 | 对应 Task |
|----------|-----------|
| 三栏断点布局 | Task 1 |
| 分类面板切换 | Task 1 |
| 按钮悬停/按下动画 | Task 2 |
| 列表项悬停/选中效果 | Task 3 |
| 卡片阴影 | Task 3 |
| 测试验证 | Task 4 |

### 2. 占位符扫描

无 TBD/TODO/空白段落。

### 3. 类型一致性

- `ThemeManager` 属性名与 Task 2-5 一致
- `LayoutMode` 枚举在 Task 1 内定义并使用
- `OnSizeAllocated` 是 MAUI ContentPage 的标准方法

### 4. 范围检查

仅包含 Phase 2 核心: 响应式布局 + 视觉反馈。不含图片预览、弹窗动画等后续阶段内容。

---

*计划版本: v1.0 | 2026-04-08*
