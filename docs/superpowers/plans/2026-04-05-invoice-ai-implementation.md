# InvoiceAI 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个 Windows 桌面优先的日本发票智能识别与管理工具，支持 OCR → AI 整理 → SQLite 存储 → 分类查询 + Excel 导出。

**Architecture:** 分层架构（App → Core → Data → Models），MVVM + C# Markup，DI 注入。Windows 11 Fluent 风格三栏布局。

**Tech Stack:** .NET MAUI 9, C# Markup (CommunityToolkit.Maui.Markup), CommunityToolkit.Mvvm, EF Core + SQLite, EPPlus, 百度 OCR API, GLM API (bigmodel.cn)

**Spec:** `docs/superpowers/specs/2026-04-05-invoice-ai-design.md`

---

## 文件清单

### InvoiceAI.Models
- `src/InvoiceAI.Models/InvoiceAI.Models.csproj`
- `src/InvoiceAI.Models/Invoice.cs` — 发票实体
- `src/InvoiceAI.Models/InvoiceItem.cs` — 明细行模型
- `src/InvoiceAI.Models/InvoiceType.cs` — 适格类型枚举
- `src/InvoiceAI.Models/GlmInvoiceResponse.cs` — GLM 返回结构映射

### InvoiceAI.Data
- `src/InvoiceAI.Data/InvoiceAI.Data.csproj`
- `src/InvoiceAI.Data/AppDbContext.cs` — EF Core DbContext
- `src/InvoiceAI.Data/InvoiceConfiguration.cs` — 字段映射
- `src/InvoiceAI.Data/Migrations/` — Code First 迁移

### InvoiceAI.Core
- `src/InvoiceAI.Core/InvoiceAI.Core.csproj`
- `src/InvoiceAI.Core/Services/IBaiduOcrService.cs`
- `src/InvoiceAI.Core/Services/BaiduOcrService.cs`
- `src/InvoiceAI.Core/Services/IGlmService.cs`
- `src/InvoiceAI.Core/Services/GlmService.cs`
- `src/InvoiceAI.Core/Services/IInvoiceService.cs`
- `src/InvoiceAI.Core/Services/InvoiceService.cs`
- `src/InvoiceAI.Core/Services/IExcelExportService.cs`
- `src/InvoiceAI.Core/Services/ExcelExportService.cs`
- `src/InvoiceAI.Core/Services/IFileService.cs`
- `src/InvoiceAI.Core/Services/FileService.cs`
- `src/InvoiceAI.Core/Services/IAppSettingsService.cs`
- `src/InvoiceAI.Core/Services/AppSettingsService.cs`
- `src/InvoiceAI.Core/Prompts/InvoicePrompt.cs` — GLM Prompt 模板
- `src/InvoiceAI.Core/ViewModels/MainViewModel.cs`
- `src/InvoiceAI.Core/ViewModels/InvoiceDetailViewModel.cs`
- `src/InvoiceAI.Core/ViewModels/ImportViewModel.cs`
- `src/InvoiceAI.Core/ViewModels/SettingsViewModel.cs`
- `src/InvoiceAI.Core/Helpers/AppSettings.cs` — 配置模型

### InvoiceAI.App
- `src/InvoiceAI.App/InvoiceAI.App.csproj`
- `src/InvoiceAI.App/MauiProgram.cs` — DI 注册 + 生命周期
- `src/InvoiceAI.App/App.cs` — Application 子类
- `src/InvoiceAI.App/Pages/MainPage.cs` — C# Markup 主页面
- `src/InvoiceAI.App/Pages/SettingsPage.cs` — 设置弹窗页面
- `src/InvoiceAI.App/Resources/Strings.resx` — 中文资源
- `src/InvoiceAI.App/Resources/Strings.ja.resx` — 日语资源

### Tests
- `tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj`
- `tests/InvoiceAI.Core.Tests/Services/GlmServiceTests.cs`
- `tests/InvoiceAI.Core.Tests/Services/FileServiceTests.cs`
- `tests/InvoiceAI.Core.Tests/Services/InvoiceServiceTests.cs`

---

## Task 1: 环境准备 — 安装 MAUI 工作负载

**Files:** 无文件变更

- [ ] **Step 1: 安装 MAUI Windows 桌面工作负载**

```bash
dotnet workload install maui-desktop
```

- [ ] **Step 2: 验证安装**

```bash
dotnet workload list
```

预期: 输出包含 `maui-desktop`

---

## Task 2: 解决方案与项目脚手架

**Files:**
- Create: `InvoiceAI.sln`
- Create: `src/InvoiceAI.Models/InvoiceAI.Models.csproj`
- Create: `src/InvoiceAI.Data/InvoiceAI.Data.csproj`
- Create: `src/InvoiceAI.Core/InvoiceAI.Core.csproj`
- Create: `src/InvoiceAI.App/InvoiceAI.App.csproj`
- Create: `tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj`

- [ ] **Step 1: 创建解决方案和项目**

