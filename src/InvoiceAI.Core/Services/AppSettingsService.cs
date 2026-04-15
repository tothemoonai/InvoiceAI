using System.Text.Json;
using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

public class AppSettingsService : IAppSettingsService
{
    // 配置文件放在用户 AppData 目录下，不受编译/清理影响
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InvoiceAI", "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (File.Exists(SettingsPath))
        {
            var json = await File.ReadAllTextAsync(SettingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
        }
        else
        {
            // 首次运行：创建默认配置文件
            Settings = new AppSettings();
            await SaveAsync();
            LogHelper.Log($"已创建默认配置文件: {SettingsPath}");
        }
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json);
    }
}