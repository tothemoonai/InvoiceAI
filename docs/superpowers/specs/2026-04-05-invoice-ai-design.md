# InvoiceAI — 日本发票智能识别与管理系统 设计文档

> 日期: 2026-04-05
> 状态: 已批准

## 1. 项目概述

Windows 桌面优先的日本发票识别工具，基于 .NET MAUI 构建。

**核心功能**: 导入图片/PDF → 百度 OCR 提取文本 → GLM API 智能整理（严格遵循适格請求書规则）→ SQLite 本地存储 → 分类查询 + Excel 导出。

**目标用户**: 在日本工作的职场人士、会计、自由职业者。

## 2. 技术栈

| 组件 | 技术 |
|---|---|
| 框架 | .NET MAUI (.NET 8/9) |
| UI | C# Markup (CommunityToolkit.Maui.Markup) |
| 架构 | MVVM + CommunityToolkit.Mvvm |
| OCR | 百度 OCR API（直接处理图片和 PDF，无需额外 PDF 库） |
| AI | GLM API (bigmodel.cn)，模型 TBD（GLM-5 或 GLM-4V-Plus，后续选定） |
| 数据库 | EF Core + SQLite (Code First + Migrations) |
| Excel | EPPlus |
| 双语 | .resx 资源文件（中文默认 + 日语） |

**关于 PDF 库的说明**: README 中要求 PdfiumNet 或 PdfSharp，但用户确认百度 OCR API 可直接处理 PDF 文件，因此不需要额外的 PDF 渲染库。

**关于多模态输入的说明**: README 提到支持 OCR 文本 + 原始发票图片发送给 GLM 提高准确率。当前版本采用纯文本模式（OCR 文本 → GLM），多模态作为可选增强功能，未来根据所选 GLM 模型能力决定是否启用。

### NuGet 包清单

| 包名 | 用途 |
|---|---|
| CommunityToolkit.Maui | MAUI 工具包 |
| CommunityToolkit.Maui.Markup | C# Markup 支持 |
| CommunityToolkit.Mvvm | MVVM 源生成器 ([ObservableProperty], [RelayCommand]) |
| Microsoft.EntityFrameworkCore.Sqlite | SQLite ORM |
| EPPlus | Excel 导出 |

## 3. 架构方案：分层架构

```
InvoiceAI/
├── InvoiceAI.sln
├── src/
│   ├── InvoiceAI.Models/                  # 纯 POCO 实体
│   ├── InvoiceAI.Data/                    # EF Core 数据层
│   ├── InvoiceAI.Core/                    # 业务逻辑 + Services + ViewModels
│   └── InvoiceAI.App/                     # MAUI 主项目 (C# Markup)
├── tests/
│   └── InvoiceAI.Core.Tests/              # 单元测试 (xUnit)
├── docs/
└── InvoiceAI_Readme.MD
```

**依赖方向**: App → Core → Data → Models（单向依赖，实用分层架构。Core 直接引用 Data 获取 DbContext，Data 引用 Models 获取实体类。）

## 4. 数据模型

### Invoice 表

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | int PK | 自增主键 |
| IssuerName | string | 発行事業者名 |
| RegistrationNumber | string | 登録番号 (T + 13桁) |
| TransactionDate | DateTime? | 取引年月日 |
| Description | string | 取引内容摘要 |
| ItemsJson | string | 明细行 JSON |
| TaxExcludedAmount | decimal? | 税抜金額 |
| TaxIncludedAmount | decimal? | 税込金額 |
| TaxAmount | decimal? | 消費税額 |
| RecipientName | string? | 交付を受ける事業者名 |
| InvoiceType | enum | Standard / Simplified / NonQualified |
| MissingFields | string? | 缺失项 (JSON 数组) |
| Category | string | 用户分类 |
| SourceFilePath | string? | 原始文件路径 |
| FileHash | string? | 文件 SHA256 哈希（用于重复检测） |
| OcrRawText | string? | OCR 原始文本 |
| GlmRawResponse | string? | GLM 原始返回 |
| IsConfirmed | bool | 是否已确认保存 |
| CreatedAt | DateTime | 创建时间 |
| UpdatedAt | DateTime | 更新时间 |

### ItemsJson 结构

```json
[
  {
    "name": "令和6年3月分 電気料金",
    "amount": 8000,
    "taxRate": 10,
    "isReducedRate": false
  }
]
```

## 5. 服务层

### 服务接口

| 服务 | 职责 |
|---|---|
| IBaiduOcrService | 调用百度 OCR API，返回识别文本（支持图片和 PDF） |
| IGlmService | 发送 OCR 文本 + Prompt → 返回结构化 JSON |
| IInvoiceService | 发票 CRUD、查询、分类 |
| IExcelExportService | 按分类/时间范围导出 Excel |
| IFileService | 文件路径处理、格式过滤、哈希计算 |

### 核心工作流

```
导入文件 → FileService.ComputeHashAsync() → 重复检测
         → BaiduOcrService.RecognizeAsync()
         → GlmService.ProcessInvoiceAsync(ocrText)
         → UI 显示 (IsConfirmed=false)
         → 用户确认/编辑
         → InvoiceService.SaveAsync()
```

### GLM Prompt 设计

系统 Prompt 嵌入完整的适格請求書 6 项规则 + 簡易インボイス规则，要求 GLM 返回固定 JSON schema：

