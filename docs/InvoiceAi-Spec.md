# InvoiceAI — 日本发票智能识别与管理系统 设计文档

> 日期: 2026-04-05 (初版) | 2026-04-07 (修订 v2) | 2026-04-08 (修订 v3) | 2026-04-09 (修订 v4) | 2026-04-09 (修订 v6) | **2026-04-13 (修订 v7)** | **2026-04-14 (修订 v8)**
> 状态: 已实现并验证
>
> **变更记录 (2026-04-14 v8)**:
>
> - **架构重构**: 提取 `IInvoiceImportService` 接口和 `InvoiceImportService` 实现，将导入核心逻辑（分批处理、OCR、AI 分析、归档）从 `ImportViewModel` 剥离。
> - **代码复用**: `ImportViewModel` 简化为仅处理 UI 状态，命令行测试 (`PdfImportTest`) 直接调用 `InvoiceImportService`，确保 APP 与测试 100% 逻辑一致。
> - **Bug 修复 (PDF 导入映射)**: 修复了 PDF 拆分图片导入时，GLM 分析结果索引与源图片对应关系错误的 Bug。现在发票记录能准确关联到对应的源文件（如 `page_1.jpg`），并归档至正确的分类目录。
> - **UI 修复**: 修复已导出发票详情对话框 (`SavedInvoiceDetailDialog`) 中，因 Grid 布局嵌套导致编辑输入框不可见的问题。
> - **UI 优化**: 主界面分类列表和发票详情面板添加 `VerticalScrollBarVisibility`，支持窗口缩小时的滚动查看。
> - **测试增强**: 新增 `DbCheck` 工具用于快速检查数据库内容；新增 `import_pdf` 测试用例验证 PDF 批量导入逻辑。
>
> **变更记录 (2026-04-13 v7)**:
>
> - **发票详情编辑保存功能**: 主界面发票详情面板支持编辑模式，点击"编辑"按钮进入编辑，所有字段可修改，点击"保存"后更新数据库。支持发行方、登録番号、交易日期、内容、分类、税抜金額、税込金額、消費税額、交付先、发票类型、明细项目等字段的编辑
> - **编辑保存不改变 IsConfirmed**: 用户在主界面编辑保存发票后，`IsConfirmed` 字段保持不变，发票仍显示在主界面（未导出列表），不会自动进入已导出列表
> - **术语统一为"已导出"**: 主界面按钮"💾 已保存"改为"📤 已导出"，窗口标题"已保存发票列表"改为"已导出发票列表"，空状态文本同步更新
> - **已导出发票详情对话框修复**: 重构表单布局使用 `Grid.Add()` 直接添加控件，避免嵌套 Grid 导致布局问题。增强 Entry 控件可见性（添加 BackgroundColor、Placeholder、显式设置 IsVisible/IsEnabled）。使用 `PushAsync` 代替 `PushModalAsync`
> - **导入流程错误提示增强**: 各阶段失败时提供明确的错误提示：
>   - 无支持文件: "错误：没有支持的文件格式（仅支持 JPG、PNG、PDF）"
>   - 超过限制: "提示：最多处理 5 个文件，已选择前 5 个"
>   - PDF 拆分失败: "❌ PDF拆分失败: 文件名 - 错误信息"
>   - 压缩失败: "⚠ 压缩失败，使用原图"
>   - OCR 失败: "❌ OCR失败: 错误信息"
>   - AI 分析失败: "❌ AI分析失败: 错误信息"
>   - 最终状态: "处理完成: X/Y 成功 | 注意：PDF拆分失败 N 个；压缩失败 M 个（使用原图）..."
> - **UI 滚动条优化**: 分类面板和详情面板添加 `VerticalScrollBarVisibility = ScrollBarVisibility.Always`，确保窗口变小时内容可滚动查看
> - **详情面板布局修复**: 将 `detailContainer` 从 `VerticalStackLayout` 改为 `Grid`，使用 `RowDefinitions` 约束高度，使 `ScrollView` 正确计算并显示滚动条
>
> **变更记录 (2026-04-09 v6)**:
> 
> - **已保存发票列表管理**: 新增独立窗口（SavedInvoicesWindow）以表格形式展示已确认发票，支持分类/日期筛选、编辑详情、删除。主界面中间栏默认显示未导出发票，移除"确认"按钮、"仅显示已确认"开关、"✅"徽章
> - **命令行测试模式**: 新增 `--test`/`-t` 参数进入无头测试模式，支持 `--case=X` 指定单个测试、`--all` 运行全部。8 个测试用例覆盖 load/category/search/import/export/delete/saved/edit。标准格式输出结果到控制台，退出码 0=通过，非0=失败
> 
> **变更记录 (2026-04-09 v4)**:
> 
> - 统一配色系统：紫罗兰色 `#6C5CE7` 作为品牌主色，语义化颜色键替换硬编码颜色
> - 暗色主题支持：设置页面添加"主题设置"区块（跟随系统/浅色/暗色），保存到 appsettings.json 的 ThemeMode 字段
> - 字体样式定义：添加标题、金额、正文、状态等 11 个字体样式
> - 导入覆盖层修复：改为手动控制显示/隐藏，避免误触发；拖拽导入功能完全移除
> - 响应式布局：支持 Expanded (>1200px)、Standard (900-1200px) 两种布局模式，动态调整列宽
> - 发票列表布局：金额紧跟日期显示，分类独占一行 (Row 2)，类型标签靠右对齐
> - 确认按钮修复：恢复 3 列 Grid 布局，保存后刷新列表更新确认标记 ✅
> - 导出功能修复：添加 `ValueTuple<string, DateTime?, DateTime?, int>` 类型匹配，支持三种导出模式
> - 金额显示修复：设置 `LineBreakMode.NoWrap` + `MaxLines=1` 防止金额竖向显示
> 
> **变更记录 (2026-04-09 v5 - 新增)**:
> 
> - **单文件多发票支持**:
>   - 优化 OCR 提示词，明确告知 LLM 单文件（多页 PDF）可能包含多张发票
>   - 修改 `GlmService` 解析逻辑，优先识别 JSON 数组格式，支持返回多张发票
>   - 优化 `ImportViewModel` 映射逻辑，支持单个文件映射到多个 `Invoice` 对象
>   - curl 测试验证：Cerebras API 正确返回包含 3 张发票的 JSON 数组
> - **已保存记录交互优化**:
>   - 选择模式改为 `Single`，默认单选，支持 Ctrl 键多选
>   - 确认标记 (✅) 从第一排移至第三排最右侧，与分类同行
>   - 第一排仅显示公司名和类型标签，视觉更简洁
>   - 使用 Grid 布局确保发票列表占据剩余空间并支持滚动
> - **详情面板优化**:
>   - 将「确认」和「删除」按钮固定在详情面板顶部（ScrollView 外部）
>   - 用户滚动查看长内容时，按钮始终可见，无需滚回顶部即可操作
> 
> **变更记录 (2026-04-07 v2)**:
> 
> - 新增 Cerebras 作为第三 LLM 提供商 (Qwen-3-235B, max_tokens=32768)
> - 设置页 LLM 配置改为: 提供商选择 → 模型下拉菜单 (Picker) → API Key (密码模式)，移除端点地址手动输入
> - NVIDIA 默认模型改为 `deepseek-ai/deepseek-v3.1-terminus`
> - 每个提供商预置模型列表，切换时自动填充端点地址和模型
> - 新增 Token 消耗统计 (prompt/completion/total)，在导入结果和 timing.log 中展示
> - 429 限流自动重试 (指数退避: 2s→4s→8s)
> - 日志目录集中管理至项目 `TEMP/errorlog/`（LogHelper 自动定位项目根目录）
> 
> **变更记录 (2026-04-08 v3)**:
> 
> - 新增导出设置：Excel 导出路径配置（文件夹选择器）、导出后自动保存确认开关
> - 新增发票归档设置：发票文件保存路径配置（导入后文件压缩/重命名的归档目录）
> - 分类管理 UI 重构：从纵向列表改为 3 列网格布局 (GridItemsLayout)，紧凑芯片样式

