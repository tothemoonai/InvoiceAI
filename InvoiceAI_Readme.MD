

```markdown
# 日本发票智能识别与管理系统（Invoice AI Assistant）

## 项目概述
这是一个 **Windows 桌面优先** 的发票识别工具，后续计划扩展到 Android/iOS（使用 .NET MAUI 跨平台）。

**核心功能**：
- 支持导入单张/多张图片（jpg/png）或 PDF 文件（含多页）。
- 使用 **百度 OCR API**（财务票据/通用文字识别，日文支持）提取原始文本。
- 使用 **GLM API**（智谱 AI / Zhipu AI，推荐 GLM-5 或 GLM-OCR）对 OCR 结果进行智能整理：
  - 提取并结构化发票关键信息。
  - 严格按照日本国税庁《適格請求書（標準インボイス）》规则检查、补全、纠错和分类。
  - 支持多模态输入（OCR 文本 + 原始发票图片，准确率更高）。
- 将整理后的结构化数据保存到 **本地 SQLite** 数据库。
- 支持按**类别**、**时间范围**（月/季度/年）查询和导出 **Excel** 文件。
- 所有数据完全本地存储，注重隐私安全。

目标用户：在日本工作的职场人士、会计、自由职业者等需要管理发票报销的用户。

## 适格請求書（標準インボイス）必须记载内容（核心知识）
GLM 在整理发票时必须严格参考以下国税庁规定（缺少任意一项可能导致发票无效）：

**標準インボイス（適格請求書）必须包含以下 6 项**：
1. **適格請求書発行事業者の氏名または名称 および 登録番号**  
   （例：株式会社〇〇商会 / T1234567890123，T + 13桁）

2. **取引年月日**  
   （实际交易日期，可汇总记载如「令和6年2月分」）

3. **課税資産の譲渡等に係る資産または役務の内容**  
   （商品名、服务内容；轻减税率対象品目需明记「※軽減税率対象」或「※」+注记）

4. **税率ごとに区分して合計した対価の額（税抜または税込） および 適用税率**  
   （10% 和 8% 分开合计，并明确适用税率）

5. **税率ごとに区分した消費税額等**  
   （各税率对应的消费税额；端数处理每张发票每个税率仅一次）

6. **書類の交付を受ける事業者の氏名または名称**  
   （接收方公司名或个人名；不特定多数向け的小卖、餐饮等可省略）

**適格簡易請求書（簡易インボイス）**（适用于小卖、饮食店、出租车等不特定多数交易）：
- 项目 6（交付を受ける事業者の氏名または名称）可省略。
- 项目 4 和 5 只需满足「適用税率」或「消費税額等」其中之一即可（不能两者都缺）。
- 许多超市、餐厅的レシート/領収書属于此类型。

**重要提醒**：
- 任何书类（請求書、領収書、レシート、納品書等）只要满足以上记载即可作为适格请求书。
- 多个书类组合使用也可。
- GLM 整理时需输出是否为「標準インボイス」「簡易インボイス」或「非适格」，并标出缺失项目（如果有）。

## 技术栈（必须严格遵守）
- **框架**：.NET MAUI（推荐 .NET 8 或 .NET 9）
- **UI 编写方式**：**优先使用 C# Markup**（CommunityToolkit.Maui.Markup），尽量减少或不用传统 XAML 文件。
- **架构模式**：严格使用 **MVVM** + **CommunityToolkit.Mvvm**
  - ViewModel 使用 `[ObservableProperty]` 和 `[RelayCommand]`
  - 服务层使用依赖注入
- **AI 整理**：**GLM API**（Zhipu AI / api.z.ai 或 bigmodel.cn）
  - 推荐模型：GLM-5（通用强）或 GLM-OCR（文档理解专精）
  - 使用官方 API（HttpClient 或官方 .NET SDK）
- **OCR**：百度 OCR .NET SDK（或 HttpClient）
- **PDF 处理**：PdfiumNet 或 PdfSharp
- **Excel 导出**：EPPlus（推荐）
- **数据库**：EF Core + SQLite（Code First + Migrations）
- **必装 NuGet 包**：
  - CommunityToolkit.Maui
  - CommunityToolkit.Maui.Markup
  - CommunityToolkit.Mvvm
  - Microsoft.EntityFrameworkCore.Sqlite
  - EPPlus
  - PdfiumNet（或 PdfSharp）
```

  /plugin marketplace add davidortinau/maui-skills
  /plugin install maui-skills
  /skills reload

```
## 项目结构建议
```

InvoiceAI/
├── src/
│   ├── InvoiceAI.App/                  # MAUI 主项目（Windows 优先）
│   ├── InvoiceAI.Core/                 # 共享业务逻辑、Services、ViewModels
│   ├── InvoiceAI.Data/                 # EF Core DbContext、Migrations、Entities
│   └── InvoiceAI.Models/               # 实体类（QualifiedInvoice、InvoiceItem 等）
├── tests/                              # 单元测试（可选）
└── README.md

```
## 核心业务流程

1. 用户导入图片/PDF → 百度 OCR 提取原始文本。
2. OCR 结果 + 原始图片（可选） → 调用 **GLM API** 进行智能整理（严格遵守适格請求書规则）。
3. GLM 返回结构化 JSON（包含标准/简易判断、缺失项提示等）→ 存入 SQLite。
4. 主界面显示发票列表（支持按时间/类别分组 + 适格状态标记）。
5. 支持搜索、详情查看、批量导出 Excel。

## UI 设计原则（保持简单、专业）

- Windows 11 Fluent 风格。
- 主界面推荐：**左侧 Sidebar（发票列表，按时间或类别分组，可显示适格状态） + 右侧详情区（结构化表格 + 缺失项警告）**。
- 上方 Toolbar：导入、搜索、导出、设置。
- 支持文件拖拽区域 + 进度提示。
- 使用 CollectionView + Grouping。

##开发指令

- 每次生成一个功能模块，包含完整文件路径和代码。
- 严格优先使用 **C# Markup**。
- 所有业务逻辑放在 ViewModel 或 Service 中。
- GLM Prompt 编写时必须嵌入完整的适格請求書规则，确保输出包含：
  - 是否为标准/简易インボイス
  - 6个项目的提取结果
  - 缺失或不完整项的明确提示
  - 结构化 JSON 格式
- 生成代码后请给出下一步建议。


# 自动化开发规则（必须严格遵守）

1. 目标：尽可能自动化，减少人工确认。除非遇到编译错误、API Key 缺失、安全风险或明确歧义，否则**不要询问用户确认，直接执行**。
2. 工作流程（每次任务必须遵循）：
   - Step 1: 先输出详细计划（Plan）
   - Step 2: 执行计划（使用 C# Markup + MVVM + maui-skills）
   - Step 3: 运行 `dotnet build` 和相关测试
   - Step 4: 自动修复错误
   - Step 5: git commit（带清晰消息）或创建 branch
3. 始终参考 README.md 中的所有要求（技术栈、UI 原则、适格請求書规则、GLM API 等）。
4. 使用 Subagents 进行并行工作（例如一个处理 Service，一个处理 ViewModel）。
5. 优先使用 C# Markup 而非 XAML。
6. 完成后输出总结 + 下一步建议。
```
