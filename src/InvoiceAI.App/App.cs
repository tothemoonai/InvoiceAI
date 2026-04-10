using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Data;
using InvoiceAI.App.Utils;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.App;

public partial class App : Application
{
    private readonly IAppSettingsService _settingsService;
    private readonly AppDbContext _dbContext;
    private readonly IServiceProvider _services;

    public App(IAppSettingsService settingsService, AppDbContext dbContext, IServiceProvider services)
    {
        _settingsService = settingsService;
        _dbContext = dbContext;
        _services = services;

        // 监听系统主题变化
        RequestedThemeChanged += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Theme changed: {RequestedTheme}");
        };
    }

    protected override async void OnStart()
    {
        base.OnStart();
        await _settingsService.LoadAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        // 临时诊断: 检查 IsConfirmed 数据一致性
        var total = await _dbContext.Invoices.CountAsync();
        var confirmed = await _dbContext.Invoices.CountAsync(i => i.IsConfirmed);
        var unconfirmed = await _dbContext.Invoices.CountAsync(i => !i.IsConfirmed);
        var discrepancy = total - confirmed - unconfirmed;
        System.Diagnostics.Debug.WriteLine($"[DIAG] Total={total}, Confirmed={confirmed}, Unconfirmed={unconfirmed}, Discrepancy={discrepancy}");

        if (discrepancy > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[DIAG] ⚠ 发现 {discrepancy} 条 IsConfirmed=NULL 的记录，正在修复...");
            using var conn = _dbContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Invoices SET IsConfirmed = 0 WHERE IsConfirmed IS NULL";
            var fixedCount = await cmd.ExecuteNonQueryAsync();
            await _dbContext.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"[DIAG] 已修复 {fixedCount} 条记录，设置 IsConfirmed=0");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // 应用主题设置
        ApplyThemeMode();

        try
        {
            var page = Handler.MauiContext.Services.GetRequiredService<Pages.MainPage>();
            var navPage = new NavigationPage(page)
            {
                BarBackgroundColor = ThemeManager.BrandPrimary,
                BarTextColor = Colors.White
            };
            var window = new Window(navPage);

            return window;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateWindow error: {ex}");
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "InvoiceAI", "startup_crash.log"),
                $"{DateTime.Now:O}\n{ex}");
            throw;
        }
    }

    /// <summary>
    /// 根据 AppSettings.ThemeMode 设置应用主题
    /// </summary>
    private void ApplyThemeMode()
    {
        UserAppTheme = _settingsService.Settings.ThemeMode switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified  // Auto: 跟随系统
        };
    }
}