## 1. 项目概述

Windows 桌面优先的日本发票识别工具，基于 .NET MAUI 构建。

**核心功能**: 导入图片/PDF → PaddleOCR 提取 Markdown 文本 → GLM-4.7 智能整理（严格遵循适格請求書规则）→ SQLite 本地存储 → 分类查询 + Excel 导出。

> **Spec 变更记录 (2026-04-07)**:
> 
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

| 组件    | 技术                                                     |
| ----- | ------------------------------------------------------ |
| 框架    | .NET MAUI 9 (net9.0-windows10.0.19041.0)               |
| UI    | C# Markup (CommunityToolkit.Maui.Markup)               |
| 架构    | MVVM + CommunityToolkit.Mvvm                           |
| OCR   | PaddleOCR-VL-1.5（Token 认证，返回 Markdown，支持图片和 PDF）       |
| AI    | GLM-4.7 多提供商: 智谱 (bigmodel.cn) + NVIDIA NIM + Cerebras |
| 数据库   | EF Core + SQLite (Code First)                          |
| Excel | MiniExcel                                              |
| 双语    | .resx 资源文件（中文默认 + 日语）                                  |

**关于 PDF 库的说明**: PaddleOCR 可直接处理 PDF 文件（`fileType: 0`），因此不需要额外的 PDF 渲染库。

