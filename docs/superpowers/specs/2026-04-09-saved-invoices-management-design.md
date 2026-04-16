# 已保存发票列表管理功能 - 设计文档

**日期**: 2026-04-09  
**状态**: 待审核

---

## 1. 需求概述

### 1.1 核心需求

1. 用户可管理已导出的发票列表（已确认发票）
2. 主界面中间栏显示当次识别的记录和未导出的发票列表
3. 已导出的发票自动转入已保存类别
4. 已保存列表支持按分类、时间、导出时间筛选显示
5. 用户可浏览并编辑发票详情，修正错误后保存
6. 移除原有的"确认"功能

### 1.2 成功标准

- 点击"已保存列表"按钮弹出独立窗口，可与主界面同时操作
- 表格形式展示已保存发票，类似 Excel 列表
- 支持编辑和删除已保存发票
- 主界面不再显示"确认"按钮和"仅显示已确认"开关

---

## 2. 架构设计

### 2.1 整体架构

```
主界面 (MainPage)
├── 左栏: 分类列表 + 操作按钮（导入 / 导出 / 已保存列表 / 设置）
├── 中栏: 未导出发票列表 (IsConfirmed = false)
└── 右栏: 发票详情编辑面板

已保存列表窗口 (SavedInvoicesWindow) ← 独立 MAUI Window
├── 顶部工具栏: 筛选和排序控制
├── 中部表格区: Excel 样式的发票列表
└── 底部状态栏: 记录计数

发票详情对话框 (SavedInvoiceDetailDialog) ← 模态对话框
├── 基本信息区: 发行方、登録番号、日期、分类、金额等
├── 明細項目区: 可编辑的发票明细列表
└── 操作按钮: 保存 / 取消 / 删除
```

### 2.2 新增文件

| 文件 | 职责 |
|------|------|
| `src/InvoiceAI.Core/ViewModels/SavedInvoicesViewModel.cs` | 已保存列表窗口 ViewModel |
| `src/InvoiceAI.App/Pages/SavedInvoicesWindow.cs` | 已保存列表窗口 UI |
| `src/InvoiceAI.App/Pages/SavedInvoiceDetailDialog.cs` | 发票详情编辑对话框 UI |

### 2.3 修改文件

| 文件 | 修改内容 |
|------|----------|
| `src/InvoiceAI.App/Pages/MainPage.cs` | 增加"已保存列表"按钮；移除"确认"按钮 UI；移除"仅显示已确认"开关；移除列表中的"✅"徽章；中间栏默认显示未导出发票 |
| `src/InvoiceAI.Core/ViewModels/MainViewModel.cs` | 增加 `OpenSavedInvoicesCommand`；移除 `ShowConfirmedOnly` 属性 |
| `src/InvoiceAI.Core/ViewModels/InvoiceDetailViewModel.cs` | 移除 `SaveAsync`（确认）命令 |
| `src/InvoiceAI.Core/Services/IInvoiceService.cs` | 增加 `GetByExportDateRangeAsync` 方法 |
| `src/InvoiceAI.Core/Services/InvoiceService.cs` | 实现 `GetByExportDateRangeAsync` 方法 |
| `src/InvoiceAI.App/MauiProgram.cs` | 注册 `SavedInvoicesViewModel` |

---

## 3. 数据流

### 3.1 打开已保存列表

```
用户点击"已保存列表"按钮
    ↓
MainViewModel.OpenSavedInvoicesCommand.Execute()
    ↓
创建或激活 SavedInvoicesWindow
    ↓
SavedInvoicesViewModel.LoadDataCommand
    ↓
InvoiceService.GetConfirmedAsync()  → 查询 IsConfirmed=true 的发票
    ↓
更新 ObservableCollection<SavedInvoiceRow>
    ↓
表格显示数据
```

### 3.2 筛选数据

```
用户选择分类 / 设置日期范围 / 切换排序
    ↓
SavedInvoicesViewModel.ApplyFilters()
    ↓
在内存中过滤 ObservableCollection
    ↓
刷新表格显示
```

### 3.3 编辑发票详情

```
用户点击表格中的某一行
    ↓
弹出 SavedInvoiceDetailDialog (模态)
    ↓
显示发票完整信息，所有字段可编辑
    ↓
用户修改 → 点击"保存"
    ↓
InvoiceService.UpdateAsync(invoice)
    ↓
对话框关闭，SavedInvoicesViewModel 刷新表格
```

