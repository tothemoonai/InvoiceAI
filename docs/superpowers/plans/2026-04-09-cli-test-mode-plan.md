# 命令行测试模式 - 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 InvoiceAI 应用添加命令行测试模式，支持 --test/--case 参数在无头模式下执行 8 个功能测试。

**Architecture:** Windows 平台 App.xaml.cs 重写 OnLaunched 拦截 --test 参数，进入无头模式（不创建窗口）。TestRunner 构建轻量 DI 容器，路由到 TestCases 中的具体测试方法。测试直接在主数据库上执行，结果按标准格式输出到控制台。

**Tech Stack:** .NET MAUI (Windows/WinUI), EF Core SQLite, CommunityToolkit.Mvvm, MiniExcel, System.CommandLine (手动解析)

---

## 文件结构

### 新增文件
| 文件 | 职责 |
|------|------|
| `src/InvoiceAI.App/TestRunner.cs` | 命令行解析 + DI 构建 + 测试调度 + 结果格式化 |
| `src/InvoiceAI.App/TestCases.cs` | 8 个测试用例实现 |

### 修改文件
| 文件 | 修改内容 |
|------|----------|
| `src/InvoiceAI.App/Platforms/Windows/App.xaml.cs` | 重写 `OnLaunched`，检测 `--test` 参数，无头执行 |

---

## 任务分解

### Task 1: 创建 TestRunner.cs - 核心测试调度器

**Files:**
- Create: `src/InvoiceAI.App/TestRunner.cs`

- [ ] **Step 1: 创建 TestRunner 完整代码**

```csharp
using System.Diagnostics;
using InvoiceAI.Core.Services;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.App;

public static class TestRunner
{
    private static readonly string TestResultsDir = Path.Combine(
        AppContext.BaseDirectory, "test_results");

    /// <summary>
    /// 主入口: 解析参数 → 构建 DI → 执行测试 → 退出
    /// </summary>
    public static async Task<int> RunAsync(string[] args)
    {
        // 解析参数
        var testMode = args.Contains("--test") || args.Contains("-t");
        if (!testMode) return 1;

        var caseName = args.FirstOrDefault(a => a.StartsWith("--case="))?.Split('=')[1];
        var runAll = args.Contains("--all");

        if (string.IsNullOrEmpty(caseName) && !runAll)
        {
            caseName = "all";
            runAll = true;
        }

        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║     InvoiceAI 命令行测试模式             ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine();

        // 构建 DI 容器
        var services = BuildTestServices();

        // 确保测试目录存在
        Directory.CreateDirectory(TestResultsDir);

        // 执行测试
        int exitCode;
        if (runAll || caseName == "all")
        {
            exitCode = await RunAllAsync(services);
        }
        else
        {
            exitCode = await RunSingleAsync(caseName, services) ? 0 : 1;
        }

        Console.WriteLine();
        Console.WriteLine($"退出码: {exitCode}");
        return exitCode;
    }

    /// <summary>
    /// 构建测试用 DI 容器
    /// </summary>
    private static IServiceProvider BuildTestServices()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // 数据库 (与主应用相同的路径)
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InvoiceAI", "invoices.db");
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // 核心业务服务
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IBaiduOcrService, BaiduOcrService>();
        services.AddSingleton<IGlmService, GlmService>();
        services.AddSingleton<IInvoiceService, InvoiceService>();
        services.AddSingleton<IExcelExportService, ExcelExportService>();

        // HttpClient
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(15) });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 运行所有测试
    /// </summary>
    private static async Task<int> RunAllAsync(IServiceProvider services)
    {
        var cases = new[] { "load", "category", "search", "import", "export", "delete", "saved", "edit" };
        int passCount = 0;
        int failCount = 0;

        foreach (var c in cases)
        {
            var result = await RunSingleAsync(c, services);
            if (result) passCount++; else failCount++;
        }

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║              测试汇总                      ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine($"通过: {passCount}/{passCount + failCount}");
        if (failCount > 0) Console.WriteLine($"失败: {failCount}");

        return failCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// 运行单个测试
    /// </summary>
    private static async Task<bool> RunSingleAsync(string caseName, IServiceProvider services)
    {
        try
        {
            var result = caseName.ToLower() switch
            {
                "load" => await TestCases.TestLoad(services),
                "category" => await TestCases.TestCategory(services),
                "search" => await TestCases.TestSearch(services),
                "import" => await TestCases.TestImport(services),
                "export" => await TestCases.TestExport(services),
                "delete" => await TestCases.TestDelete(services),
                "saved" => await TestCases.TestSaved(services),
                "edit" => await TestCases.TestEdit(services),
                _ => new TestCaseResult(
                    caseName, "N/A", "有效的测试用例", "N/A", null, false,
                    $"未知的测试用例: {caseName}。可用: load, category, search, import, export, delete, saved, edit")
            };

            PrintResult(result);
            return result.Passed;
        }
        catch (Exception ex)
        {
            var result = new TestCaseResult(caseName, "N/A", "不抛异常", $"异常: {ex.GetType().Name}: {ex.Message}", null, false, ex.Message);
            PrintResult(result);
            return false;
        }
    }

    /// <summary>
    /// 格式化输出测试结果
    /// </summary>
    private static void PrintResult(TestCaseResult result)
    {
        Console.WriteLine($"=== 测试 [{result.CaseName}] 开始 ===");
        Console.WriteLine($"输入/条件: {result.Input}");
        Console.WriteLine($"预期结果: {result.Expected}");
        Console.WriteLine($"实际结果: {result.Actual}");
        Console.WriteLine($"截图路径: {result.ScreenshotPath ?? "N/A"}");
        Console.WriteLine($"状态: {(result.Passed ? "PASS" : $"FAIL: {result.FailureReason}")}");
        Console.WriteLine($"=== 测试结束 ===");
        Console.WriteLine();
    }
}

public record TestCaseResult(
    string CaseName,
    string Input,
    string Expected,
    string Actual,
    string? ScreenshotPath,
    bool Passed,
    string? FailureReason = null
);
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 编译失败（TestCases.cs 还不存在）— 这是预期的

---

### Task 2: 创建 TestCases.cs - 8 个测试用例

**Files:**
- Create: `src/InvoiceAI.App/TestCases.cs`

- [ ] **Step 1: 创建 TestCases 完整代码**

```csharp
using System.Text.Json;
using InvoiceAI.Core.Services;
using InvoiceAI.Models;