**关于 GLM-4.7 推理模型的说明**: GLM-4.7 是推理模型，响应中包含 `reasoning_content`（思维链）和 `content`（实际回答）。`reasoning_tokens` 约占 `completion_tokens` 的 93%（如 2729/2904），因此 `max_tokens` 必须设为 100000 以确保有足够空间输出完整 JSON。

### NuGet 包清单

| 包名                                   | 用途                                               |
| ------------------------------------ | ------------------------------------------------ |
| CommunityToolkit.Maui                | MAUI 工具包                                         |
| CommunityToolkit.Maui.Markup         | C# Markup 支持                                     |
| CommunityToolkit.Mvvm                | MVVM 源生成器 ([ObservableProperty], [RelayCommand]) |
| Microsoft.EntityFrameworkCore.Sqlite | SQLite ORM                                       |
| MiniExcel                            | Excel 导出（替代 EPPlus，无许可证问题）                       |

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

| 字段                 | 类型        | 说明                                   |
| ------------------ | --------- | ------------------------------------ |
| Id                 | int PK    | 自增主键                                 |
| IssuerName         | string    | 発行事業者名                               |
| RegistrationNumber | string    | 登録番号 (T + 13桁)                       |
| TransactionDate    | DateTime? | 取引年月日                                |
| Description        | string    | 取引内容摘要                               |
| ItemsJson          | string    | 明细行 JSON                             |
| TaxExcludedAmount  | decimal?  | 税抜金額                                 |
| TaxIncludedAmount  | decimal?  | 税込金額                                 |
| TaxAmount          | decimal?  | 消費税額                                 |
| RecipientName      | string?   | 交付を受ける事業者名                           |
| InvoiceType        | enum      | Standard / Simplified / NonQualified |
| MissingFields      | string?   | 缺失项 (JSON 数组)                        |
| Category           | string    | 用户分类                                 |
| SourceFilePath     | string?   | 原始文件路径                               |
| FileHash           | string?   | 文件 SHA256 哈希（用于重复检测）                 |
| OcrRawText         | string?   | OCR 原始文本                             |
| GlmRawResponse     | string?   | GLM 原始返回                             |
| IsConfirmed        | bool      | 是否已确认保存                              |
| CreatedAt          | DateTime  | 创建时间                                 |
| UpdatedAt          | DateTime  | 更新时间                                 |

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

| 服务                  | 职责                                                                                            |
| ------------------- | --------------------------------------------------------------------------------------------- |
| IBaiduOcrService    | 调用 PaddleOCR API，返回 Markdown 识别文本（支持图片和 PDF）                                                  |
| IGlmService         | 发送 OCR 文本 + Prompt → GLM-4.7 推理 → 返回结构化 JSON + Token 统计（支持智谱/NVIDIA/Cerebras 三提供商，429 限流自动重试） |
| IInvoiceImportService | 导入核心逻辑 (分批处理, OCR, AI, 归档)                                                          |
| IInvoiceService     | 发票 CRUD、查询、分类                                                                                 |
| IExcelExportService | 按分类/时间范围导出 Excel                                                                              |
| IFileService        | 文件路径处理、格式过滤、哈希计算                                                                              |

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

