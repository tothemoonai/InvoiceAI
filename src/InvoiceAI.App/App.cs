using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Data;
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
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // 应用主题设置
        ApplyThemeMode();

        try
        {
            var page = Handler.MauiContext.Services.GetRequiredService<Pages.MainPage>();
            var window = new Window(page);

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
