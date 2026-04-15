using InvoiceAI.Core.Helpers;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public interface IAppSettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
    Task<EffectiveApiKeys> GetEffectiveApiKeysAsync();
}