```bash
cd C:/ClaudeCodeProject/InvoiceAI

# 创建解决方案
dotnet new sln -n InvoiceAI

# 创建 Models 类库
dotnet new classlib -n InvoiceAI.Models -o src/InvoiceAI.Models -f net9.0

# 创建 Data 类库
dotnet new classlib -n InvoiceAI.Data -o src/InvoiceAI.Data -f net9.0

# 创建 Core 类库
dotnet new classlib -n InvoiceAI.Core -o src/InvoiceAI.Core -f net9.0

# 创建 MAUI App (Windows 优先)
dotnet new maui -n InvoiceAI.App -o src/InvoiceAI.App

# 创建测试项目
dotnet new xunit -n InvoiceAI.Core.Tests -o tests/InvoiceAI.Core.Tests -f net9.0
```

- [ ] **Step 2: 添加项目引用到解决方案**

```bash
dotnet sln add src/InvoiceAI.Models/InvoiceAI.Models.csproj
dotnet sln add src/InvoiceAI.Data/InvoiceAI.Data.csproj
dotnet sln add src/InvoiceAI.Core/InvoiceAI.Core.csproj
dotnet sln add src/InvoiceAI.App/InvoiceAI.App.csproj
dotnet sln add tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj
```

- [ ] **Step 3: 配置项目间引用**

```bash
# Data → Models
dotnet add src/InvoiceAI.Data/InvoiceAI.Data.csproj reference src/InvoiceAI.Models/InvoiceAI.Models.csproj

# Core → Data, Models
dotnet add src/InvoiceAI.Core/InvoiceAI.Core.csproj reference src/InvoiceAI.Data/InvoiceAI.Data.csproj
dotnet add src/InvoiceAI.Core/InvoiceAI.Core.csproj reference src/InvoiceAI.Models/InvoiceAI.Models.csproj

# App → Core, Data, Models
dotnet add src/InvoiceAI.App/InvoiceAI.App.csproj reference src/InvoiceAI.Core/InvoiceAI.Core.csproj
dotnet add src/InvoiceAI.App/InvoiceAI.App.csproj reference src/InvoiceAI.Data/InvoiceAI.Data.csproj
dotnet add src/InvoiceAI.App/InvoiceAI.App.csproj reference src/InvoiceAI.Models/InvoiceAI.Models.csproj

# Tests → Core, Models
dotnet add tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj reference src/InvoiceAI.Core/InvoiceAI.Core.csproj
dotnet add tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj reference src/InvoiceAI.Models/InvoiceAI.Models.csproj
```

- [ ] **Step 4: 添加 NuGet 包**

```bash
# Data - EF Core SQLite
dotnet add src/InvoiceAI.Data/InvoiceAI.Data.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/InvoiceAI.Data/InvoiceAI.Data.csproj package Microsoft.EntityFrameworkCore.Design

# Core - MVVM + EPPlus
dotnet add src/InvoiceAI.Core/InvoiceAI.Core.csproj package CommunityToolkit.Mvvm
dotnet add src/InvoiceAI.Core/InvoiceAI.Core.csproj package EPPlus

# App - MAUI Toolkits
dotnet add src/InvoiceAI.App/InvoiceAI.App.csproj package CommunityToolkit.Maui
dotnet add src/InvoiceAI.App/InvoiceAI.App.csproj package CommunityToolkit.Maui.Markup

# Tests
dotnet add tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj package Moq
dotnet add tests/InvoiceAI.Core.Tests/InvoiceAI.Core.Tests.csproj package FluentAssertions
```

- [ ] **Step 5: 删除模板生成的默认文件，验证构建**

```bash
# 删除默认 Class1.cs 文件
rm -f src/InvoiceAI.Models/Class1.cs
rm -f src/InvoiceAI.Data/Class1.cs
rm -f src/InvoiceAI.Core/Class1.cs

# 删除 MAUI 模板的默认 XAML 页面（后续用 C# Markup 替代）
rm -f src/InvoiceAI.App/MainPage.xaml
rm -f src/InvoiceAI.App/MainPage.xaml.cs
rm -f src/InvoiceAI.App/App.xaml
rm -f src/InvoiceAI.App/App.xaml.cs

# 创建空的 C# 文件占位（防止编译错误）
cat > src/InvoiceAI.App/App.cs << 'CSHARP'
namespace InvoiceAI.App;

public partial class App : Application
{
    public App() { InitializeComponent(); }
    protected override Window CreateWindow(IActivationState? activationState) =>
        new Window(new ContentPage { Content = new Label { Text = "InvoiceAI" } });
}
CSHARP

cat > src/InvoiceAI.App/MauiProgram.cs << 'CSHARP'
namespace InvoiceAI.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        return builder.Build();
    }
}
CSHARP

# 验证构建
dotnet build InvoiceAI.sln
```

预期: 构建成功（可能有 MAUI App 的空项目警告，正常）

- [ ] **Step 6: Commit**

```bash
git init
git add -A
git commit -m "feat: scaffold solution with 4 projects + test project"
```

---

## Task 3: Models 层 — 实体与枚举

**Files:**
- Create: `src/InvoiceAI.Models/InvoiceType.cs`
- Create: `src/InvoiceAI.Models/InvoiceItem.cs`
- Create: `src/InvoiceAI.Models/Invoice.cs`
- Create: `src/InvoiceAI.Models/GlmInvoiceResponse.cs`

- [ ] **Step 1: 创建 InvoiceType 枚举**

