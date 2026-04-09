using System;
using System.Linq;
using System.Threading;
using Microsoft.Maui;
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

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
