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
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                // 新增: 标题字体 (Poppins)
                fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
                fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemiBold");

                // 新增: 中文 (Noto Sans SC)
                fonts.AddFont("NotoSansSC-Regular.ttf", "NotoSansSCRegular");
                fonts.AddFont("NotoSansSC-Medium.ttf", "NotoSansSCMedium");

                // 新增: 日文 (Noto Sans JP)
                fonts.AddFont("NotoSansJP-Regular.ttf", "NotoSansJPRegular");
                fonts.AddFont("NotoSansJP-Medium.ttf", "NotoSansJPMedium");

                // 新增: 数字/金额 (JetBrains Mono)
                fonts.AddFont("JetBrainsMono-Medium.ttf", "JetBrainsMonoMedium");

                // 新增: 辅助文字 (Inter)
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
            });

        // Database
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "invoices.db");
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // HTTP client
        builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(15) });

        // Singleton services
        builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<IBaiduOcrService, BaiduOcrService>();
        builder.Services.AddSingleton<IGlmService, GlmService>();
        builder.Services.AddSingleton<IInvoiceService, InvoiceService>();
        builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();
        
        // Import Service (Core Logic)
        builder.Services.AddSingleton<IInvoiceImportService, InvoiceImportService>();

        // ViewModels (singleton — shared state across MainPage)
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<InvoiceDetailViewModel>();
        builder.Services.AddSingleton<ImportViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<InvoiceAI.Core.ViewModels.SavedInvoicesViewModel>();

        // Pages (transient)
        builder.Services.AddTransient<Pages.MainPage>();
        builder.Services.AddTransient<Pages.SettingsPage>();

        return builder.Build();
    }
}
