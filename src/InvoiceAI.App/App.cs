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
    }

    protected override async void OnStart()
    {
        base.OnStart();
        await _settingsService.LoadAsync();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var page = Handler.MauiContext.Services.GetRequiredService<Pages.MainPage>();
            var navPage = new NavigationPage(page);
            var window = new Window(navPage);

#if WINDOWS
            SetupWindowsDragDrop(window);
#endif

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

#if WINDOWS
    private void SetupWindowsDragDrop(Window window)
    {
        // Delay setup until native window is fully created
        window.HandlerChanged += (_, _) =>
        {
            var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (nativeWindow?.Content is not Microsoft.UI.Xaml.UIElement content) return;

            content.AllowDrop = true;

            content.DragEnter += (s, args) =>
            {
                if (args.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                {
                    args.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                    var mainPage = (window.Page as NavigationPage)?.CurrentPage as Pages.MainPage;
                    mainPage?.ShowDropZone(true);
                }
            };

            content.DragLeave += (s, args) =>
            {
                var mainPage = (window.Page as NavigationPage)?.CurrentPage as Pages.MainPage;
                mainPage?.ShowDropZone(false);
            };

            content.Drop += async (s, args) =>
            {
                var mainPage = (window.Page as NavigationPage)?.CurrentPage as Pages.MainPage;
                mainPage?.ShowDropZone(false);

                try
                {
                    if (!args.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                        return;

                    var items = await args.DataView.GetStorageItemsAsync();
                    var filePaths = items
                        .OfType<global::Windows.Storage.StorageFile>()
                        .Select(f => f.Path)
                        .ToArray();

                    if (filePaths.Length == 0) return;

                    var importVm = _services.GetRequiredService<ImportViewModel>();
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await importVm.ProcessFilesCommand.ExecuteAsync(filePaths);
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Drop error: {ex.Message}");
                }
            };
        };
    }
#endif
}
