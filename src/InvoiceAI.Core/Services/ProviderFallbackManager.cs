using InvoiceAI.Core.Helpers;

namespace InvoiceAI.Core.Services;

/// <summary>
/// Manages provider fallback logic during import operations.
/// Tracks user-verified providers and provides sequential fallback.
/// </summary>
public class ProviderFallbackManager : IProviderFallbackManager
{
    private readonly IAppSettingsService _settingsService;

    public ProviderFallbackManager(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task MarkProviderVerifiedAsync(string provider)
    {
        var verified = _settingsService.Settings.Glm.VerifiedProviders;
        if (!verified.Contains(provider))
        {
            verified.Add(provider);
            await _settingsService.SaveAsync();
        }
    }

    /// <inheritdoc/>
    public string? TryGetNextProvider(string currentProvider, string reason)
    {
        var verified = _settingsService.Settings.Glm.VerifiedProviders;

        // Log the fallback attempt
        LogHelper.Log($"Provider fallback: {currentProvider} failed ({reason})");
        LogHelper.Log($"Verified providers: [{string.Join(", ", verified)}]");

        var currentIndex = verified.IndexOf(currentProvider);

        // Find next provider after current in the verified list
        for (int i = currentIndex + 1; i < verified.Count; i++)
        {
            var next = verified[i];
            LogHelper.Log($"Switching to provider: {next}");
            return next;
        }

        LogHelper.Log($"No more verified providers available");
        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetVerifiedProviders()
    {
        return _settingsService.Settings.Glm.VerifiedProviders.AsReadOnly();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // Provider state is managed by AppSettings.Glm.Provider
        // This method is a no-op but kept for interface symmetry
        LogHelper.Log("ProviderFallbackManager reset");
    }
}
