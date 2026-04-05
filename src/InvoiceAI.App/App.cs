using InvoiceAI.Core.Services;
using InvoiceAI.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.App;

public partial class App : Application
{
    private readonly IAppSettingsService _settingsService;
    private readonly AppDbContext _dbContext;

    public App(IAppSettingsService settingsService, AppDbContext dbContext)
    {
        _settingsService = settingsService;
        _dbContext = dbContext;
    }

    protected override async void OnStart()
    {
        base.OnStart();
        await _settingsService.LoadAsync();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = Handler.MauiContext.Services.GetRequiredService<Pages.MainPage>();
        return new Window(page);
    }
}
