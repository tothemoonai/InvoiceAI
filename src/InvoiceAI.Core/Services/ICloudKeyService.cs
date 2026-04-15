using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public interface ICloudKeyService
{
    Task<CloudKeyConfig?> GetCloudKeysAsync(string groupId);
    Task<CloudKeyConfig?> GetCachedCloudKeysAsync();
    void ClearCachedKeys();
    bool IsCloudKeyValid(CloudKeyConfig? config);
}