```csharp
// src/InvoiceAI.Models/InvoiceType.cs
namespace InvoiceAI.Models;

public enum InvoiceType
{
    Standard,    // 標準インボイス
    Simplified,  // 簡易インボイス
    NonQualified // 非适格
}
```

- [ ] **Step 2: 创建 InvoiceItem 明细行模型**

```csharp
// src/InvoiceAI.Models/InvoiceItem.cs
namespace InvoiceAI.Models;

public class InvoiceItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TaxRate { get; set; }
    public bool IsReducedRate { get; set; }
}
```

- [ ] **Step 3: 创建 Invoice 实体**

```csharp
// src/InvoiceAI.Models/Invoice.cs
namespace InvoiceAI.Models;

public class Invoice
{
    public int Id { get; set; }
    public string IssuerName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime? TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public decimal? TaxExcludedAmount { get; set; }
    public decimal? TaxIncludedAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string? RecipientName { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public string? MissingFields { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SourceFilePath { get; set; }
    public string? FileHash { get; set; }
    public string? OcrRawText { get; set; }
    public string? GlmRawResponse { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: 创建 GlmInvoiceResponse — GLM API 返回映射**

```csharp
// src/InvoiceAI.Models/GlmInvoiceResponse.cs
using System.Text.Json.Serialization;

namespace InvoiceAI.Models;

public class GlmInvoiceResponse
{
    [JsonPropertyName("issuerName")]
    public string IssuerName { get; set; } = string.Empty;

    [JsonPropertyName("registrationNumber")]
    public string RegistrationNumber { get; set; } = string.Empty;

    [JsonPropertyName("transactionDate")]
    public string? TransactionDate { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<GlmInvoiceItem> Items { get; set; } = [];

    [JsonPropertyName("taxExcludedAmount")]
    public decimal? TaxExcludedAmount { get; set; }

    [JsonPropertyName("taxIncludedAmount")]
    public decimal? TaxIncludedAmount { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal? TaxAmount { get; set; }

    [JsonPropertyName("recipientName")]
    public string? RecipientName { get; set; }

    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; set; } = "NonQualified";

    [JsonPropertyName("missingFields")]
    public List<string> MissingFields { get; set; } = [];

    [JsonPropertyName("suggestedCategory")]
    public string SuggestedCategory { get; set; } = "その他";
}

public class GlmInvoiceItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("taxRate")]
    public int TaxRate { get; set; }

    [JsonPropertyName("isReducedRate")]
    public bool IsReducedRate { get; set; }
}
```

- [ ] **Step 5: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.Models/InvoiceAI.Models.csproj
git add -A && git commit -m "feat: add Invoice models, enums, and GLM response mapping"
```

---

## Task 4: Data 层 — EF Core + SQLite

**Files:**
- Create: `src/InvoiceAI.Data/AppDbContext.cs`
- Create: `src/InvoiceAI.Data/InvoiceConfiguration.cs`

- [ ] **Step 1: 创建 DbContext**

```csharp
// src/InvoiceAI.Data/AppDbContext.cs
using InvoiceAI.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Data;

public class AppDbContext : DbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
    }
}
```

- [ ] **Step 2: 创建 Invoice 实体配置**

```csharp
// src/InvoiceAI.Data/InvoiceConfiguration.cs
using InvoiceAI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceAI.Data;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();
        builder.Property(i => i.IssuerName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.RegistrationNumber).HasMaxLength(20);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.ItemsJson).HasColumnType("TEXT");
        builder.Property(i => i.MissingFields).HasColumnType("TEXT");
        builder.Property(i => i.Category).HasMaxLength(50);
        builder.Property(i => i.SourceFilePath).HasMaxLength(500);
        builder.Property(i => i.FileHash).HasMaxLength(64);
        builder.Property(i => i.OcrRawText).HasColumnType("TEXT");
        builder.Property(i => i.GlmRawResponse).HasColumnType("TEXT");
        builder.HasIndex(i => i.Category);
        builder.HasIndex(i => i.TransactionDate);
        builder.HasIndex(i => i.FileHash);
    }
}
```

- [ ] **Step 3: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.Data/InvoiceAI.Data.csproj
git add -A && git commit -m "feat: add EF Core DbContext with Invoice configuration"
```

> **注意:** EF 迁移将在 Task 11 (MauiProgram.cs DI 注册) 之后执行，因为 `dotnet ef migrations add` 需要启动项目已配置 DbContext。

---

## Task 5: 配置服务 — AppSettings

**Files:**
- Create: `src/InvoiceAI.Core/Helpers/AppSettings.cs`
- Create: `src/InvoiceAI.Core/Services/IAppSettingsService.cs`
- Create: `src/InvoiceAI.Core/Services/AppSettingsService.cs`

- [ ] **Step 1: 创建 AppSettings 模型**

```csharp
// src/InvoiceAI.Core/Helpers/AppSettings.cs
namespace InvoiceAI.Core.Helpers;

public class AppSettings
{
    public BaiduOcrSettings BaiduOcr { get; set; } = new();
    public GlmSettings Glm { get; set; } = new();
    public string Language { get; set; } = "zh";
    public List<string> Categories { get; set; } =
    [
        "電気・ガス", "食料品", "オフィス用品",
        "交通費", "通信費", "接待費", "その他"
    ];
}

public class BaiduOcrSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

