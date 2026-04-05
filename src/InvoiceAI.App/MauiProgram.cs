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
        builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(60) });

        // Singleton services
        builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<IBaiduOcrService, BaiduOcrService>();
        builder.Services.AddSingleton<IGlmService, GlmService>();
        builder.Services.AddSingleton<IInvoiceService, InvoiceService>();
        builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();

        // ViewModels (transient — fresh per navigation)
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<InvoiceDetailViewModel>();
        builder.Services.AddTransient<ImportViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Pages (transient)
        builder.Services.AddTransient<Pages.MainPage>();

        return builder.Build();
    }
}