```json
{
  "issuerName": "株式会社〇〇",
  "registrationNumber": "T1234567890123",
  "transactionDate": "2024-03-31",
  "description": "電気料金",
  "items": [...],
  "taxExcludedAmount": 8000,
  "taxIncludedAmount": 8800,
  "taxAmount": 800,
  "recipientName": "〇〇株式会社",
  "invoiceType": "Standard",
  "missingFields": [],
  "suggestedCategory": "電気・ガス"
}
```

GLM 判断 invoiceType (Standard/Simplified/NonQualified) 并列出 missingFields。

### 分类系统

- 预置分类: 電気・ガス、食料品、オフィス用品、交通費、通信費、接待費、その他
- GLM 建议 suggestedCategory
- 用户可在设置中添加/编辑/删除分类
- 分类存储在 appsettings.json 中（轻量配置数据，非关系型查询需求）

## 6. UI 设计

### 主界面：三栏布局 (方案 A)

**UI 风格**: Windows 11 Fluent Design（圆角、透明亚克力效果、系统主题色）。中栏发票列表使用 CollectionView + Grouping 按月分组。

```
┌──────────┬──────────────┬───────────────────────────┐
│ 左栏 180px│ 中栏 280px   │ 右栏 *                    │
│ 分类导航  │ 发票列表      │ 详情区                     │
│          │              │                           │
│ 全部(24) │ 🔵本次识别    │ 工具栏: 搜索 | 导出 | 设置  │
│ 電気ガス  │ ─────────── │                           │
│ 食料品    │ 按月分组      │ 发票详情表格               │
│ オフィス  │ 历史记录      │ 适格状态标记               │
│ 交通費    │              │ 分类标签                   │
│ その他    │              │                           │
│          │              │ 操作: 保存 | 编辑 | 重试    │
│ 📥导入    │ 📤导出分类   │                           │
└──────────┴──────────────┴───────────────────────────┘
```

### 适格状态标记

- 🟢 ✅ 標準インボイス (Standard)
- 🟡 ⚠ 簡易インボイス (Simplified)
- 🔴 ✗ 非适格 (NonQualified)

### 页面与 ViewModel 对应

| 组件 | ViewModel | 职责 |
|---|---|---|
| CategoryList (左栏) | MainViewModel | 分类列表、数量统计、导入触发 |
| InvoiceList (中栏) | MainViewModel | 发票列表、分组、选中高亮 |
| InvoiceDetail (右栏) | InvoiceDetailViewModel | 详情展示、适格状态、编辑/保存 |
| ImportOverlay (弹窗) | ImportViewModel | 文件选择 → OCR → GLM → 预览 |
| SettingsDialog (弹窗) | SettingsViewModel | API 密钥、语言、分类管理 |

### 导入流程

1. 用户点击导入 → FilePicker (jpg/png/pdf 多选)，**也支持拖拽文件到主窗口**
2. 进度弹窗显示处理状态 (文件名 + 进度条)
3. 完成后中栏顶部高亮显示本次识别结果
4. 右栏自动显示第一张识别结果
5. 用户逐张确认/编辑分类 → 保存入库

### 拖拽支持

主窗口支持文件拖拽 (DragAndDrop)。拖入文件后触发与点击导入相同的处理流程。

### Excel 导出

1. 弹出导出选项: 按当前分类 / 全部 / 指定时间范围
2. EPPlus 生成 Excel: 表头 + 数据行 + 汇总行
3. 保存到用户选择路径

## 7. 设置与配置

### 配置内容

- 百度 OCR API Key / Secret Key
- GLM API Key
- GLM 端点 (默认 api.bigmodel.cn)
- 界面语言 (中文/日语)
- 自定义分类列表

### 存储位置

```
%LOCALAPPDATA%/InvoiceAI/
├── appsettings.json    → 配置文件
└── invoices.db         → SQLite 数据库
```

API 密钥不存储在代码仓库中。

## 8. 双语支持

使用 .resx 资源文件:
- `Strings.resx` → 中文 (默认)
- `Strings.ja.resx` → 日语

用户在设置中切换语言后，需要重新导航到当前页面以刷新 UI 文字。实现时使用基于 INotifyPropertyChanged 的资源访问模式，确保已渲染的 UI 元素能响应语言变更。

## 9. 错误处理

| 场景 | 处理方式 |
|---|---|
| API 密钥未配置 | 首次启动引导到设置页，顶部提示横幅 |
| OCR 识别失败 | 显示错误，允许重试，保留原始文件路径 |
| GLM 返回格式异常 | 尝试解析，失败则显示原始文本供手动录入 |
| 网络超时 | 重试 2 次，仍失败提示检查网络 |
| 不支持的文件格式 | 导入时过滤，仅允许 jpg/png/pdf |
| 重复导入 | 根据 FileHash (SHA256) 检测，提示是否覆盖 |
| 数据库迁移失败 | 启动时检查，提示修复或重建 |

## 10. DI 注册

MauiProgram.cs 关键注册:
- HttpClient → 单例
- IBaiduOcrService → BaiduOcrService
- IGlmService → GlmService
- IInvoiceService → InvoiceService
- IExcelExportService → ExcelExportService
- IFileService → FileService
- AppDbContext → SQLite
- MainViewModel, ImportViewModel, SettingsViewModel, InvoiceDetailViewModel