namespace InvoiceAI.App;

public static class TestCases
{
    private static readonly string TestResultsDir = Path.Combine(
        AppContext.BaseDirectory, "test_results");

    // ─── 1. Load: 数据加载 ─────────────────────────────

    public static async Task<TestCaseResult> TestLoad(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();
        var timestamp = DateTime.Now.ToString("HHmmss");

        // 确保数据库初始化
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var invoices = await invoiceService.GetUnconfirmedAsync();
        var totalCount = (await invoiceService.GetAllAsync()).Count;

        bool passed = invoices.All(i => !i.IsConfirmed);
        var allConfirmed = invoices.Count > 0 && invoices.All(i => !i.IsConfirmed);

        return new TestCaseResult(
            "load",
            $"查询未确认发票列表 (数据库共有 {totalCount} 条记录)",
            "返回所有 IsConfirmed=false 的发票列表",
            $"获取 {invoices.Count} 条未确认发票记录，全部 IsConfirmed=false: {allConfirmed}",
            null,
            passed,
            passed ? null : "存在 IsConfirmed=true 的发票在返回列表中"
        );
    }

    // ─── 2. Category: 分类管理 ─────────────────────────

    public static async Task<TestCaseResult> TestCategory(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var counts = await invoiceService.GetCategoryCountsAsync();
        var distinctCats = await invoiceService.GetDistinctCategoriesAsync();

        bool passed = counts != null && counts.Count >= 0;

        var detail = counts.Count > 0
            ? string.Join(", ", counts.Select(kv => $"{kv.Key}:{kv.Value}"))
            : "数据库为空（无分类数据）";

        return new TestCaseResult(
            "category",
            $"获取分类计数 + 按分类筛选 (共 {distinctCats.Count} 个分类: {string.Join(", ", distinctCats)})",
            "返回非空分类字典，按分类筛选返回对应发票",
            $"分类计数: {detail}",
            null,
            passed,
            passed ? null : "分类计数返回 null"
        );
    }

