using System.Text.Json;
using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

public class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(SettingsPath)) return;
        var json = await File.ReadAllTextAsync(SettingsPath);
        Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json);
    }
}