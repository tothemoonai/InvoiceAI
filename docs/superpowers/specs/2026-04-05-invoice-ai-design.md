# InvoiceAI — 日本发票智能识别与管理系统 设计文档

> 日期: 2026-04-05 (初版) | 2026-04-07 (修订)
> 状态: 已实现并验证
>
> **变更记录 (2026-04-07 v2)**:
> - 新增 Cerebras 作为第三 LLM 提供商 (Qwen-3-235B, max_tokens=32768)
> - 设置页 LLM 配置改为: 提供商选择 → 模型下拉菜单 (Picker) → API Key (密码模式)，移除端点地址手动输入
> - NVIDIA 默认模型改为 `deepseek-ai/deepseek-v3.1-terminus`
> - 每个提供商预置模型列表，切换时自动填充端点地址和模型
> - 新增 Token 消耗统计 (prompt/completion/total)，在导入结果和 timing.log 中展示
> - 429 限流自动重试 (指数退避: 2s→4s→8s)
> - 日志目录集中管理至项目 `TEMP/errorlog/`（LogHelper 自动定位项目根目录）

## 1. 项目概述

Windows 桌面优先的日本发票识别工具，基于 .NET MAUI 构建。

**核心功能**: 导入图片/PDF → PaddleOCR 提取 Markdown 文本 → GLM-4.7 智能整理（严格遵循适格請求書规则）→ SQLite 本地存储 → 分类查询 + Excel 导出。

> **Spec 变更记录 (2026-04-07)**:
> - OCR 从百度 OCR (ApiKey+SecretKey) 迁移到 PaddleOCR-VL-1.5 (Token+Endpoint)
> - GLM 模型确定为 glm-4.7（推理模型），`max_tokens=100000`
> - Excel 库从 EPPlus 替换为 MiniExcel（无许可证问题）
> - 配置文件从 `%LOCALAPPDATA%` 移至 exe 同目录 (`AppContext.BaseDirectory`)
> - ViewModels 改为 Singleton（共享状态），Collection 必须 in-place 更新
> - WinUI3 FileOpenPicker 替代 `FilePicker.Default.PickAsync`
> - 设置页测试按钮改用 `Clicked` 事件（WinUI3 Command 绑定不生效）
> - App 生命周期: 异步初始化移至 `OnStart()`，禁止在 `App()` 构造函数中同步等待
> - GLM 多提供商支持: 新增 NVIDIA NIM (`z-ai/glm4.7`, max_tokens=32768) 作为替代提供商，与智谱共存
> - 新增 Cerebras 提供商 (`qwen-3-235b-a22b-instruct-2507`, max_tokens=32768)，NVIDIA 默认模型改为 `deepseek-ai/deepseek-v3.1-terminus`
> - 设置页 LLM 配置 UI 重构: 提供商 → 模型下拉菜单 → API Key，端点地址自动填充
> - Token 消耗统计: 每张发票显示 prompt/completion/total tokens，汇总到 timing.log
> - 429 限流重试: 指数退避 (2s→4s→8s)，单次和批量均支持
> - 日志集中管理: LogHelper 自动定位项目根目录 `TEMP/errorlog/`

**目标用户**: 在日本工作的职场人士、会计、自由职业者。

## 2. 技术栈

| 组件 | 技术 |
|---|---|
| 框架 | .NET MAUI 9 (net9.0-windows10.0.19041.0) |
| UI | C# Markup (CommunityToolkit.Maui.Markup) |
| 架构 | MVVM + CommunityToolkit.Mvvm |
| OCR | PaddleOCR-VL-1.5（Token 认证，返回 Markdown，支持图片和 PDF） |
| AI | GLM-4.7 多提供商: 智谱 (bigmodel.cn) + NVIDIA NIM + Cerebras |
| 数据库 | EF Core + SQLite (Code First) |
| Excel | MiniExcel |
| 双语 | .resx 资源文件（中文默认 + 日语） |

**关于 PDF 库的说明**: PaddleOCR 可直接处理 PDF 文件（`fileType: 0`），因此不需要额外的 PDF 渲染库。

**关于 GLM-4.7 推理模型的说明**: GLM-4.7 是推理模型，响应中包含 `reasoning_content`（思维链）和 `content`（实际回答）。`reasoning_tokens` 约占 `completion_tokens` 的 93%（如 2729/2904），因此 `max_tokens` 必须设为 100000 以确保有足够空间输出完整 JSON。

### NuGet 包清单

| 包名 | 用途 |
|---|---|
| CommunityToolkit.Maui | MAUI 工具包 |
| CommunityToolkit.Maui.Markup | C# Markup 支持 |
| CommunityToolkit.Mvvm | MVVM 源生成器 ([ObservableProperty], [RelayCommand]) |
| Microsoft.EntityFrameworkCore.Sqlite | SQLite ORM |
| MiniExcel | Excel 导出（替代 EPPlus，无许可证问题） |

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
| IBaiduOcrService | 调用 PaddleOCR API，返回 Markdown 识别文本（支持图片和 PDF） |
| IGlmService | 发送 OCR 文本 + Prompt → GLM-4.7 推理 → 返回结构化 JSON + Token 统计（支持智谱/NVIDIA/Cerebras 三提供商，429 限流自动重试） |
| IInvoiceService | 发票 CRUD、查询、分类 |
| IExcelExportService | 按分类/时间范围导出 Excel |
| IFileService | 文件路径处理、格式过滤、哈希计算 |

