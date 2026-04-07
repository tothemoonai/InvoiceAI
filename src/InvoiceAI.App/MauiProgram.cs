using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Maui;

namespace InvoiceAI.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Database
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "invoices.db");
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // HTTP client
        builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(5) });

        // Singleton services
        builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<IBaiduOcrService, BaiduOcrService>();
        builder.Services.AddSingleton<IGlmService, GlmService>();
        builder.Services.AddSingleton<IInvoiceService, InvoiceService>();
        builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();

        // ViewModels (singleton — shared state across MainPage)
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<InvoiceDetailViewModel>();
        builder.Services.AddSingleton<ImportViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();

        // Pages (transient)
        builder.Services.AddTransient<Pages.MainPage>();
        builder.Services.AddTransient<Pages.SettingsPage>();

        return builder.Build();
    }
}
