using System.Diagnostics;
using InvoiceAI.Core.Services;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.App;

public static class TestRunner
{
    /// <summary>
    /// 查找 MAUI 应用使用的 invoices.db 数据库路径。
    /// MAUI 使用: FileSystem.AppDataDirectory + "invoices.db"
    /// 在 Windows 上, 这通常解析为:
    ///   C:\Users\<user>\AppData\Local\User Name\<package-id>\Data\invoices.db
    /// 或回退到:
    ///   %APPDATA%\InvoiceAI\invoices.db
    /// </summary>
    private static string FindDatabasePath()
    {
        // 搜索 MAUI 在 Windows 上使用的可能路径
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        
        var possiblePaths = new[]
        {
            // MAUI 常见路径 (unpackaged WinUI3)
            Path.Combine(localAppData, "User Name", "com.companyname.invoiceai.app", "Data", "invoices.db"),
            Path.Combine(localAppData, "com.companyname.invoiceai.app", "Data", "invoices.db"),
            Path.Combine(localAppData, "com.companyname.invoiceai.app", "invoices.db"),
            // 回退路径
            Path.Combine(roamingAppData, "InvoiceAI", "invoices.db"),
            Path.Combine(localAppData, "InvoiceAI", "invoices.db"),
        };
        
        // 优先返回已存在的数据库
        foreach (var p in possiblePaths)
        {
            if (File.Exists(p)) return p;
        }
        
        // 如果没有找到，返回第一个路径（会在 EnsureCreatedAsync 时创建）
        return possiblePaths[0];
    }
    
    /// <summary>
    /// 从 AppContext.BaseDirectory 向上搜索，找到包含 TEMP/testdata 和 TEMP/testlog 的项目根目录。
    /// 只有同时存在这两个子目录才认为是真正的项目根目录。
    /// </summary>
    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 15; i++)
        {
            var tempDir = Path.Combine(dir, "TEMP");
            // 只有当 TEMP/testdata 存在时才认为是项目根目录
            if (Directory.Exists(Path.Combine(tempDir, "testdata")) &&
                Directory.Exists(Path.Combine(tempDir, "testlog")))
                return dir;
            var parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent) || parent == dir) break;
            dir = parent;
        }
        // fallback: 假设 6 级 (src/InvoiceAI.App/bin/Debug/net9.0-windows10.0.19041.0/win10-x64/ → 项目根)
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    }

    private static readonly string ProjectRoot = FindProjectRoot();

    // 测试日志输出目录: TEMP\testlog
    private static readonly string TestLogDir = Path.Combine(ProjectRoot, "TEMP", "testlog");

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

        // 确保测试日志目录存在
        Directory.CreateDirectory(TestLogDir);

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

        // 数据库 (使用 MAUI 应用相同的数据库路径)
        var dbPath = FindDatabasePath();
        Console.WriteLine($"[TEST] 数据库路径: {dbPath}");
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
        var cases = new[] { "load", "category", "search", "import", "export", "delete", "imagepath", "pdfconvert", "pdfimport", "saved", "edit" };
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
                "imagepath" => await TestCases.TestImagePath(services),
                "pdfconvert" => await TestCases.TestPdfConvert(services),
                "pdfimport" => await TestCases.TestPdfImport(services),
                "saved" => await TestCases.TestSaved(services),
                "edit" => await TestCases.TestEdit(services),
                _ => new TestCaseResult(
                    caseName, "N/A", "有效的测试用例", "N/A", null, false,
                    $"未知的测试用例: {caseName}。可用: load, category, search, import, export, delete, imagepath, saved, edit")
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