public class GlmSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    public string Model { get; set; } = "glm-4-flash";
}
```

- [ ] **Step 2: 创建 IAppSettingsService 接口**

```csharp
// src/InvoiceAI.Core/Services/IAppSettingsService.cs
using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

public interface IAppSettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
}
```

- [ ] **Step 3: 创建 AppSettingsService 实现**

```csharp
// src/InvoiceAI.Core/Services/AppSettingsService.cs
using System.Text.Json;
using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

public class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InvoiceAI", "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Settings { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(SettingsPath)) return;
        var json = await File.ReadAllTextAsync(SettingsPath);
        Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json);
    }
}
```

- [ ] **Step 4: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj
git add -A && git commit -m "feat: add AppSettings model and settings service with JSON persistence"
```

---

## Task 6: GLM Prompt 模板

**Files:**
- Create: `src/InvoiceAI.Core/Prompts/InvoicePrompt.cs`

- [ ] **Step 1: 创建 Prompt 模板（嵌入适格請求書完整规则）**

```csharp
// src/InvoiceAI.Core/Prompts/InvoicePrompt.cs
namespace InvoiceAI.Core.Prompts;

public static class InvoicePrompt
{
    public const string SystemPrompt = """
你是一个日本发票（適格請求書/インボイス）专业分析助手。你的任务是分析 OCR 识别的日本发票文本，提取结构化信息，并严格按照日本国税庁规定判断适格状态。

## 適格請求書（標準インボイス）必须包含以下 6 项：
1. 適格請求書発行事業者の氏名または名称 および 登録番号（例：T + 13桁数字）
2. 取引年月日（实际交易日期）
3. 課税資産の譲渡等に係る資産または役務の内容（商品名/服务内容；軽減税率対象需明记「※軽減税率対象」或「※」）
4. 税率ごとに区分して合計した対価の額（税抜または税込）および適用税率（10% 和 8% 分开合计）
5. 税率ごとに区分した消費税額等（各税率对应的消费税额）
6. 書類の交付を受ける事業者の氏名または名称（接收方名；不特定多数向け可省略）

## 適格簡易請求書（簡易インボイス）：
- 项目6可省略
- 项目4和5只需满足「適用税率」或「消費税額等」其中之一

## 输出要求：
严格输出以下 JSON 格式，不要包含其他文字：
{
  "issuerName": "発行事業者名",
  "registrationNumber": "T + 13桁（无法识别则为空字符串）",
  "transactionDate": "YYYY-MM-DD 或 YYYY-MM（无法识别则为空）",
  "description": "取引内容摘要",
  "items": [{"name": "品目名", "amount": 0, "taxRate": 10, "isReducedRate": false}],
  "taxExcludedAmount": 0,
  "taxIncludedAmount": 0,
  "taxAmount": 0,
  "recipientName": "接收方名（如有）",
  "invoiceType": "Standard 或 Simplified 或 NonQualified",
  "missingFields": ["缺失的项目编号列表"],
  "suggestedCategory": "建议分类"
}

分类选项：電気・ガス、食料品、オフィス用品、交通費、通信費、接待費、その他
""";

    public static string BuildUserMessage(string ocrText)
    {
        return $"请分析以下日本发票 OCR 识别文本，提取结构化信息并判断适格状态：\n\n{ocrText}";
    }
}
```

- [ ] **Step 2: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj
git add -A && git commit -m "feat: add GLM prompt template with 适格請求書 rules"
```

---

## Task 7: 核心服务 — FileService, BaiduOcrService, GlmService

**Files:**
- Create: `src/InvoiceAI.Core/Services/IFileService.cs`
- Create: `src/InvoiceAI.Core/Services/FileService.cs`
- Create: `src/InvoiceAI.Core/Services/IBaiduOcrService.cs`
- Create: `src/InvoiceAI.Core/Services/BaiduOcrService.cs`
- Create: `src/InvoiceAI.Core/Services/IGlmService.cs`
- Create: `src/InvoiceAI.Core/Services/GlmService.cs`

**并行: 这三个服务互相独立，可用 subagent 并行实现。**

### IFileService + FileService

```csharp
// src/InvoiceAI.Core/Services/IFileService.cs
namespace InvoiceAI.Core.Services;

public interface IFileService
{
    Task<string> ComputeFileHashAsync(string filePath);
    bool IsSupportedFile(string filePath);
    IReadOnlyList<string> FilterSupportedFiles(IEnumerable<string> files);
}
```

```csharp
// src/InvoiceAI.Core/Services/FileService.cs
using System.Security.Cryptography;

namespace InvoiceAI.Core.Services;

public class FileService : IFileService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    public async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool IsSupportedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public IReadOnlyList<string> FilterSupportedFiles(IEnumerable<string> files)
    {
        return files.Where(IsSupportedFile).ToList();
    }
}
```

### IBaiduOcrService + BaiduOcrService

```csharp
// src/InvoiceAI.Core/Services/IBaiduOcrService.cs
namespace InvoiceAI.Core.Services;

public interface IBaiduOcrService
{
    Task<string> RecognizeAsync(string filePath);
}
```

```csharp
// src/InvoiceAI.Core/Services/BaiduOcrService.cs
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

