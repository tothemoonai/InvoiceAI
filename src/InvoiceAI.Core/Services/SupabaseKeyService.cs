using InvoiceAI.Core.Helpers;
using InvoiceAI.Models.Auth;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using SupabaseClient = Supabase.Client;

namespace InvoiceAI.Core.Services;

public class SupabaseKeyService : ICloudKeyService
{
    private readonly SupabaseClient _supabaseClient;
    private CloudKeyConfig? _cachedKeys;
    private readonly object _cacheLock = new();

    public SupabaseKeyService(SupabaseClient supabaseClient)
    {
        _supabaseClient = supabaseClient;
    }

    public async Task<CloudKeyConfig?> GetCloudKeysAsync(string groupId)
    {
        try
        {
            var response = await _supabaseClient
                .From<KeysRow>()
                .Where(x => x.group_id == groupId)
                .Get();

            if (response.Models?.Count > 0)
            {
                var row = response.Models[0];
                var config = new CloudKeyConfig
                {
                    OcrToken = row.ocr_token ?? string.Empty,
                    OcrEndpoint = row.ocr_endpoint ?? string.Empty,
                    ZhipuApiKey = row.zhipu_apikey,
                    ZhipuEndpoint = row.zhipu_endpoint,
                    ZhipuModel = row.zhipu_model,
                    NvidiaApiKey = row.nvidia_apikey,
                    NvidiaEndpoint = row.nvidia_endpoint,
                    NvidiaModel = row.nvidia_model,
                    CerebrasApiKey = row.cerebras_apikey,
                    CerebrasEndpoint = row.cerebras_endpoint,
                    CerebrasModel = row.cerebras_model,
                    Version = row.version ?? 1
                };

                // Thread-safe cache update
                lock (_cacheLock)
                {
                    _cachedKeys = config;
                }

                AuthAuditLogger.LogKeyFetch(groupId, true, config.Version);
                return config;
            }

            AuthAuditLogger.LogKeyFetch(groupId, false, null);
            return null;
        }
        catch
        {
            AuthAuditLogger.LogKeyFetch(groupId, false, null);
            // Log error but don't throw - caller handles null
            return null;
        }
    }

    public Task<CloudKeyConfig?> GetCachedCloudKeysAsync()
    {
        lock (_cacheLock)
        {
            return Task.FromResult(_cachedKeys);
        }
    }

    public void ClearCachedKeys()
    {
        lock (_cacheLock)
        {
            _cachedKeys = null;
        }
    }

    public bool IsCloudKeyValid(CloudKeyConfig? config)
    {
        if (config == null) return false;

        // Check OCR configuration
        bool hasValidOcr = !string.IsNullOrEmpty(config.OcrToken) &&
                           !string.IsNullOrEmpty(config.OcrEndpoint);

        // Check at least one GLM provider is configured
        bool hasValidGlmProvider =
            (!string.IsNullOrEmpty(config.ZhipuApiKey) && !string.IsNullOrEmpty(config.ZhipuModel)) ||
            (!string.IsNullOrEmpty(config.NvidiaApiKey) && !string.IsNullOrEmpty(config.NvidiaModel)) ||
            (!string.IsNullOrEmpty(config.CerebrasApiKey) && !string.IsNullOrEmpty(config.CerebrasModel));

        return hasValidOcr && hasValidGlmProvider;
    }

    // Supabase table mapping
    [Table("keys")]
    private class KeysRow : BaseModel
    {
        public string group_id { get; set; } = string.Empty;
        public string ocr_token { get; set; } = string.Empty;
        public string ocr_endpoint { get; set; } = string.Empty;
        public string? zhipu_apikey { get; set; }
        public string? zhipu_endpoint { get; set; }
        public string? zhipu_model { get; set; }
        public string? nvidia_apikey { get; set; }
        public string? nvidia_endpoint { get; set; }
        public string? nvidia_model { get; set; }
        public string? cerebras_apikey { get; set; }
        public string? cerebras_endpoint { get; set; }
        public string? cerebras_model { get; set; }
        public int? version { get; set; }
    }
}