### 核心工作流

```
导入文件 → FileService.ComputeHashAsync() → 重复检测
         → BaiduOcrService.RecognizeAsync() [PaddleOCR, 返回 Markdown]
         → OCR 结果保存到 TEMP/errorlog/ocr/*.md
         → GlmService.ProcessBatchAsync(ocrTexts) [多提供商, 429限流重试, Token统计]
         → ExtractJson() 处理 reasoning_content + content → 反序列化 + Token usage 提取
         → GLM 原始响应保存到 TEMP/errorlog/glm_raw_response.json
         → InvoiceService.SaveAsync() → UI 显示 (IsConfirmed=false) + Token 消耗
         → 用户确认/编辑 → 保存
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

### Token 消耗统计

每次 GLM 调用从响应 JSON 的 `usage` 字段提取 token 用量：

- `PromptTokens` / `CompletionTokens` / `TotalTokens` 记录在 `GlmInvoiceResponse` 中
- 导入完成消息显示总 token: `"处理完成: 2/2 成功 (总耗时 5.2s, tokens: 3200)"`
- 每张发票状态显示各自 token: `"✅ 完成 (tokens: 1600)"`
- timing.log 记录详细: `[HH:mm:ss] GLM batch (2 invoices): 3200ms | tokens: 3200 (prompt: 800, completion: 2400)`

### 多提供商配置

| 提供商 | 默认模型 | max_tokens | 特殊参数 |
|---|---|---|---|
| 智谱 (zhipu) | glm-4.7 | 100000 | `temperature: 1.0`, `thinking: { type: "disabled" }` |
| NVIDIA NIM | deepseek-ai/deepseek-v3.1-terminus | 32768 | `temperature: 0.1` |
| Cerebras | qwen-3-235b-a22b-instruct-2507 | 32768 | `temperature: 0.1` |

提供商切换时自动保存旧提供商配置、加载新提供商配置、更新模型下拉列表。

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

- PaddleOCR Token / Endpoint
- LLM 提供商选择 (智谱 / NVIDIA NIM / Cerebras)
- 模型下拉选择 (每个提供商预置模型列表，切换提供商时自动更新)
- API Key (密码模式输入，端点地址根据提供商自动填充)
- 智谱: glm-4.7 / glm-4-plus (max_tokens=100000)
- NVIDIA: deepseek-ai/deepseek-v3.1-terminus / deepseek-ai/deepseek-r1 (max_tokens=32768)
- Cerebras: qwen-3-235b-a22b-instruct-2507 (max_tokens=32768)
- 界面语言 (中文/日语)
- 自定义分类列表

### 存储位置

```
AppContext.BaseDirectory/
├── InvoiceAI.App.exe
└── appsettings.json    → 配置文件（与 exe 同目录）

FileSystem.AppDataDirectory/
└── invoices.db         → SQLite 数据库

<项目根目录>/TEMP/errorlog/
├── import_error.log    → 导入错误日志
├── timing.log          → 计时 + Token 消耗统计
├── glm_raw_response.json → GLM 原始响应（调试用）
├── glm_batch_raw_response.json → 批量 GLM 响应
└── ocr/                → OCR 识别结果 Markdown 文件
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
| OCR 识别失败 | 显示错误，允许重试。错误详情写入 TEMP/errorlog/import_error.log |
| GLM 返回格式异常 | ExtractJson() 尝试从 reasoning_content/content 提取 JSON，失败则记录原始响应到 glm_parse_failed.txt |
| 网络超时 | 重试 2 次，仍失败提示检查网络 |
| 429 限流 | 指数退避重试 (2s→4s→8s)，最多 3 次，日志记录重试过程 |
| 不支持的文件格式 | 导入时过滤，仅允许 jpg/png/pdf |
| 重复导入 | 根据 FileHash (SHA256) 检测，提示是否覆盖 |
| 数据库迁移失败 | 启动时检查，提示修复或重建 |

## 10. DI 注册

MauiProgram.cs 关键注册:
- HttpClient → 单例 (Timeout = 5 分钟)
- IBaiduOcrService → BaiduOcrService (PaddleOCR)
- IGlmService → GlmService (多提供商, max_tokens=100000/32768, 429 重试, Token 统计)
- IInvoiceService → InvoiceService
- IExcelExportService → ExcelExportService (MiniExcel)
- IFileService → FileService
- IAppSettingsService → AppSettingsService (单例)
- AppDbContext → SQLite (Scoped)
- MainViewModel, ImportViewModel, SettingsViewModel, InvoiceDetailViewModel → **单例**（共享状态）
- Pages (MainPage, SettingsPage) → Transient