public class BaiduOcrService : IBaiduOcrService
{
    private readonly HttpClient _httpClient;
    private readonly IAppSettingsService _settingsService;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public BaiduOcrService(HttpClient httpClient, IAppSettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<string> RecognizeAsync(string filePath)
    {
        var token = await GetAccessTokenAsync();
        var bytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(bytes);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        // 百度 OCR: 图片用通用文字识别（含日文），PDF 用文档识别
        var url = ext == ".pdf"
            ? $"https://aip.baidubce.com/rest/2.0/ocr/v1/doc_analysis?access_token={token}"
            : $"https://aip.baidubce.com/rest/2.0/ocr/v1/general?language_type=JAP&access_token={token}";

        var parameters = new Dictionary<string, string>
        {
            ["image"] = base64,
            ["language_type"] = "JAP"
        };

        var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        // 提取所有 words_result 中的 text
        var texts = json.TryGetProperty("words_result", out var results)
            ? string.Join("\n", results.EnumerateArray()
                .Select(r => r.TryGetProperty("words", out var w) ? w.GetString() ?? "" : ""))
            : "";

        return texts;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
            return _accessToken;

        var settings = _settingsService.Settings.BaiduOcr;
        var url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={settings.ApiKey}&client_secret={settings.SecretKey}";

        var response = await _httpClient.PostAsync(url, null);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        _accessToken = json.GetProperty("access_token").GetString();
        var expiresIn = json.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 300); // 提前5分钟刷新

        return _accessToken!;
    }
}
```

### IGlmService + GlmService

```csharp
// src/InvoiceAI.Core/Services/IGlmService.cs
using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public interface IGlmService
{
    Task<GlmInvoiceResponse> ProcessInvoiceAsync(string ocrText);
}
```

```csharp
// src/InvoiceAI.Core/Services/GlmService.cs
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceAI.Core.Helpers;
using InvoiceAI.Core.Prompts;
using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public class GlmService : IGlmService
{
    private readonly HttpClient _httpClient;
    private readonly IAppSettingsService _settingsService;

    public GlmService(HttpClient httpClient, IAppSettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<GlmInvoiceResponse> ProcessInvoiceAsync(string ocrText)
    {
        var settings = _settingsService.Settings.Glm;

        var requestBody = new
        {
            model = settings.Model,
            messages = new object[]
            {
                new { role = "system", content = InvoicePrompt.SystemPrompt },
                new { role = "user", content = InvoicePrompt.BuildUserMessage(ocrText) }
            },
            temperature = 0.1,
            max_tokens = 2000
        };

        var response = await _httpClient.PostAsJsonAsync(settings.Endpoint, requestBody);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;

        // 提取 JSON 部分（GLM 可能返回 markdown 包裹的 JSON）
        var jsonStart = content.IndexOf('{');
        var jsonEnd = content.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = content[jsonStart..(jsonEnd + 1)];
            return JsonSerializer.Deserialize<GlmInvoiceResponse>(jsonStr,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new GlmInvoiceResponse { Description = "解析失败", InvoiceType = "NonQualified" };
        }

        return new GlmInvoiceResponse
        {
            Description = "GLM 返回格式异常",
            InvoiceType = "NonQualified",
            MissingFields = ["1", "2", "3", "4", "5", "6"]
        };
    }
}
```

- [ ] **Step: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj
git add -A && git commit -m "feat: add FileService, BaiduOcrService, and GlmService implementations"
```

---

## Task 8: 核心服务 — InvoiceService, ExcelExportService

**Files:**
- Create: `src/InvoiceAI.Core/Services/IInvoiceService.cs`
- Create: `src/InvoiceAI.Core/Services/InvoiceService.cs`
- Create: `src/InvoiceAI.Core/Services/IExcelExportService.cs`
- Create: `src/InvoiceAI.Core/Services/ExcelExportService.cs`

### IInvoiceService + InvoiceService

```csharp
// src/InvoiceAI.Core/Services/IInvoiceService.cs
using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public interface IInvoiceService
{
    Task<List<Invoice>> GetAllAsync();
    Task<List<Invoice>> GetByCategoryAsync(string category);
    Task<List<Invoice>> GetByDateRangeAsync(DateTime start, DateTime end);
    Task<List<Invoice>> GetUnconfirmedAsync();
    Task<Invoice?> GetByIdAsync(int id);
    Task<Invoice> SaveAsync(Invoice invoice);
    Task UpdateAsync(Invoice invoice);
    Task DeleteAsync(int id);
    Task<bool> ExistsByHashAsync(string fileHash);
    Task<Dictionary<string, int>> GetCategoryCountsAsync();
}
```

```csharp
// src/InvoiceAI.Core/Services/InvoiceService.cs
using InvoiceAI.Data;
using InvoiceAI.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Core.Services;

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _dbContext;

    public InvoiceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Invoice>> GetAllAsync()
        => await _dbContext.Invoices.OrderByDescending(i => i.TransactionDate).ToListAsync();

    public async Task<List<Invoice>> GetByCategoryAsync(string category)
        => await _dbContext.Invoices
            .Where(i => i.Category == category)
            .OrderByDescending(i => i.TransactionDate)
            .ToListAsync();

    public async Task<List<Invoice>> GetByDateRangeAsync(DateTime start, DateTime end)
        => await _dbContext.Invoices
            .Where(i => i.TransactionDate >= start && i.TransactionDate <= end)
            .OrderByDescending(i => i.TransactionDate)
            .ToListAsync();

    public async Task<List<Invoice>> GetUnconfirmedAsync()
        => await _dbContext.Invoices
            .Where(i => !i.IsConfirmed)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

    public async Task<Invoice?> GetByIdAsync(int id)
        => await _dbContext.Invoices.FindAsync(id);

    public async Task<Invoice> SaveAsync(Invoice invoice)
    {
        invoice.CreatedAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();
        return invoice;
    }

    public async Task UpdateAsync(Invoice invoice)
    {
        invoice.UpdatedAt = DateTime.UtcNow;
        _dbContext.Invoices.Update(invoice);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var invoice = await _dbContext.Invoices.FindAsync(id);
        if (invoice != null)
        {
            _dbContext.Invoices.Remove(invoice);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByHashAsync(string fileHash)
        => await _dbContext.Invoices.AnyAsync(i => i.FileHash == fileHash);

    public async Task<Dictionary<string, int>> GetCategoryCountsAsync()
        => await _dbContext.Invoices
            .GroupBy(i => i.Category)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
}
```

### IExcelExportService + ExcelExportService

```csharp
// src/InvoiceAI.Core/Services/IExcelExportService.cs
using InvoiceAI.Models;

namespace InvoiceAI.Core.Services;

public interface IExcelExportService
{
    Task<string> ExportAsync(List<Invoice> invoices, string filePath);
}
```

```csharp
// src/InvoiceAI.Core/Services/ExcelExportService.cs
using System.Globalization;
using InvoiceAI.Models;
using OfficeOpenXml;

namespace InvoiceAI.Core.Services;

public class ExcelExportService : IExcelExportService
{
    public ExcelExportService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public async Task<string> ExportAsync(List<Invoice> invoices, string filePath)
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("发票导出");

        // 表头
        var headers = new[] { "日期", "発行事業者", "登録番号", "内容", "分类", "税抜金額", "税込金額", "消費税額", "适格状态", "缺失项" };
        for (int i = 0; i < headers.Length; i++)
            sheet.Cells[1, i + 1].Value = headers[i];

        // 样式表头
        using (var range = sheet.Cells[1, 1, 1, headers.Length])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        // 数据行
        for (int r = 0; r < invoices.Count; r++)
        {
            var inv = invoices[r];
            var row = r + 2;
            sheet.Cells[row, 1].Value = inv.TransactionDate?.ToString("yyyy-MM-dd") ?? "";
            sheet.Cells[row, 2].Value = inv.IssuerName;
            sheet.Cells[row, 3].Value = inv.RegistrationNumber;
            sheet.Cells[row, 4].Value = inv.Description;
            sheet.Cells[row, 5].Value = inv.Category;
            sheet.Cells[row, 6].Value = (double?)inv.TaxExcludedAmount;
            sheet.Cells[row, 7].Value = (double?)inv.TaxIncludedAmount;
            sheet.Cells[row, 8].Value = (double?)inv.TaxAmount;
            sheet.Cells[row, 9].Value = inv.InvoiceType switch
            {
                InvoiceType.Standard => "標準",
                InvoiceType.Simplified => "簡易",
                _ => "非适格"
            };
            sheet.Cells[row, 10].Value = inv.MissingFields;
        }

        // 汇总行
        var summaryRow = invoices.Count + 2;
        sheet.Cells[summaryRow, 5].Value = "合计";
        sheet.Cells[summaryRow, 5].Style.Font.Bold = true;
        sheet.Cells[summaryRow, 6].Formula = $"SUM(F2:F{summaryRow - 1})";
        sheet.Cells[summaryRow, 7].Formula = $"SUM(G2:G{summaryRow - 1})";
        sheet.Cells[summaryRow, 8].Formula = $"SUM(H2:H{summaryRow - 1})";

        sheet.Cells.AutoFitColumns();

        var dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        var fileInfo = new FileInfo(filePath);
        await package.SaveAsAsync(fileInfo);

        return filePath;
    }
}
```

- [ ] **Step: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj
git add -A && git commit -m "feat: add InvoiceService CRUD and ExcelExportService with EPPlus"
```

---

## Task 9: 双语资源文件

**Files:**
- Create: `src/InvoiceAI.App/Resources/Strings.resx` (中文默认)
- Create: `src/InvoiceAI.App/Resources/Strings.ja.resx` (日语)

- [ ] **Step 1: 创建中文资源文件 Strings.resx**

包含所有 UI 文字的键值对：AppName, Import, Export, Search, Settings, Save, Edit, Delete, Retry, Category, Date, IssuerName, RegistrationNumber, Amount, TaxAmount, InvoiceType, Standard, Simplified, NonQualified, MissingFields, ConfirmSave, Processing, NoInvoices, AllCategories, Language, BaiduOcrSettings, GlmSettings, ApiKey, SecretKey, TestConnection, AddCategory, RemoveCategory, DataPath, Backup, OpenDataFolder 等。

- [ ] **Step 2: 创建日语资源文件 Strings.ja.resx**

对应的日语翻译：インポート, エクスポート, 検索, 設定, 保存, 編集, 削除, 再試行 等。

- [ ] **Step 3: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
git add -A && git commit -m "feat: add bilingual resource files (Chinese + Japanese)"
```

---

## Task 10: ViewModels

**Files:**
- Create: `src/InvoiceAI.Core/ViewModels/MainViewModel.cs`
- Create: `src/InvoiceAI.Core/ViewModels/InvoiceDetailViewModel.cs`
- Create: `src/InvoiceAI.Core/ViewModels/ImportViewModel.cs`
- Create: `src/InvoiceAI.Core/ViewModels/SettingsViewModel.cs`

### MainViewModel

```csharp
// 核心职责:
// - 分类列表 (ObservableCollection<string>) + 各分类数量
// - 发票列表 (ObservableCollection<Invoice>) + 分组显示
// - 当前选中分类 SelectedCategory
// - 当前选中发票 SelectedInvoice
// - 搜索文本 SearchText → 过滤列表
// - ImportCommand → 触发导入流程
// - ExportCommand → 导出当前分类
// - LoadDataAsync() → 初始化加载数据
```

关键属性用 `[ObservableProperty]`，命令用 `[RelayCommand]`。

### InvoiceDetailViewModel

```csharp
// 核心职责:
// - CurrentInvoice (当前显示的发票)
// - InvoiceItems (解析 ItemsJson 后的明细列表)
// - InvoiceTypeDisplay (适格状态显示文本)
// - MissingFieldsDisplay (缺失项显示)
// - SaveCommand → 保存/确认发票
// - EditCategoryCommand → 修改分类
// - RetryCommand → 重新识别
```

### ImportViewModel

```csharp
// 核心职责:
// - ImportFiles (选中的文件列表)
// - ProcessingStatus (处理进度信息)
// - Progress (0-100 进度)
// - IsProcessing (bool)
// - RecognitionResults (ObservableCollection<Invoice> 识别结果)
// - ImportCommand → FilePicker → OCR → GLM 流程
// - CancelCommand → 取消处理
```

### SettingsViewModel

```csharp
// 核心职责:
// - BaiduApiKey, BaiduSecretKey (双向绑定)
// - GlmApiKey (双向绑定)
// - SelectedLanguage (中文/日语)
// - Categories (ObservableCollection<string>)
// - SaveCommand → 保存配置
// - TestBaiduConnectionCommand → 测试百度 OCR
// - TestGlmConnectionCommand → 测试 GLM
// - AddCategoryCommand / RemoveCategoryCommand
```

- [ ] **Step: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.Core/InvoiceAI.Core.csproj
git add -A && git commit -m "feat: add MainViewModel, InvoiceDetailViewModel, ImportViewModel, SettingsViewModel"
```

---

## Task 11: MAUI App 入口 — MauiProgram.cs + App.cs

**Files:**
- Modify: `src/InvoiceAI.App/MauiProgram.cs`
- Modify: `src/InvoiceAI.App/App.cs`

- [ ] **Step 1: 配置 MauiProgram.cs — DI 注册**

```csharp
// src/InvoiceAI.App/MauiProgram.cs
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
               .UseMauiCommunityToolkit()
               .UseMauiCommunityToolkitMarkup();

        // 数据库
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "invoices.db");
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // 单例服务
        builder.Services.AddSingleton(HttpClientFactory());
        builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<IBaiduOcrService, BaiduOcrService>();
        builder.Services.AddSingleton<IGlmService, GlmService>();
        builder.Services.AddSingleton<IInvoiceService, InvoiceService>();
        builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();

        // Transient: Pages + ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<InvoiceDetailViewModel>();
        builder.Services.AddTransient<ImportViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<Pages.MainPage>();

        return builder.Build();
    }

    private static HttpClient HttpClientFactory()
    {
        return new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }
}
```

- [ ] **Step 2: 配置 App.cs — 启动时初始化数据库和设置**

```csharp
// src/InvoiceAI.App/App.cs
using InvoiceAI.Core.Services;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.App;

