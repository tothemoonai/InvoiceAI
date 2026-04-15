namespace InvoiceAI.Core.Services;

/// <summary>
/// Manages provider fallback logic during import operations.
/// </summary>
public interface IProviderFallbackManager
{
    /// <summary>
    /// Marks a provider as verified (user tested connection successfully).
    /// </summary>
    /// <param name="provider">The provider ID (e.g., "zhipu", "nvidia", "cerebras")</param>
    Task MarkProviderVerifiedAsync(string provider);

    /// <summary>
    /// Attempts to get the next available verified provider after a failure.
    /// </summary>
    /// <param name="currentProvider">The provider that just failed</param>
    /// <param name="reason">Failure reason for logging</param>
    /// <returns>Next verified provider ID, or null if none available</returns>
    string? TryGetNextProvider(string currentProvider, string reason);

    /// <summary>
    /// Gets the list of all verified providers.
    /// </summary>
    IReadOnlyList<string> GetVerifiedProviders();

    /// <summary>
    /// Resets the current provider state (called after import completes).
    /// </summary>
    void Reset();
}