| 提供商        | 默认模型                               | max_tokens | 特殊参数                                                 |
| ---------- | ---------------------------------- | ---------- | ---------------------------------------------------- |
| 智谱 (zhipu) | glm-4.7                            | 100000     | `temperature: 1.0`, `thinking: { type: "disabled" }` |
| NVIDIA NIM | deepseek-ai/deepseek-v3.1-terminus | 32768      | `temperature: 0.1`                                   |
| Cerebras   | qwen-3-235b-a22b-instruct-2507     | 32768      | `temperature: 0.1`                                   |

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

| 组件                  | ViewModel              | 职责                    |
| ------------------- | ---------------------- | --------------------- |
| CategoryList (左栏)   | MainViewModel          | 分类列表、数量统计、导入触发        |
| InvoiceList (中栏)    | MainViewModel          | 发票列表、分组、选中高亮          |
| InvoiceDetail (右栏)  | InvoiceDetailViewModel | 详情展示、适格状态、编辑/保存（支持只读和编辑两种模式） |
| ImportOverlay (弹窗)  | ImportViewModel        | 文件选择 → OCR → GLM → 预览 |
| SettingsDialog (弹窗) | SettingsViewModel      | API 密钥、语言、分类管理        |

### 导入流程

1. 用户点击导入 → FilePicker (jpg/png/pdf 多选)，**也支持拖拽文件到主窗口**
2. 进度弹窗显示处理状态 (文件名 + 进度条 + 各阶段状态)
3. 各阶段错误提示：
   - **PDF 拆分失败**: `❌ PDF拆分失败: 文件名 - 错误信息`
   - **图片压缩失败**: `⚠ 压缩失败，使用原图`（自动回退到原图继续处理）
   - **OCR 识别失败**: `❌ OCR失败: 错误信息`（跳过该文件，继续处理下一个）
   - **AI 分析失败**: `❌ AI分析失败: 错误信息`（批量失败时所有文件标记为失败）
   - **文件已存在**: `⚠ 已存在（跳过）`（根据 FileHash 去重）
4. 完成后显示摘要：`处理完成: X/Y 成功 (总耗时 Zs) | 注意：PDF拆分失败 N 个；压缩失败 M 个（使用原图）；OCR失败 K 个；跳过 L 个（已存在）`
5. 中栏顶部高亮显示本次识别结果
6. 右栏自动显示第一张识别结果
7. 用户逐张编辑分类 → 保存入库（`IsConfirmed` 不变，仍显示在主界面）

### 拖拽支持

主窗口支持文件拖拽 (DragAndDrop)。拖入文件后触发与点击导入相同的处理流程。

### Excel 导出

1. 弹出导出选项: 按当前分类 / 全部 / 指定时间范围
2. EPPlus 生成 Excel: 表头 + 数据行 + 汇总行
3. 保存到用户选择路径
4. 导出的发票自动设置 `IsConfirmed = true`，移入已导出列表

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
- 导出设置：Excel 导出路径（文件夹选择器）、导出后自动保存确认开关
- 发票归档设置：发票文件保存路径（导入后文件压缩/重命名的归档目录）
- 自定义分类列表（3 列网格布局，紧凑芯片样式）

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

| 场景             | 处理方式                                                                                          |
| -------------- | --------------------------------------------------------------------------------------------- |
| API 密钥未配置       | 首次启动引导到设置页，顶部提示横幅                                                                             |
| OCR 识别失败       | 显示错误，允许重试。错误详情写入 TEMP/errorlog/import_error.log。导入流程中显示 `❌ OCR失败: 错误信息` 并跳过该文件       |
| GLM 返回格式异常     | ExtractJson() 尝试从 reasoning_content/content 提取 JSON，失败则记录原始响应到 glm_parse_failed.txt   |
| 网络超时           | 重试 2 次，仍失败提示检查网络                                                                              |
| 429 限流         | 指数退避重试 (2s→4s→8s)，最多 3 次，日志记录重试过程                                                                 |
| 不支持的文件格式        | 导入时过滤，仅允许 jpg/png/pdf。显示提示: `错误：没有支持的文件格式（仅支持 JPG、PNG、PDF）`                              |
| 重复导入           | 根据 FileHash (SHA256) 检测，显示 `⚠ 已存在（跳过）`                                                                |
| 数据库迁移失败         | 启动时检查，提示修复或重建                                                                                  |
| PDF 拆分失败       | 显示 `❌ PDF拆分失败: 文件名 - 错误信息`，记录到日志，继续处理其他文件                                                        |
| 图片压缩失败         | 显示 `⚠ 压缩失败，使用原图`，自动回退到原始图片继续 OCR 处理                                                              |
| AI 分析失败        | 显示 `❌ AI分析失败: 错误信息`，批量失败时所有 OCR 成功的文件标记为失败                                                      |
| 编辑保存验证失败       | 显示验证错误对话框（如: `発行事業者不能为空`），不保存到数据库                                                                  |
| 已导出发票详情对话框为空   | 重构布局使用 `Grid.Add()` 直接添加控件，增强 Entry 可见性（BackgroundColor、Placeholder、IsVisible/IsEnabled） |