public partial class App : Application
{
    private readonly IAppSettingsService _settingsService;
    private readonly AppDbContext _dbContext;

    public App(IAppSettingsService settingsService, AppDbContext dbContext)
    {
        _settingsService = settingsService;
        _dbContext = dbContext;
        InitializeComponent();
    }

    protected override async void OnStart()
    {
        base.OnStart();
        await _settingsService.LoadAsync();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var mainPage = Handler.MauiContext.Services.GetRequiredService<Pages.MainPage>();
        return new Window(mainPage);
    }
}
```

- [ ] **Step 3: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
git add -A && git commit -m "feat: configure DI registration and app lifecycle"
```

- [ ] **Step 4: 创建 EF Core 初始迁移（DI 已就绪，可执行迁移命令）**

```bash
dotnet ef migrations add InitialCreate \
  --project src/InvoiceAI.Data/InvoiceAI.Data.csproj \
  --startup-project src/InvoiceAI.App/InvoiceAI.App.csproj \
  --output-dir Migrations

git add -A && git commit -m "feat: add EF Core initial migration"
```

---

## Task 12: UI — MainPage (C# Markup 三栏布局)

**Files:**
- Create: `src/InvoiceAI.App/Pages/MainPage.cs`

- [ ] **Step 1: 实现 C# Markup 主页面**