### 3.4 删除发票

```
用户在详情对话框中点击"删除"
    ↓
确认对话框: "确定删除此发票?"
    ↓
InvoiceService.DeleteAsync(id)
    ↓
对话框关闭，SavedInvoicesViewModel 从列表中移除该行
```

---

## 4. 组件详细设计

### 4.1 SavedInvoicesViewModel

**属性**:
```csharp
public ObservableCollection<SavedInvoiceRow> Invoices { get; }
public ObservableCollection<string> Categories { get; }
public string SelectedCategory { get; set; } = "全部";
public DateTime? FilterStartDate { get; set; }
public DateTime? FilterEndDate { get; set; }
public string SortMode { get; set; } = "TransactionDate"; // 或 "ExportedAt"
public bool IsBusy { get; set; }
public string StatusMessage { get; set; }
```

**命令**:
```csharp
public ICommand LoadDataCommand { get; }
public ICommand ApplyFiltersCommand { get; }
public ICommand EditInvoiceCommand { get; }       // 参数: Invoice
public ICommand DeleteInvoiceCommand { get; }      // 参数: Invoice
public ICommand OpenDetailDialogCommand { get; }   // 参数: Invoice → 弹出编辑对话框
```

**SavedInvoiceRow 模型** (简化显示用):
```csharp
public class SavedInvoiceRow
{
    public int Id { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string IssuerName { get; set; }
    public string RegistrationNumber { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public decimal? TaxExcludedAmount { get; set; }
    public decimal? TaxIncludedAmount { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public DateTime CreatedAt { get; set; }  // 导出时间
    public bool IsConfirmed { get; set; }
}
```

### 4.2 SavedInvoicesWindow UI 布局

```
┌─────────────────────────────────────────────────────────────┐
│  已保存发票列表                                        [─][×]│
├─────────────────────────────────────────────────────────────┤
│  [分类: 全部 ▼]  [起始日期] ~ [结束日期]  [🔄刷新]          │
│  排序: (●) 交易日期  ( ) 导出时间                           │
├─────────────────────────────────────────────────────────────┤
│  日期       │ 发行方      │ 登録番号   │ 内容  │ 分类 │ 金额│
│  ──────────┼────────────┼───────────┼──────┼──────┼─────│
│  2026-04-01 │ XX商店     │ T12345...  │ 办公用品│ 文具│ ¥1,100│ ← 点击行
│  2026-03-28 │ YY交通     │ T98765...  │ 交通费  │ 交通│ ¥500  │
│  ...                                                         │
├─────────────────────────────────────────────────────────────┤
│  共 42 条记录                                               │
└─────────────────────────────────────────────────────────────┘
```

**表格列定义**:
| 列 | 宽度 | 对齐 | 格式 |
|----|------|------|------|
| 日期 | 100px | 左 | yyyy-MM-dd |
| 发行方 | 150px | 左 | 文本，超出截断 |
| 登録番号 | 120px | 左 | 文本 |
| 内容 | 200px | 左 | 文本，超出截断 |
| 分类 | 80px | 左 | 文本 |
| 税抜金额 | 100px | 右 | ¥#,N0 |
| 税込金额 | 100px | 右 | ¥#,N0 |
| 类型 | 80px | 左 | 标准/简易/非适格 |
| 导出时间 | 140px | 左 | yyyy-MM-dd HH:mm |

**注意**: 当前 Invoice 模型没有 `ExportedAt` 字段，"导出时间"列显示的是 `CreatedAt`（发票创建/导入时间）。如需准确记录导出时间，需新增 `ExportedAt` 字段并在导出时更新。本次实现暂使用 `CreatedAt`。

### 4.3 SavedInvoiceDetailDialog UI 布局