### 导入流程错误提示详情

导入流程各阶段失败时提供明确的错误提示，最终状态消息包含完整摘要：

```
处理完成: 3/5 成功 (总耗时 12.3s, tokens: 1234) | 注意：PDF拆分失败 1 个；压缩失败 1 个（使用原图）；OCR失败 1 个；跳过 1 个（已存在）
```

### 编辑模式错误提示

- **验证错误**: 发行方不能为空时，弹出对话框显示 `発行事業者不能为空`
- **编辑取消**: 点击取消按钮，恢复所有字段到编辑前的值

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

---

## 11. 已导出发票列表管理

### 11.1 需求概述

用户可管理已导出的发票列表（已确认发票）。主界面中间栏显示当次识别的记录和未导出的发票列表。已导出的发票自动转入已导出类别。已导出列表支持按分类、时间、导出时间筛选显示。用户可浏览并编辑发票详情，修正错误后保存。**术语统一**: 所有 "已保存" 相关文本统一改为 "已导出"。

### 11.2 整体架构

```
主界面 (MainPage)
├── 左栏: 分类列表 + 操作按钮（导入 / 导出 / 📤 已导出 / 设置）
├── 中栏: 未导出发票列表 (IsConfirmed = false)
└── 右栏: 发票详情编辑面板（支持只读和编辑两种模式）

已导出列表窗口 (SavedInvoicesWindow) ← 独立 MAUI Window
├── 顶部工具栏: 筛选和排序控制
├── 中部表格区: Excel 样式的发票列表
└── 底部状态栏: 记录计数

发票详情对话框 (SavedInvoiceDetailDialog) ← 导航页面（PushAsync）
├── 基本信息区: 发行方、登録番号、日期、分类、金额等
├── 明細項目区: 可编辑的发票明细列表
└── 操作按钮: 保存 / 取消 / 删除
```

### 11.3 新增文件

| 文件                                                        | 职责                |
| --------------------------------------------------------- | ----------------- |
| `src/InvoiceAI.Core/ViewModels/SavedInvoicesViewModel.cs` | 已导出列表窗口 ViewModel |
| `src/InvoiceAI.App/Pages/SavedInvoicesWindow.cs`          | 已导出列表窗口 UI（标题: "已导出发票列表"） |
| `src/InvoiceAI.App/Pages/SavedInvoiceDetailDialog.cs`     | 发票详情编辑对话框 UI（导航页面，非模态） |

### 11.4 修改文件

| 文件                                                        | 修改内容                                                                 |
| --------------------------------------------------------- | -------------------------------------------------------------------- |
| `src/InvoiceAI.App/Pages/MainPage.cs`                     | 增加"📤 已导出"按钮（替代"💾 已保存"）；移除"确认"按钮 UI；移除"仅显示已确认"开关；移除列表中的"✅"徽章；中间栏默认显示未导出发票；右栏添加编辑/保存功能 |
| `src/InvoiceAI.Core/ViewModels/MainViewModel.cs`          | 增加 `OpenSavedInvoicesCommand`；移除 `ShowConfirmedOnly` 属性                      |
| `src/InvoiceAI.Core/ViewModels/InvoiceDetailViewModel.cs` | 添加编辑模式支持（`IsEditMode`、可编辑字段属性、`StartEditingCommand`、`SaveCommand`、`CancelEditingCommand`） |
| `src/InvoiceAI.Core/ViewModels/ImportViewModel.cs`        | 增强各阶段错误提示，添加错误计数和最终状态摘要                                                  |
| `src/InvoiceAI.Core/Services/IInvoiceService.cs`          | 增加 `GetByCreateDateRangeAsync` 和 `GetDistinctCategoriesAsync` 方法              |
| `src/InvoiceAI.Core/Services/InvoiceService.cs`           | 实现上述两个新方法                                                            |
| `src/InvoiceAI.App/MauiProgram.cs`                        | 注册 `SavedInvoicesViewModel`                                         |