三栏 Grid 布局 (180px | 280px | *)，包含：
- 左栏: 分类 CollectionView + 导入按钮
- 中栏: 本次识别高亮区 + 历史发票 CollectionView (按月分组)
- 右栏: 工具栏 + 发票详情表格 + 操作按钮

使用 `CommunityToolkit.Maui.Markup` 的流畅 API：
```csharp
new Grid {
    ColumnDefinitions = Columns.Define(180, 280, Star),
    // ...
}
```

控件使用 MAUI 9 正确 API：
- `CollectionView` (不使用已废弃的 `ListView`)
- `Border` (不使用已废弃的 `Frame`)
- `VerticalStackLayout` / `HorizontalStackLayout` (不使用兼容性 `StackLayout`)
- `Border` + `StrokeShape` 实现圆角 Fluent 风格
- `MainThread.BeginInvokeOnMainThread()` 更新 ObservableCollection

- [ ] **Step 2: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
git add -A && git commit -m "feat: implement MainPage with C# Markup three-column layout"
```

---

## Task 13: UI — 导入流程与拖拽

**Files:**
- Modify: `src/InvoiceAI.App/Pages/MainPage.cs` (添加拖拽支持)

- [ ] **Step 1: 添加文件拖拽处理**

在 MainPage 上注册 `DragOver` 和 `Drop` 事件：
```csharp
this.DragOver += (s, e) => { e.AcceptedOperation = DataPackageOperation.Copy; };
this.Drop += async (s, e) => { /* 从 DataPackageView 提取文件路径 → 触发 ImportViewModel */ };
```

- [ ] **Step 2: 添加导入进度覆盖层**

在 MainPage 上层叠加一个导入进度面板（Border + ActivityIndicator + 文件列表）。

- [ ] **Step 3: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
git add -A && git commit -m "feat: add drag-drop import and progress overlay"
```