```
┌──────────────────────────────────────────┐
│  编辑发票详情                            │
├──────────────────────────────────────────┤
│  基本信息                                │
│  ┌────────────────────────────────────┐  │
│  │ 发行方: [____________________]     │  │
│  │ 登録番号: [__________________]     │  │
│  │ 交易日期: [2026-04-01]             │  │
│  │ 分类: [文具 ▼]                     │  │
│  │ 税抜金额: [1,000]  税込金额: [1,100]│  │
│  │ 消费税额: [100]                    │  │
│  │ 类型: [标准 ▼]                     │  │
│  │ 接收方: [____________________]     │  │
│  └────────────────────────────────────┘  │
│                                          │
│  明細項目                                │
│  ┌────────────────────────────────────┐  │
│  │ 品目名          │ 税率 │ 金额      │  │
│  │ ───────────────┼──────┼───────── │  │
│  │ 办公用纸        │ 10%  │ ¥1,000  │  │
│  │ [可编辑行...]   │      │         │  │
│  └────────────────────────────────────┘  │
│                                          │
│  [保存]    [取消]    [删除]              │
└──────────────────────────────────────────┘
```

### 4.4 IInvoiceService 新增方法

```csharp
/// <summary>
/// 按创建时间范围查询已确认发票（使用 CreatedAt 作为导出时间的替代）
/// </summary>
Task<List<Invoice>> GetByExportDateRangeAsync(DateTime start, DateTime end);
```

**注意**: 由于当前模型没有 `ExportedAt` 字段，此方法查询的是 `CreatedAt` 字段。

---

## 5. 移除的功能

### 5.1 主界面移除内容

| 移除项 | 位置 | 替代方案 |
|--------|------|----------|
| "✅ 确认"按钮 | 详情面板顶部 | 用户直接在详情中编辑后点"保存" |
| "仅显示已确认"开关 | 发票列表上方 | 已导出发票自动不在主列表显示 |
| "✅"确认标记徽章 | 发票列表每项 | 不需要，主列表只显示未导出 |

### 5.2 MainViewModel 移除内容

| 移除项 | 类型 |
|--------|------|
| `ShowConfirmedOnly` 属性 | bool |
| `OnShowConfirmedOnlyChanged` 方法 | partial void |
| `RefreshInvoiceListAsync` 中的已确认过滤逻辑 | 方法内部逻辑 |

### 5.3 InvoiceDetailViewModel 移除内容

| 移除项 | 类型 |
|--------|------|
| `SaveAsync` 方法 | RelayCommand |

### 5.4 主界面中间栏变更

- **原来**: 显示所有发票（可通过"仅显示已确认"过滤）
- **现在**: 只显示未导出发票（IsConfirmed = false）
- 实现方式: `MainViewModel.LoadDataCommand` 默认查询 `GetUnconfirmedAsync()`

---

## 6. 错误处理

| 场景 | 处理方式 |
|------|----------|
| 编辑详情时必填字段为空 | 保存前验证，显示错误提示 |
| 删除发票失败 | 显示错误对话框，保留原数据 |
| 已保存窗口加载失败 | 显示错误提示，不崩溃 |
| 数据库并发冲突 | 捕获 DbUpdateConcurrencyException，提示用户刷新 |

---

## 7. 测试要点

### 7.1 功能测试

1. 点击"已保存列表"按钮 → 弹出独立窗口
2. 已保存窗口显示所有 IsConfirmed=true 的发票
3. 按分类筛选 → 只显示该分类的发票
4. 按日期范围筛选 → 只显示该范围内的发票
5. 切换排序方式 → 表格按新顺序显示
6. 点击某行 → 弹出详情对话框
7. 编辑详情 → 保存 → 数据库更新，表格刷新
8. 删除发票 → 确认后删除，表格移除该行
9. 主界面中间栏只显示未导出发票
10. 主界面无"确认"按钮、无"仅显示已确认"开关、无"✅"徽章

### 7.2 集成测试

1. 导出发票后（AutoSaveAfterExport=true）→ 发票自动从主列表消失，出现在已保存列表
2. 主界面和已保存窗口同时操作 → 数据一致性
3. 多窗口关闭/重新打开 → 状态正确恢复

---

## 8. 实现优先级

| 优先级 | 功能 | 说明 |
|--------|------|------|
| P0 | 已保存列表窗口基础 | 显示表格，加载数据 |
| P0 | 主界面移除确认功能 | 移除按钮、开关、徽章 |
| P0 | 主界面中间栏只显示未导出 | 修改查询逻辑 |
| P1 | 筛选功能 | 分类、日期范围、排序 |
| P1 | 详情编辑对话框 | 基本信息 + 明細編輯 |
| P1 | 保存和删除功能 | 数据库操作 + 刷新 |
| P2 | 窗口状态持久化 | 记住上次的位置、筛选条件 |