### 11.5 数据流

- **打开已导出列表**: 点击"📤 已导出"按钮 → 创建 SavedInvoicesWindow → LoadDataCommand → 查询 IsConfirmed=true → 表格显示
- **筛选数据**: 选择分类/日期范围/排序 → ApplyFilters() → 内存过滤 ObservableCollection → 刷新表格
- **编辑发票详情**: 点击行 → 弹出 SavedInvoiceDetailDialog（PushAsync） → 编辑 → Save → UpdateAsync → 刷新表格
- **删除发票**: 详情对话框中点删除 → 确认 → DeleteAsync → 移除行
- **主界面编辑保存**: 选择发票 → 点击"✏ 编辑" → 修改字段 → 点击"💾 保存" → UpdateAsync → `IsConfirmed` 不变，仍在主界面显示

### 11.6 SavedInvoiceRow 模型

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
    public DateTime CreatedAt { get; set; }  // 导出时间（使用 CreatedAt 替代）
    public bool IsConfirmed { get; set; }
}
```

**注意**: 当前 Invoice 模型没有 `ExportedAt` 字段，"导出时间"列显示的是 `CreatedAt`（发票创建/导入时间）。

### 11.7 SavedInvoicesWindow UI 布局

```
┌─────────────────────────────────────────────────────────────┐
│  已导出发票列表                                        [─][×]│
├─────────────────────────────────────────────────────────────┤
│  [分类: 全部 ▼]  [起始日期] ~ [结束日期]  [🔄刷新]          │
│  排序: (●) 交易日期  ( ) 导出时间                           │
├─────────────────────────────────────────────────────────────┤
│  日期 │ 发行方 │ 登録番号 │ 内容 │ 分类 │ 税抜 │ 税込 │ 类型 │ 导出时间 │
│  ─────────────┼────────────────┼────────────┼───────────┼─────────│
│  2026-04-01 │ XX商店 │ T12345... │ 办公用品 │ 文具 │ ¥1,100 │ 標準 │ ... │
├─────────────────────────────────────────────────────────────┤
│  共 42 条记录                                               │
└─────────────────────────────────────────────────────────────┘
```

**空状态提示**: `暂无已导出的发票记录`

**表格列定义**:
| 列 | 宽度 | 对齐 | 格式 |
|----|------|------|------|
| 日期 | 100px | 左 | yyyy-MM-dd |
| 发行方 | 150px | 左 | 文本，超出截断 |
| 登録番号 | 120px | 左 | 文本 |
| 内容 | 200px | 左 | 文本，超出截断 |
| 分类 | 80px | 左 | 文本 |
| 税抜金額 | 100px | 右 | ¥#,N0 |
| 税込金額 | 100px | 右 | ¥#,N0 |
| 类型 | 80px | 左 | 标准/简易/非适格 |
| 导出时间 | 140px | 左 | yyyy-MM-dd HH:mm |

### 11.8 移除/变更的功能

| 移除/变更项                | 位置                     | 替代方案/说明                                      |
| ---------------------- | ---------------------- | -------------------------------------------- |
| "✅ 确认"按钮               | 详情面板顶部                 | 用户直接在详情中编辑后点"保存"（`IsConfirmed` 不变）               |
| "仅显示已确认"开关             | 发票列表上方                 | 已导出发票自动不在主列表显示                               |
| "✅"确认标记徽章              | 发票列表每项                 | 不需要，主列表只显示未导出                                  |
| `ShowConfirmedOnly` 属性 | MainViewModel          | —                                            |
| `SaveAsync` 命令         | InvoiceDetailViewModel | 变更为编辑保存模式：`StartEditingCommand` / `SaveCommand` / `CancelEditingCommand` |
| "💾 已保存"按钮             | 主界面左栏底部                 | 改为"📤 已导出"按钮                                  |
| SavedInvoiceDetailDialog 模态对话框 | 已导出列表窗口              | 改为导航页面（`PushAsync`），避免模态对话框导致的 UI 问题          |

---

## 12. 命令行测试模式

### 12.1 需求概述

应用支持 `--test` / `-t` 命令行参数进入测试模式。支持 `--case=功能名称` 指定单个测试场景。支持 `--all` 或不带 case 时运行所有测试。测试模式不弹出主窗口，直接执行核心业务逻辑。每个测试按标准格式输出结果到控制台。测试完成后返回退出码（0=全部通过，非0=有失败）。

### 12.2 测试覆盖的功能

| #   | 功能    | --case 值   |
| --- | ----- | ---------- |
| 1   | 数据加载  | `load`     |
| 2   | 分类管理  | `category` |
| 3   | 发票搜索  | `search`   |
| 4   | 发票导入  | `import`   |
| 5   | 发票导出  | `export`   |
| 6   | 发票删除  | `delete`   |
| 7   | 已导出列表 | `saved`    |
| 8   | 编辑保存  | `edit`     |
| 9   | ViewModel 编辑保存 | `editsave` |
| 10  | 编辑验证  | `editvalidation` |

### 12.3 无头执行流程

```
InvoiceAI.App.exe --test --case=load
    ↓