---

## Task 14: UI — 设置弹窗

**Files:**
- Create: `src/InvoiceAI.App/Pages/SettingsPage.cs`

- [ ] **Step 1: 实现设置页面**

C# Markup 弹窗页面，包含：
- 百度 OCR API Key / Secret Key 输入框 + 测试按钮
- GLM API Key 输入框 + 测试按钮
- 语言切换 Radio 按钮
- 分类管理列表（添加/删除按钮）
- 保存/取消按钮

使用 `DisplayAlertAsync` 显示测试连接结果。

- [ ] **Step 2: 构建验证 + Commit**

```bash
dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj
git add -A && git commit -m "feat: implement Settings page with API config and category management"
```

---

## Task 15: 单元测试

**Files:**
- Create: `tests/InvoiceAI.Core.Tests/Services/GlmServiceTests.cs`
- Create: `tests/InvoiceAI.Core.Tests/Services/FileServiceTests.cs`
- Create: `tests/InvoiceAI.Core.Tests/Services/InvoiceServiceTests.cs`

- [ ] **Step 1: FileService 测试**

测试 `IsSupportedFile`、`FilterSupportedFiles`、`ComputeFileHashAsync`。

- [ ] **Step 2: GlmService 测试（mock HttpClient）**

测试 JSON 提取逻辑（正常返回、markdown 包裹、异常格式）。

- [ ] **Step 3: InvoiceService 测试（用 InMemory SQLite）**

测试 CRUD 操作、按分类查询、按日期范围查询、重复哈希检测。

- [ ] **Step 4: 运行所有测试**

```bash
dotnet test tests/InvoiceAI.Core.Tests/
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "test: add unit tests for FileService, GlmService, InvoiceService"
```

---

## Task 16: 集成构建与修复

**Files:** 视情况修改

- [ ] **Step 1: 完整构建**

```bash
dotnet build InvoiceAI.sln
```

- [ ] **Step 2: 修复所有编译错误和警告**

- [ ] **Step 3: Windows 运行测试**

```bash
dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -f net9.0-windows10.0.19041.0
```

- [ ] **Step 4: Final Commit**

```bash
git add -A && git commit -m "feat: InvoiceAI v1.0 — complete integration build"
```

---

## 任务依赖关系与并行机会

```
Task 1 (环境) ──→ Task 2 (脚手架) ──→ Task 3 (Models)
                                            │
                          ┌─────────────────┼──────────────────┐
                          ▼                 ▼                   ▼
                     Task 4 (Data)    Task 5 (Config)    Task 6 (Prompt)
                          │                 │                   │
                          └────────┬────────┴──────────────────┘
                                   ▼
                      ┌────────────┼────────────┐
                      ▼            ▼             ▼
                 Task 7 (OCR/Glm/File)   Task 9 (Resources)
                      │
                      ▼
                 Task 8 (Invoice/Excel)
                      │
                      ▼
                 Task 10 (ViewModels)
                      │
                      ▼
                 Task 11 (MauiProgram)
                      │
                      ▼
                 Task 12 (MainPage UI)
                      │
                 ┌────┴────┐
                 ▼         ▼
            Task 13    Task 14
           (Import)   (Settings)
                 │         │
                 └────┬────┘
                      ▼
                 Task 15 (Tests)
                      ▼
                 Task 16 (Integration)
```

**并行机会:**
- Task 4, 5, 6 可并行
- Task 7, 9 可并行
- Task 13, 14 可并行
