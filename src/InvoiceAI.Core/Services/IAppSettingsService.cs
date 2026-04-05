using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

public interface IAppSettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
}