Platforms/Windows/App.xaml.cs (Windows 启动入口)
    ↓
解析命令行参数
    ↓
检测到 --test → 进入无头模式
    ↓
不创建 MAUI 窗口 (不调用 base.OnLaunched)
    ↓
手动构建轻量 DI 容器 (仅注册核心服务，跳过 UI 相关)
    ↓
TestRunner.Run(caseName)
    ↓
执行对应测试用例
    ↓
输出结果 → Environment.Exit(exitCode)
```

### 12.4 服务依赖层次

**测试模式注册**:

```
必需服务: AppDbContext, IAppSettingsService, IFileService,
          IInvoiceService, IExcelExportService, IBaiduOcrService,
          IGlmService, HttpClient

跳过服务: 所有 ViewModels, 所有 Pages, MAUI 字体/资源注册
```

**测试数据管理**:

- 使用主数据库: 测试直接在 `invoices.db` 主数据库上执行
- 测试输入: 使用 `invoices/` 目录下的真实发票图片
- 数据保护: 破坏性测试（delete/edit）先创建专用测试数据再删除，确保不留痕迹

### 12.5 测试输出格式

```
=== 测试 [load] 开始 ===
输入/条件: 查询未确认发票列表
预期结果: 返回发票列表，所有发票 IsConfirmed=false
实际结果: 获取 15 条未确认发票记录
截图路径: N/A
状态: PASS
=== 测试结束 ===
```

### 12.6 测试用例详细设计

| 测试                | 输入                 | 预期                       | 说明               |
| ----------------- | ------------------ | ------------------------ | ---------------- |
| **load**          | 查询未确认发票            | 返回 IsConfirmed=false 的列表 | 验证默认过滤           |
| **category**      | 获取分类计数             | 返回非空字典                   | 验证分类功能           |
| **search**        | 用第一个发票发行方名搜索       | 返回匹配结果                   | 数据库为空时 SKIP      |
| **import**        | invoices/ 目录下的一张图片 | OCR→AI→保存→删除清理           | OCR/AI 不可用时 SKIP |
| **export**        | 导出所有发票到临时 Excel    | 文件存在且大小 > 0              | 验证 MiniExcel     |
| **delete**        | 创建测试发票后删除          | GetByIdAsync 返回 null     | 验证 CRUD          |
| **saved**         | 查询已确认发票            | 返回 IsConfirmed=true 的列表  | 验证时间范围查询         |
| **edit**          | 修改发票字段后保存重读        | 字段值正确更新                  | 验证 UpdateAsync   |
| **editsave**      | ViewModel 编辑保存完整流程   | 编辑→保存→数据库更新→IsConfirmed=true | 验证编辑保存功能       |
| **editvalidation** | 空 IssuerName 尝试保存    | ValidationError 非空，IsConfirmed=false | 验证编辑验证功能   |

### 12.7 错误处理

| 场景               | 处理方式                |
| ---------------- | ------------------- |
| 无效 --case 值      | 列出可用 case，退出码=1     |
| 数据库初始化失败         | 输出错误，退出码=1          |
| 单个测试异常           | 捕获异常，标记 FAIL，继续执行后续 |
| 外部服务不可用 (OCR/AI) | 标记 SKIP（非失败），说明原因   |
| 测试数据清理失败         | 输出警告，不阻塞退出          |