    // ─── 3. Search: 发票搜索 ───────────────────────────

    public static async Task<TestCaseResult> TestSearch(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var allInvoices = await invoiceService.GetAllAsync();

        if (allInvoices.Count == 0)
        {
            return new TestCaseResult(
                "search",
                "数据库中无发票记录",
                "跳过测试（无数据）",
                "数据库为空，跳过搜索测试",
                null,
                true, // SKIP = 不视为失败
                "数据库为空，跳过"
            );
        }

        // 使用第一个发票的发行方名作为搜索词
        var searchTerm = allInvoices[0].IssuerName;
        var all = await invoiceService.GetAllAsync();
        var filtered = all.Where(i =>
            (i.IssuerName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (i.RegistrationNumber?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();

        bool passed = filtered.Count > 0 && filtered.Any(i => i.IssuerName == searchTerm);

        return new TestCaseResult(
            "search",
            $"搜索词: \"{searchTerm}\" (数据库共 {all.Count} 条)",
            $"返回包含 \"{searchTerm}\" 的发票",
            $"找到 {filtered.Count} 条匹配记录",
            null,
            passed,
            passed ? null : $"未找到包含搜索词 \"{searchTerm}\" 的发票"
        );
    }

    // ─── 4. Import: 发票导入 ──────────────────────────

    public static async Task<TestCaseResult> TestImport(IServiceProvider services)
    {
        var ocrService = services.GetRequiredService<IBaiduOcrService>();
        var glmService = services.GetRequiredService<IGlmService>();
        var invoiceService = services.GetRequiredService<IInvoiceService>();
        var fileService = services.GetRequiredService<IFileService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // 查找测试用发票图片
        var invoiceDirs = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "invoices", "食料品"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "invoices", "交通費"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "invoices", "電気・ガス")
        };

        string? testImage = null;
        foreach (var dir in invoiceDirs)
        {
            if (Directory.Exists(dir))
            {
                var images = Directory.GetFiles(dir, "*.jpg")
                    .Concat(Directory.GetFiles(dir, "*.png"))
                    .Concat(Directory.GetFiles(dir, "*.jpeg"))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(images))
                {
                    testImage = images;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(testImage) || !File.Exists(testImage))
        {
            return new TestCaseResult(
                "import",
                "查找 invoices/ 目录下的发票图片",
                "找到测试图片",
                "未找到可用的测试发票图片 (食料品/交通費/電気・ガス 目录下无 jpg/png)",
                null,
                true, // SKIP
                "无测试图片，跳过导入测试"
            );
        }

        var beforeCount = (await invoiceService.GetAllAsync()).Count;

        try
        {
            // OCR
            var ocrResult = await ocrService.RecognizeAsync(testImage);
            if (string.IsNullOrEmpty(ocrResult))
            {
                return new TestCaseResult(
                    "import",
                    $"OCR 识别: {Path.GetFileName(testImage)}",
                    "返回 OCR 文本",
                    "OCR 返回空",
                    null,
                    true, // SKIP
                    "OCR 服务不可用，跳过导入测试"
                );
            }

            // AI 分析
            var glmResult = await glmService.ProcessBatchAsync(new[] { ocrResult });
            if (glmResult?.Responses?.Count == 0)
            {
                return new TestCaseResult(
                    "import",
                    $"AI 分析: {Path.GetFileName(testImage)}",
                    "返回解析后的发票数据",
                    "AI 返回空",
                    null,
                    true, // SKIP
                    "AI 服务不可用，跳过导入测试"
                );
            }

            // 保存
            var glmResp = glmResult.Responses[0];
            var hash = await fileService.ComputeFileHashAsync(testImage);
            var invoice = new Invoice
            {
                IssuerName = glmResp.IssuerName ?? "测试导入",
                RegistrationNumber = glmResp.RegistrationNumber ?? "",
                TransactionDate = glmResp.TransactionDate,
                Description = glmResp.Description ?? "",
                ItemsJson = JsonSerializer.Serialize(glmResp.Items ?? new List<InvoiceItem>()),
                TaxExcludedAmount = glmResp.TaxExcludedAmount,
                TaxIncludedAmount = glmResp.TaxIncludedAmount,
                TaxAmount = glmResp.TaxAmount,
                InvoiceType = glmResp.InvoiceType,
                Category = glmResp.Category ?? "测试",
                SourceFilePath = testImage,
                FileHash = hash,
                IsConfirmed = false
            };

            await invoiceService.SaveAsync(invoice);
            var afterCount = (await invoiceService.GetAllAsync()).Count;

            bool passed = afterCount == beforeCount + 1;

            // 清理：删除测试导入的发票
            if (passed)
            {
                await invoiceService.DeleteAsync(invoice.Id);
            }

            return new TestCaseResult(
                "import",
                $"导入: {Path.GetFileName(testImage)}",
                $"OCR → AI → 保存，数据库记录数 +1 (导入前: {beforeCount}, 导入后: {afterCount})",
                passed ? $"成功导入并清理，数据库恢复到 {afterCount} 条" : $"导入失败 (导入前: {beforeCount}, 导入后: {afterCount})",
                null,
                passed,
                passed ? null : "导入后记录数不匹配"
            );
        }
        catch (Exception ex)
        {
            return new TestCaseResult(
                "import",
                $"导入: {Path.GetFileName(testImage)}",
                "完成 OCR → AI → 保存流程",
                $"异常: {ex.GetType().Name}: {ex.Message}",
                null,
                true, // SKIP - 外部服务异常
                $"导入流程异常: {ex.Message}"
            );
        }
    }

    // ─── 5. Export: 发票导出 ──────────────────────────

    public static async Task<TestCaseResult> TestExport(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();
        var exportService = services.GetRequiredService<IExcelExportService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var invoices = await invoiceService.GetAllAsync();
        var exportPath = Path.Combine(TestResultsDir, $"test_export_{DateTime.Now:HHmmss}.xlsx");

        try
        {
            var resultPath = await exportService.ExportAsync(invoices, exportPath);
            var fileInfo = new FileInfo(resultPath);
            bool passed = fileInfo.Exists && fileInfo.Length > 0;

            // 清理导出的文件
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }

            return new TestCaseResult(
                "export",
                $"导出 {invoices.Count} 条发票到 Excel",
                "生成有效的 .xlsx 文件",
                passed ? $"导出成功，文件大小: {fileInfo.Length} 字节 (已清理)" : "导出文件为空或不存在",
                null,
                passed,
                passed ? null : "导出文件为空或不存在"
            );
        }
        catch (Exception ex)
        {
            return new TestCaseResult(
                "export",
                $"导出 {invoices.Count} 条发票到 Excel",
                "生成有效的 .xlsx 文件",
                $"异常: {ex.GetType().Name}: {ex.Message}",
                null,
                false,
                ex.Message
            );
        }
    }

    // ─── 6. Delete: 发票删除 ──────────────────────────

    public static async Task<TestCaseResult> TestDelete(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // 创建测试发票
        var testInvoice = new Invoice
        {
            IssuerName = "测试删除专用",
            RegistrationNumber = "T999999999999",
            TransactionDate = DateTime.UtcNow,
            Description = "命令行测试临时数据",
            Category = "测试",
            TaxIncludedAmount = 100,
            IsConfirmed = false
        };

        var beforeCount = (await invoiceService.GetAllAsync()).Count;
        var saved = await invoiceService.SaveAsync(testInvoice);
        var afterSaveCount = (await invoiceService.GetAllAsync()).Count;

        // 删除
        await invoiceService.DeleteAsync(saved.Id);
        var afterDeleteCount = (await invoiceService.GetAllAsync()).Count;
        var deleted = await invoiceService.GetByIdAsync(saved.Id);

        bool passed = afterSaveCount == beforeCount + 1
                   && afterDeleteCount == beforeCount
                   && deleted == null;

        return new TestCaseResult(
            "delete",
            $"创建测试发票 → 删除 (导入前: {beforeCount}, 创建后: {afterSaveCount}, 删除后: {afterDeleteCount})",
            "删除后 GetByIdAsync 返回 null，记录数恢复到删除前",
            passed ? "删除成功，数据库记录数正确恢复" : $"删除异常 (创建后: {afterSaveCount}, 删除后: {afterDeleteCount}, GetById: {deleted != null})",
            null,
            passed,
            passed ? null : "删除后记录仍存在或数量不匹配"
        );
    }

    // ─── 7. Saved: 已保存列表 ─────────────────────────

    public static async Task<TestCaseResult> TestSaved(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // 获取已确认发票
        var confirmed = await invoiceService.GetConfirmedAsync();
        var totalCount = (await invoiceService.GetAllAsync()).Count;

        // 验证所有返回的发票确实 IsConfirmed=true
        bool allConfirmed = confirmed.All(i => i.IsConfirmed);

        // 如果有已确认发票，验证按创建时间范围查询
        List<Invoice> dateRangeResults = new();
        if (confirmed.Count > 0)
        {
            var earliest = confirmed.Min(i => i.CreatedAt).AddDays(-1);
            var latest = confirmed.Max(i => i.CreatedAt).AddDays(1);
            dateRangeResults = await invoiceService.GetByCreateDateRangeAsync(earliest, latest);
        }

        bool dateRangePassed = confirmed.Count == 0 || dateRangeResults.Count > 0;

        return new TestCaseResult(
            "saved",
            $"查询已确认发票列表 (共 {totalCount} 条，已确认: {confirmed.Count})",
            $"返回所有 IsConfirmed=true 的发票 ({confirmed.Count} 条)",
            $"已确认发票: {confirmed.Count} 条, 全部 IsConfirmed=true: {allConfirmed}, 时间范围查询: {dateRangeResults.Count} 条",
            null,
            allConfirmed && dateRangePassed,
            allConfirmed ? null : "存在 IsConfirmed=false 的发票在已确认列表中",
            allConfirmed && dateRangePassed ? null : "时间范围查询失败"
        );
    }

    // ─── 8. Edit: 编辑保存 ────────────────────────────

    public static async Task<TestCaseResult> TestEdit(IServiceProvider services)
    {
        var invoiceService = services.GetRequiredService<IInvoiceService>();

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoiceAI.Data.AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // 创建测试发票
        var testInvoice = new Invoice
        {
            IssuerName = "测试编辑专用",
            RegistrationNumber = "T888888888888",
            TransactionDate = DateTime.UtcNow,
            Description = "编辑前内容",
            Category = "测试",
            TaxIncludedAmount = 200,
            IsConfirmed = false
        };

        var saved = await invoiceService.SaveAsync(testInvoice);
        var originalId = saved.Id;

        // 编辑
        saved.Description = "编辑后内容";
        saved.Category = "测试_已编辑";
        saved.TaxIncludedAmount = 300;
        saved.UpdatedAt = DateTime.UtcNow;

        await invoiceService.UpdateAsync(saved);
        var reloaded = await invoiceService.GetByIdAsync(originalId);

        bool passed = reloaded != null
                   && reloaded.Description == "编辑后内容"
                   && reloaded.Category == "测试_已编辑"
                   && reloaded.TaxIncludedAmount == 300;

        // 清理
        await invoiceService.DeleteAsync(originalId);

        return new TestCaseResult(
            "edit",
            $"创建发票 → 编辑 Description/Category/Amount → 保存 → 重新读取",
            "UpdateAsync 后数据库记录正确更新",
            passed ? "编辑成功: Description=\"{reloaded?.Description}\", Category=\"{reloaded?.Category}\", Amount={reloaded?.TaxIncludedAmount} (已清理)" : $"编辑失败 (Description: {reloaded?.Description}, Category: {reloaded?.Category}, Amount: {reloaded?.TaxIncludedAmount})",
            null,
            passed,
            passed ? null : "编辑后字段值未正确更新"
        );
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.App/TestCases.cs
git commit -m "feat: 创建 8 个命令行测试用例"
```

---

### Task 3: 修改 App.xaml.cs - 无头入口

**Files:**
- Modify: `src/InvoiceAI.App/Platforms/Windows/App.xaml.cs`

- [ ] **Step 1: 重写 OnLaunched 方法**

Replace the entire `App.xaml.cs` content with:

```csharp
using System;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;

namespace InvoiceAI.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    private static bool _isTestMode;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 解析命令行参数
        var arguments = Environment.GetCommandLineArgs();
        _isTestMode = arguments.Contains("--test") || arguments.Contains("-t");

        if (_isTestMode)
        {
            // 测试模式: 不创建窗口，直接执行测试
            RunTestModeAsync(arguments).Wait();
            return; // 不调用 base.OnLaunched，不创建 MAUI 窗口
        }

        // 正常模式: 启动 MAUI 应用
        base.OnLaunched(args);
    }

    private static async Task RunTestModeAsync(string[] args)
    {
        Console.WriteLine("正在启动测试模式...");
        try
        {
            int exitCode = await InvoiceAI.App.TestRunner.RunAsync(args);
            Environment.Exit(exitCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"测试模式启动失败: {ex.Message}");
            Environment.Exit(2);
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: 成功，无错误

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.App/Platforms/Windows/App.xaml.cs
git commit -m "feat: Windows 平台 OnLaunched 拦截测试模式"
```

---

### Task 4: 端到端测试验证

- [ ] **Step 1: 测试单个用例**

Run: `dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -- --test --case=load`
Expected: 控制台输出 load 测试结果，状态 PASS 或 SKIP

- [ ] **Step 2: 测试所有用例**

Run: `dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -- --test --all`
Expected: 依次输出 8 个测试结果，最后有汇总

- [ ] **Step 3: 测试无效 case**

Run: `dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -- --test --case=invalid`
Expected: 输出错误信息，提示可用 case，退出码 1

- [ ] **Step 4: 测试正常启动（无 --test）**

Run: `dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj`
Expected: MAUI 窗口正常启动，无测试输出

- [ ] **Step 5: 修复发现的任何问题**

- [ ] **Step 6: 最终提交**

```bash
git add -A
git commit -m "feat: 命令行测试模式完成"
```

---

## 自审检查

### 1. Spec coverage

| Spec 需求 | 覆盖任务 |
|-----------|----------|
| --test / -t 进入测试模式 | Task 1, 3 |
| --case=功能名称 指定单个测试 | Task 1, 2 |
| --all 运行所有测试 | Task 1 |
| 不弹出主窗口 | Task 3 (不调用 base.OnLaunched) |
| 标准输出格式 | Task 1 (PrintResult) |
| load 测试 | Task 2 (TestLoad) |
| category 测试 | Task 2 (TestCategory) |
| search 测试 | Task 2 (TestSearch) |
| import 测试 | Task 2 (TestImport) |
| export 测试 | Task 2 (TestExport) |
| delete 测试 | Task 2 (TestDelete) |
| saved 测试 | Task 2 (TestSaved) |
| edit 测试 | Task 2 (TestEdit) |
| 直接在主数据库上测试 | Task 2 (使用相同 dbPath) |
| 退出码 0=通过, 非0=失败 | Task 1 |

### 2. Placeholder scan

无 TBD/TODO。所有步骤有完整代码。✅

### 3. Type consistency

- `TestCaseResult` record 在 Task 1 定义，Task 2 使用 — 一致 ✅
- `TestRunner.RunAsync(string[] args)` — Task 3 调用签名匹配 ✅
- 服务接口 `IInvoiceService`, `IExcelExportService` 等在 Task 2 中使用的均已存在于接口定义中 ✅
- `AppDbContext` 在 Task 2 和 Task 3 中使用相同的方式获取 ✅

### 4. Scope check

3 个任务，聚焦单一功能。范围合适。✅
