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
