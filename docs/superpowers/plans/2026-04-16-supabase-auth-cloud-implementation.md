# Supabase Authentication & Cloud API Keys Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Supabase-based user authentication with cloud API key provisioning to InvoiceAI, enabling users to login and automatically fetch cloud-configured OCR and GLM API keys.

**Architecture:** Minimal-change approach - add new Services (IAuthService, ICloudKeyService) and Models (Auth/*) without disrupting existing local key configuration. Cloud keys take priority over local keys via GetEffectiveApiKeysAsync().

**Tech Stack:** .NET MAUI 9, Supabase C# SDK, Supabase PostgreSQL, SQLite (local), System.Threading.Channels (concurrency)

---

## File Structure Overview

**New Files:**
```
src/InvoiceAI.Models/Auth/
├── CloudKeyConfig.cs           # Cloud key configuration (all GLM providers)
├── EffectiveApiKeys.cs          # Effective keys with source indicator
├── AuthState.cs                 # Authentication state (observable)
└── AuthResult.cs                # Authentication result

src/InvoiceAI.Core/Services/
├── IAuthService.cs              # Authentication service interface
├── SupabaseAuthService.cs      # Supabase Auth implementation
├── ICloudKeyService.cs          # Cloud key service interface
└── SupabaseKeyService.cs        # Supabase keys implementation

src/InvoiceAI.Core/ViewModels/
└── AuthViewModel.cs             # Login/logout UI logic

src/InvoiceAI.Core/Helpers/
├── SupabaseConfig.cs            # Supabase configuration model
├── DataProtection.cs            # Memory encryption (DPAPI)
└── AuthAuditLogger.cs           # Audit logging

tests/InvoiceAI.Core.Tests/Services/
├── SupabaseAuthServiceTests.cs
├── SupabaseKeyServiceTests.cs
└── AppSettingsServiceExtensionsTests.cs

tests/InvoiceAI.Core.Tests/ViewModels/
└── AuthViewModelTests.cs
```

**Modified Files:**
```
src/InvoiceAI.Core/Helpers/AppSettings.cs
src/InvoiceAI.Core/Services/IAppSettingsService.cs
src/InvoiceAI.Core/Services/AppSettingsService.cs
src/InvoiceAI.Core/Services/GlmService.cs
src/InvoiceAI.Core/Services/BaiduOcrService.cs (or IBaiduOcrService.cs)
src/InvoiceAI.App/MauiProgram.cs
src/InvoiceAI.App/Pages/SettingsPage.cs
```

---

## Phase 1: Models Layer

### Task 1: Create Auth Models

**Files:**
- Create: `src/InvoiceAI.Models/Auth/CloudKeyConfig.cs`

- [ ] **Step 1: Create the CloudKeyConfig model**

```csharp
namespace InvoiceAI.Models.Auth;

public class CloudKeyConfig
{
    // OCR
    public string OcrToken { get; set; } = string.Empty;
    public string OcrEndpoint { get; set; } = string.Empty;

    // GLM - Support all providers
    public string? ZhipuApiKey { get; set; }
    public string? ZhipuEndpoint { get; set; }
    public string? ZhipuModel { get; set; }

    public string? NvidiaApiKey { get; set; }
    public string? NvidiaEndpoint { get; set; }
    public string? NvidiaModel { get; set; }

    public string? CerebrasApiKey { get; set; }
    public string? CerebrasEndpoint { get; set; }
    public string? CerebrasModel { get; set; }

    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Models/Auth/CloudKeyConfig.cs
git commit -m "feat(auth): add CloudKeyConfig model with multi-provider support"
```

---

### Task 2: Create EffectiveApiKeys Model

**Files:**
- Create: `src/InvoiceAI.Models/Auth/EffectiveApiKeys.cs`

- [ ] **Step 1: Create the EffectiveApiKeys model**

```csharp
namespace InvoiceAI.Models.Auth;

public class EffectiveApiKeys
{
    public string OcrToken { get; set; } = string.Empty;
    public string OcrEndpoint { get; set; } = string.Empty;
    public string GlmApiKey { get; set; } = string.Empty;
    public string GlmEndpoint { get; set; } = string.Empty;
    public string GlmModel { get; set; } = string.Empty;
    public string GlmProvider { get; set; } = "zhipu";
    public string Source { get; set; } = "local"; // "cloud" or "local"
    public int KeyVersion { get; set; } = 1;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Models/Auth/EffectiveApiKeys.cs
git commit -m "feat(auth): add EffectiveApiKeys model with source tracking"
```

---

### Task 3: Create AuthState Model

**Files:**
- Create: `src/InvoiceAI.Models/Auth/AuthState.cs`

- [ ] **Step 1: Create the AuthState model**

```csharp
namespace InvoiceAI.Models.Auth;

public class AuthState
{
    public bool IsAuthenticated { get; set; }
    public string? UserEmail { get; set; }
    public string? UserGroup { get; set; }
    public string? ErrorMessage { get; set; }
    public bool CloudKeysAvailable { get; set; }
    public string? ActiveCloudProvider { get; set; }
    public bool IsUsingCloudKeys { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Models/Auth/AuthState.cs
git commit -m "feat(auth): add AuthState model for UI binding"
```

---

### Task 4: Create AuthResult Model

**Files:**
- Create: `src/InvoiceAI.Models/Auth/AuthResult.cs`

- [ ] **Step 1: Create the AuthResult model**

```csharp
namespace InvoiceAI.Models.Auth;

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UserEmail { get; set; }
    public string? UserGroup { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Models/Auth/AuthResult.cs
git commit -m "feat(auth): add AuthResult model for login responses"
```

---

## Phase 2: Services Layer

### Task 5: Create IAuthService Interface

**Files:**
- Create: `src/InvoiceAI.Core/Services/IAuthService.cs`

- [ ] **Step 1: Create the IAuthService interface**

```csharp
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string email, string password);
    Task SignOutAsync();
    Task<AuthState> GetAuthStateAsync();
    Task<string?> GetCurrentUserGroupAsync();
    event EventHandler<AuthState>? AuthStateChanged;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Core/Services/IAuthService.cs
git commit -m "feat(auth): add IAuthService interface"
```

---

### Task 6: Create ICloudKeyService Interface

**Files:**
- Create: `src/InvoiceAI.Core/Services/ICloudKeyService.cs`

- [ ] **Step 1: Create the ICloudKeyService interface**

```csharp
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public interface ICloudKeyService
{
    Task<CloudKeyConfig?> GetCloudKeysAsync(string groupId);
    Task<CloudKeyConfig?> GetCachedCloudKeysAsync();
    void ClearCachedKeys();
    bool IsCloudKeyValid(CloudKeyConfig? config);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Core/Services/ICloudKeyService.cs
git commit -m "feat(auth): add ICloudKeyService interface"
```

---

### Task 7: Create SupabaseConfig Helper

**Files:**
- Create: `src/InvoiceAI.Core/Helpers/SupabaseConfig.cs`

- [ ] **Step 1: Create the SupabaseConfig helper**

```csharp
namespace InvoiceAI.Core.Helpers;

public class SupabaseConfig
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Update AppSettings to include Supabase config**

```csharp
// In src/InvoiceAI.Core/Helpers/AppSettings.cs
// Add to AppSettings class:
public SupabaseConfig Supabase { get; set; } = new();
```

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.Core/Helpers/SupabaseConfig.cs src/InvoiceAI.Core/Helpers/AppSettings.cs
git commit -m "feat(auth): add SupabaseConfig and integrate with AppSettings"
```

---

### Task 8: Create DataProtection Helper

**Files:**
- Create: `src/InvoiceAI.Core/Helpers/DataProtection.cs`

- [ ] **Step 1: Create the DataProtection helper**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace InvoiceAI.Core.Helpers;

public static class DataProtection
{
    public static string Protect(string plainText)
    {
#if WINDOWS
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = DPAPI.Protect(bytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
#else
        // Fallback for non-Windows platforms
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
#endif
    }

    public static string? Unprotect(string protectedText)
    {
#if WINDOWS
        try
        {
            var protectedBytes = Convert.FromBase64String(protectedText);
            var bytes = DPAPI.Unprotect(protectedBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
#else
        return Encoding.UTF8.GetString(Convert.FromBase64String(protectedText));
#endif
    }

    public static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 8)
            return "****";
        return $"{key[..4]}...{key[^4..]}";
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Core/Helpers/DataProtection.cs
git commit -m "feat(auth): add DataProtection helper for secure storage"
```

---

### Task 9: Create AuthAuditLogger Helper

**Files:**
- Create: `src/InvoiceAI.Core/Helpers/AuthAuditLogger.cs`

- [ ] **Step 1: Create the AuthAuditLogger helper**

```csharp
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Helpers;

public static class AuthAuditLogger
{
    private static readonly string AuditLogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InvoiceAI", "audit");

    static AuthAuditLogger()
    {
        Directory.CreateDirectory(AuditLogDir);
    }

    public static void LogLogin(string email, string? group, bool success)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] LOGIN | Email={email} | Group={group} | Success={success}";
        WriteEntry(entry);
    }

    public static void LogLogout(string email)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] LOGOUT | Email={email}";
        WriteEntry(entry);
    }

    public static void LogKeyFetch(string group, bool success, int? version)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] KEY_FETCH | Group={group} | Success={success} | Version={version}";
        WriteEntry(entry);
    }

    public static void LogKeyVersionMismatch(string group, int cachedVersion, int serverVersion)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] KEY_VERSION_MISMATCH | Group={group} | Cached={cachedVersion} | Server={serverVersion}";
        WriteEntry(entry);
    }

    public static void LogSessionRefresh(bool success)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SESSION_REFRESH | Success={success}";
        WriteEntry(entry);
    }

    private static void WriteEntry(string entry)
    {
        var logFile = Path.Combine(AuditLogDir, $"auth_{DateTime.Now:yyyy-MM-dd}.log");
        try
        {
            File.AppendAllText(logFile, entry + Environment.NewLine);
        }
        catch
        {
            // Silently fail to avoid disrupting app flow
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Core/Helpers/AuthAuditLogger.cs
git commit -m "feat(auth): add AuthAuditLogger for security audit trail"
```

---

### Task 10: Implement SupabaseKeyService

**Files:**
- Create: `src/InvoiceAI.Core/Services/SupabaseKeyService.cs`
- Test: `tests/InvoiceAI.Core.Tests/Services/SupabaseKeyServiceTests.cs`

- [ ] **Step 1: Write the failing tests first**

```csharp
// tests/InvoiceAI.Core.Tests/Services/SupabaseKeyServiceTests.cs
using InvoiceAI.Core.Services;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Tests.Services;

public class SupabaseKeyServiceTests
{
    [Fact]
    public async Task GetCloudKeysAsync_ValidGroupId_ReturnsKeyConfig()
    {
        // Arrange
        var service = new SupabaseKeyService(/* mock dependencies */);

        // Act
        var result = await service.GetCloudKeysAsync("test_group");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.OcrToken);
    }

    [Fact]
    public void IsCloudKeyValid_CompleteConfig_ReturnsTrue()
    {
        // Arrange
        var service = new SupabaseKeyService();
        var config = new CloudKeyConfig
        {
            OcrToken = "test_token",
            OcrEndpoint = "https://test.com",
            NvidiaApiKey = "nvidia_key",
            NvidiaModel = "model"
        };

        // Act
        var result = service.IsCloudKeyValid(config);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCloudKeyValid_PartialConfig_ReturnsFalse()
    {
        // Arrange
        var service = new SupabaseKeyService();
        var config = new CloudKeyConfig
        {
            OcrToken = "test_token",
            // Missing endpoint and GLM keys
        };

        // Act
        var result = service.IsCloudKeyValid(config);

        // Assert
        Assert.False(result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~SupabaseKeyServiceTests"
```

Expected: FAIL with type/class not found errors

- [ ] **Step 3: Implement the SupabaseKeyService**

```csharp
// src/InvoiceAI.Core/Services/SupabaseKeyService.cs
using InvoiceAI.Core.Helpers;
using InvoiceAI.Models.Auth;
using Supabase;

namespace InvoiceAI.Core.Services;

public class SupabaseKeyService : ICloudKeyService
{
    private readonly Client _supabaseClient;
    private CloudKeyConfig? _cachedKeys;
    private readonly object _cacheLock = new();

    public SupabaseKeyService(Client supabaseClient)
    {
        _supabaseClient = supabaseClient;
    }

    public async Task<CloudKeyConfig?> GetCloudKeysAsync(string groupId)
    {
        try
        {
            var response = await _supabaseClient
                .From<KeysRow>("keys")
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
        catch (Exception ex)
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
    private class KeysRow
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~SupabaseKeyServiceTests"
```

Expected: PASS (or some tests may pass, others may need mocking)

- [ ] **Step 5: Commit**

```bash
git add src/InvoiceAI.Core/Services/SupabaseKeyService.cs tests/InvoiceAI.Core.Tests/Services/SupabaseKeyServiceTests.cs
git commit -m "feat(auth): implement SupabaseKeyService with thread-safe caching"
```

---

### Task 11: Implement SupabaseAuthService

**Files:**
- Create: `src/InvoiceAI.Core/Services/SupabaseAuthService.cs`
- Test: `tests/InvoiceAI.Core.Tests/Services/SupabaseAuthServiceTests.cs`

- [ ] **Step 1: Write the failing tests first**

```csharp
// tests/InvoiceAI.Core.Tests/Services/SupabaseAuthServiceTests.cs
using InvoiceAI.Core.Services;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Tests.Services;

public class SupabaseAuthServiceTests
{
    [Fact]
    public async Task SignInAsync_ValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var service = new SupabaseAuthService(/* mock dependencies */);

        // Act
        var result = await service.SignInAsync("test@example.com", "password");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("test@example.com", result.UserEmail);
    }

    [Fact]
    public async Task SignOutAsync_ClearsSessionAndRaisesEvent()
    {
        // Arrange
        var service = new SupabaseAuthService();
        AuthState? capturedState = null;
        service.AuthStateChanged += (s, state) => capturedState = state;

        // Act
        await service.SignOutAsync();

        // Assert
        Assert.NotNull(capturedState);
        Assert.False(capturedState.IsAuthenticated);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~SupabaseAuthServiceTests"
```

Expected: FAIL with type not found errors

- [ ] **Step 3: Implement the SupabaseAuthService**

```csharp
// src/InvoiceAI.Core/Services/SupabaseAuthService.cs
using InvoiceAI.Core.Helpers;
using InvoiceAI.Models.Auth;
using Supabase;
using Supabase.Gotrue;
using System.Threading;

namespace InvoiceAI.Core.Services;

public class SupabaseAuthService : IAuthService
{
    private readonly Client _supabaseClient;
    private AuthState _currentAuthState = new();
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public event EventHandler<AuthState>? AuthStateChanged;

    public SupabaseAuthService(Client supabaseClient)
    {
        _supabaseClient = supabaseClient;
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        try
        {
            var session = await _supabaseClient.Auth.SignIn(email, password);

            if (session?.User != null)
            {
                var groupId = ExtractGroupId(session.User);
                var newState = new AuthState
                {
                    IsAuthenticated = true,
                    UserEmail = session.User.Email,
                    UserGroup = groupId,
                    CloudKeysAvailable = !string.IsNullOrEmpty(groupId)
                };

                await UpdateAuthStateAsync(newState);
                AuthAuditLogger.LogLogin(email, groupId, true);

                return new AuthResult
                {
                    Success = true,
                    UserEmail = session.User.Email,
                    UserGroup = groupId
                };
            }

            AuthAuditLogger.LogLogin(email, null, false);
            return new AuthResult { Success = false, ErrorMessage = "登录失败" };
        }
        catch (Exception ex)
        {
            AuthAuditLogger.LogLogin(email, null, false);
            return new AuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            var email = _currentAuthState.UserEmail;
            await _supabaseClient.Auth.SignOut();
            AuthAuditLogger.LogLogout(email ?? "unknown");
        }
        catch
        {
            // Continue with cleanup even if sign out fails
        }

        await UpdateAuthStateAsync(new AuthState());
    }

    public async Task<AuthState> GetAuthStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            return _currentAuthState;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<string?> GetCurrentUserGroupAsync()
    {
        var state = await GetAuthStateAsync();
        return state.UserGroup;
    }

    private string? ExtractGroupId(User user)
    {
        if (user?.UserMetadata != null &&
            user.UserMetadata.TryGetValue("group_id", out var groupId))
        {
            return groupId?.ToString();
        }
        return null;
    }

    private async Task UpdateAuthStateAsync(AuthState newState)
    {
        await _stateLock.WaitAsync();
        try
        {
            _currentAuthState = newState;
            AuthStateChanged?.Invoke(this, newState);
        }
        finally
        {
            _stateLock.Release();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~SupabaseAuthServiceTests"
```

Expected: Some tests may pass, others need proper mocking

- [ ] **Step 5: Commit**

```bash
git add src/InvoiceAI.Core/Services/SupabaseAuthService.cs tests/InvoiceAI.Core.Tests/Services/SupabaseAuthServiceTests.cs
git commit -m "feat(auth): implement SupabaseAuthService with thread-safe state management"
```

---

### Task 12: Extend IAppSettingsService

**Files:**
- Modify: `src/InvoiceAI.Core/Services/IAppSettingsService.cs`
- Modify: `src/InvoiceAI.Core/Services/AppSettingsService.cs`

- [ ] **Step 1: Add GetEffectiveApiKeysAsync to IAppSettingsService**

```csharp
// In src/InvoiceAI.Core/Services/IAppSettingsService.cs
// Add:
using InvoiceAI.Models.Auth;

Task<EffectiveApiKeys> GetEffectiveApiKeysAsync();
```

- [ ] **Step 2: Implement GetEffectiveApiKeysAsync in AppSettingsService**

```csharp
// In src/InvoiceAI.Core/Services/AppSettingsService.cs
// Add to class:
private readonly IAuthService? _authService;
private readonly ICloudKeyService? _cloudKeyService;

// Update constructor to inject services (optional parameters):
public AppSettingsService(
    string configPath,
    IAuthService? authService = null,
    ICloudKeyService? cloudKeyService = null)
{
    // ... existing code ...
    _authService = authService;
    _cloudKeyService = cloudKeyService;
}

// Implement the new method:
public async Task<EffectiveApiKeys> GetEffectiveApiKeysAsync()
{
    var authState = _authService != null ? await _authService.GetAuthStateAsync() : null;

    // Try cloud keys first if authenticated
    if (authState?.IsAuthenticated == true &&
        authState.CloudKeysAvailable &&
        _cloudKeyService != null)
    {
        try
        {
            var cloudKeys = await _cloudKeyService.GetCachedCloudKeysAsync();
            if (cloudKeys != null && _cloudKeyService.IsCloudKeyValid(cloudKeys))
            {
                // Map cloud keys to active provider
                var provider = Settings.Glm.Provider;
                var (apiKey, endpoint, model, _) = Settings.Glm.GetActiveConfig();

                // Use cloud keys for the active provider
                var cloudKeyConfig = GetCloudKeysForProvider(cloudKeys, provider);
                if (cloudKeyConfig != null)
                {
                    return new EffectiveApiKeys
                    {
                        OcrToken = cloudKeys.OcrToken,
                        OcrEndpoint = cloudKeys.OcrEndpoint,
                        GlmApiKey = cloudKeyConfig.ApiKey,
                        GlmEndpoint = cloudKeyConfig.Endpoint,
                        GlmModel = cloudKeyConfig.Model,
                        GlmProvider = provider,
                        Source = "cloud",
                        KeyVersion = cloudKeys.Version
                    };
                }
            }
        }
        catch
        {
            // Fall through to local keys on any error
        }
    }

    // Fallback to local configuration
    var (localApiKey, localEndpoint, localModel, _) = Settings.Glm.GetActiveConfig();
    return new EffectiveApiKeys
    {
        OcrToken = Settings.BaiduOcr.Token,
        OcrEndpoint = Settings.BaiduOcr.Endpoint,
        GlmApiKey = localApiKey,
        GlmEndpoint = localEndpoint,
        GlmModel = localModel,
        GlmProvider = Settings.Glm.Provider,
        Source = "local",
        KeyVersion = 1
    };
}

private (string ApiKey, string Endpoint, string Model)? GetCloudKeysForProvider(CloudKeyConfig config, string provider)
{
    return provider switch
    {
        "nvidia" when !string.IsNullOrEmpty(config.NvidiaApiKey) => (config.NvidiaApiKey!, config.NvidiaEndpoint!, config.NvidiaModel!),
        "cerebras" when !string.IsNullOrEmpty(config.CerebrasApiKey) => (config.CerebrasApiKey!, config.CerebrasEndpoint!, config.CerebrasModel!),
        "zhipu" when !string.IsNullOrEmpty(config.ZhipuApiKey) => (config.ZhipuApiKey!, config.ZhipuEndpoint!, config.ZhipuModel!),
        _ => null
    };
}
```

- [ ] **Step 3: Commit**

```bash
git add src/InvoiceAI.Core/Services/IAppSettingsService.cs src/InvoiceAI.Core/Services/AppSettingsService.cs
git commit -m "feat(auth): add GetEffectiveApiKeysAsync with cloud/local fallback logic"
```

---

### Task 13: Update GlmService to Use EffectiveApiKeys

**Files:**
- Modify: `src/InvoiceAI.Core/Services/GlmService.cs`

- [ ] **Step 1: Update GlmService to use GetEffectiveApiKeysAsync**

```csharp
// In src/InvoiceAI.Core/Services/GlmService.cs
// Update ProcessInvoiceAsync and ProcessBatchCoreAsync methods:

public async Task<GlmInvoiceResponse> ProcessInvoiceAsync(string ocrText)
{
    const int maxRetries = 3;
    for (int attempt = 0; ; attempt++)
    {
        // Use effective keys (cloud or local)
        var effectiveKeys = await _settingsService.GetEffectiveApiKeysAsync();
        var maxTokens = effectiveKeys.Source == "cloud" && effectiveKeys.GlmProvider == "zhipu" ? 100000 : 32768;

        var requestBody = BuildRequestBody(
            InvoicePrompt.BuildUserMessage(ocrText), maxTokens, effectiveKeys.GlmProvider);

        using var request = new HttpRequestMessage(HttpMethod.Post, effectiveKeys.GlmEndpoint);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {effectiveKeys.GlmApiKey}");
        // ... rest of method
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Core/Services/GlmService.cs
git commit -m "feat(auth): update GlmService to use effective API keys"
```

---

### Task 14: Update BaiduOcrService to Use EffectiveApiKeys

**Files:**
- Modify: `src/InvoiceAI.Core/Services/BaiduOcrService.cs` (or create if it uses IBaiduOcrService)

- [ ] **Step 1: Update BaiduOcrService to use effective keys**

```csharp
// In src/InvoiceAI.Core/Services/BaiduOcrService.cs
// Update methods that use Settings.BaiduOcr.Token/Endpoint:

public async Task<string> ProcessImageAsync(byte[] imageData)
{
    var effectiveKeys = await _settingsService.GetEffectiveApiKeysAsync();

    using var request = new HttpRequestMessage(HttpMethod.Post, effectiveKeys.OcrEndpoint);
    request.Headers.TryAddWithoutValidation("Authorization", $"token {effectiveKeys.OcrToken}");
    // ... rest of method
}
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.Core/Services/BaiduOcrService.cs
git commit -m "feat(auth): update BaiduOcrService to use effective API keys"
```

---

## Phase 3: ViewModel Layer

### Task 15: Create AuthViewModel

**Files:**
- Create: `src/InvoiceAI.Core/ViewModels/AuthViewModel.cs`
- Test: `tests/InvoiceAI.Core.Tests/ViewModels/AuthViewModelTests.cs`

- [ ] **Step 1: Write the failing tests first**

```csharp
// tests/InvoiceAI.Core.Tests/ViewModels/AuthViewModelTests.cs
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Services;
using InvoiceAI.Core.ViewModels;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Tests.ViewModels;

public class AuthViewModelTests
{
    [Fact]
    public async Task LoginCommand_ValidCredentials_CallsAuthService()
    {
        // Arrange
        var mockAuthService = new /* mock implementation */;
        var viewModel = new AuthViewModel(mockAuthService, null);

        // Act
        await viewModel.LoginCommand.ExecuteAsync("user@example.com:password");

        // Assert
        Assert.True(viewModel.AuthState.IsAuthenticated);
    }

    [Fact]
    public async Task LogoutCommand_ClearsAuthState()
    {
        // Arrange
        var mockAuthService = new /* mock implementation */;
        var viewModel = new AuthViewModel(mockAuthService, null);
        // Simulate logged in state

        // Act
        await viewModel.LogoutCommand.ExecuteAsync(null);

        // Assert
        Assert.False(viewModel.AuthState.IsAuthenticated);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~AuthViewModelTests"
```

Expected: FAIL with type not found errors

- [ ] **Step 3: Implement the AuthViewModel**

```csharp
// src/InvoiceAI.Core/ViewModels/AuthViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceAI.Core.Services;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICloudKeyService _cloudKeyService;

    [ObservableProperty] private AuthState _authState = new();

    public AuthViewModel(IAuthService authService, ICloudKeyService cloudKeyService)
    {
        _authService = authService;
        _cloudKeyService = cloudKeyService;

        // Subscribe to auth state changes
        _authService.AuthStateChanged += (s, state) => AuthState = state;

        // Initialize current state
        Task.Run(async () =>
        {
            var initialState = await _authService.GetAuthStateAsync();
            AuthState = initialState;
        });
    }

    [RelayCommand]
    private async Task LoginAsync(string credentials)
    {
        var parts = credentials.Split(':', 2);
        if (parts.Length != 2)
        {
            AuthState = new AuthState { ErrorMessage = "请输入邮箱和密码" };
            return;
        }

        var email = parts[0];
        var password = parts[1];

        var result = await _authService.SignInAsync(email, password);

        if (result.Success)
        {
            // Fetch cloud keys after successful login
            if (!string.IsNullOrEmpty(result.UserGroup))
            {
                var cloudKeys = await _cloudKeyService.GetCloudKeysAsync(result.UserGroup);
                var state = await _authService.GetAuthStateAsync();
                state.CloudKeysAvailable = cloudKeys != null && _cloudKeyService.IsCloudKeyValid(cloudKeys);
                if (cloudKeys != null)
                {
                    state.ActiveCloudProvider = GetFirstAvailableProvider(cloudKeys);
                    state.IsUsingCloudKeys = true;
                }
                AuthState = state;
            }
        }
        else
        {
            AuthState = new AuthState { ErrorMessage = result.ErrorMessage };
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.SignOutAsync();
        _cloudKeyService.ClearCachedKeys();
        AuthState = new AuthState();
    }

    private string? GetFirstAvailableProvider(CloudKeyConfig config)
    {
        if (!string.IsNullOrEmpty(config.NvidiaApiKey)) return "nvidia";
        if (!string.IsNullOrEmpty(config.ZhipuApiKey)) return "zhipu";
        if (!string.IsNullOrEmpty(config.CerebrasApiKey)) return "cerebras";
        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~AuthViewModelTests"
```

Expected: Some tests pass with proper mocking

- [ ] **Step 5: Commit**

```bash
git add src/InvoiceAI.Core/ViewModels/AuthViewModel.cs tests/InvoiceAI.Core.Tests/ViewModels/AuthViewModelTests.cs
git commit -m "feat(auth): implement AuthViewModel with login/logout commands"
```

---

## Phase 4: UI Layer

### Task 16: Register Services in DI

**Files:**
- Modify: `src/InvoiceAI.App/MauiProgram.cs`

- [ ] **Step 1: Add Supabase client and auth services to DI**

```csharp
// In src/InvoiceAI.App/MauiProgram.cs
// Add to CreateMauiApp method:

// Read Supabase configuration from appsettings.json
var supabaseConfig = builder.Configuration.GetSection("Supabase").Get<InvoiceAI.Core.Helpers.SupabaseConfig>()
    ?? new InvoiceAI.Core.Helpers.SupabaseConfig();

// Initialize Supabase client
var supabaseClient = new Supabase.Client(supabaseConfig.Url, supabaseConfig.AnonKey);

// Register auth services
builder.Services.AddSingleton(supabaseClient);
builder.Services.AddSingleton<IInvoiceAI.Core.Services.IAuthService, InvoiceAI.Core.Services.SupabaseAuthService>();
builder.Services.AddSingleton<IInvoiceAI.Core.Services.ICloudKeyService, InvoiceAI.Core.Services.SupabaseKeyService>();

// Register AuthViewModel
builder.Services.AddSingleton<InvoiceAI.Core.ViewModels.AuthViewModel>();
```

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.App/MauiProgram.cs
git commit -m "feat(auth): register Supabase and auth services in DI"
```

---

### Task 17: Add Account Section to SettingsPage

**Files:**
- Modify: `src/InvoiceAI.App/Pages/SettingsPage.cs`

- [ ] **Step 1: Add account section UI to SettingsPage**

```csharp
// In src/InvoiceAI.App/Pages/SettingsPage.cs
// Add to BuildContent method, at the top of the VerticalStackLayout:

// Add field to hold AuthViewModel:
private readonly AuthViewModel _authViewModel;

// Update constructor:
public SettingsPage(SettingsViewModel viewModel, AuthViewModel authViewModel)
{
    _vm = viewModel;
    _authViewModel = authViewModel;
    BindingContext = viewModel;
    // ... existing code ...
}

// Add to BuildContent, first item in VerticalStackLayout.Children:
BuildAccountSection(),

// Add the BuildAccountSection method:
private View BuildAccountSection()
{
    var statusIndicator = new Label
    {
        FontSize = 11,
        TextColor = _authViewModel.AuthState.IsAuthenticated ? Color.FromArgb("#4CAF50") : Color.FromArgb("#9E9E9E")
    }.Bind(Label.TextProperty, nameof(_authViewModel.IsUsingCloudKeys), converter: new FuncConverter<bool, string>(usingCloud => usingCloud ? "🟢" : "⚪"));

    var accountSection = new Border
    {
        StrokeShape = new RoundRectangle { CornerRadius = 8 },
        StrokeThickness = 1,
        Stroke = ThemeManager.BorderLight,
        BackgroundColor = ThemeManager.CardBackground,
        Padding = new Thickness(16, 12),
        Content = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = "📦", FontSize = 18 },
                        new Label
                        {
                            Text = "账户",
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = ThemeManager.TextPrimary,
                            VerticalOptions = LayoutOptions.Center
                        },
                        statusIndicator
                    }
                },
                new BoxView { Color = ThemeManager.BorderLight, HeightRequest = 1, Margin = new Thickness(0, 4) },
                BuildAccountContent()
            }
        }
    };

    return accountSection;
}

private View BuildAccountContent()
{
    // Not logged in view
    var notLoggedInView = new VerticalStackLayout
    {
        Spacing = 8,
        Children =
        {
            new Label
            {
                Text = "登录后可使用云端 API Key，无需手动配置",
                FontSize = 13,
                TextColor = ThemeManager.TextSecondary
            },
            new Label
            {
                Text = "云端 Key 优先于本地配置使用",
                FontSize = 12,
                TextColor = ThemeManager.TextTertiary
            },
            new Button
            {
                Text = "登录...",
                BackgroundColor = ThemeManager.BrandPrimary,
                TextColor = Colors.White,
                FontSize = 14,
                HorizontalOptions = LayoutOptions.End,
                MinimumHeightRequest = 36
            }
            .Invoke(btn => btn.Clicked += OnLoginClicked)
        }
    };

    // Logged in view
    var loggedInView = new VerticalStackLayout
    {
        Spacing = 8,
        Children =
        {
            new Label { Text = "👤", FontSize = 14 }
                .Bind(Label.TextProperty, nameof(_authViewModel.AuthState.UserEmail), converter: new FuncConverter<string?, string>(email => $"👤 {email}")),
            new Label
            {
                Text = "🏷️ 用户组: Premium",
                FontSize = 13,
                TextColor = ThemeManager.TextPrimary
            }
            .Bind(Label.TextProperty, nameof(_authViewModel.AuthState.UserGroup), converter: new FuncConverter<string?, string>(group => $"🏷️ 用户组: {group}")),
            new Label
            {
                Text = "✅ 云端 API Key 已激活",
                FontSize = 12,
                TextColor = ThemeManager.Success
            }
            .Bind(Label.TextProperty, nameof(_authViewModel.AuthState.CloudKeysAvailable), converter: new FuncConverter<bool, string>(available => available ? "✅ 云端 API Key 已激活" : "⚠️ 云端 API Key 不可用")),
            new Label
            {
                Text = "🔄 当前使用: 云端配置",
                FontSize = 12,
                TextColor = ThemeManager.Info
            }
            .Bind(Label.TextProperty, nameof(_authViewModel.AuthState.IsUsingCloudKeys), converter: new FuncConverter<bool, string>(usingCloud => usingCloud ? "🔄 当前使用: 云端配置" : "🔄 当前使用: 本地配置")),
            new Button
            {
                Text = "登出",
                BackgroundColor = ThemeManager.TextSecondary,
                TextColor = Colors.White,
                FontSize = 14,
                HorizontalOptions = LayoutOptions.End,
                MinimumHeightRequest = 36
            }
            .Invoke(btn => btn.Clicked += async (s, e) => await _authViewModel.LogoutCommand.ExecuteAsync(null))
        }
    };

    // Use a ContentView to switch between views
    var contentView = new ContentView { Content = notLoggedInView };
    contentView.SetBinding(ContentView.ContentProperty, nameof(_authViewModel.AuthState.IsAuthenticated),
        converter: new FuncConverter<bool, View>(isLoggedIn => isLoggedIn ? loggedInView : notLoggedInView));

    return contentView;
}

private async void OnLoginClicked(object? sender, EventArgs e)
{
    var emailEntry = new Entry { Placeholder = "电子邮箱", Margin = new Thickness(0, 0, 0, 8) };
    var passwordEntry = new Entry { Placeholder = "密码", IsPassword = true, Margin = new Thickness(0, 0, 0, 8) };
    var errorLabel = new Label { TextColor = Colors.Red, Margin = new Thickness(0, 0, 0, 8) };

    var loginButton = new Button { Text = "登录", Margin = new Thickness(0, 0, 8, 0) };
    var cancelButton = new Button { Text = "取消" };

    var content = new VerticalStackLayout
    {
        Padding = 20,
        Spacing = 8,
        Children = { emailEntry, passwordEntry, errorLabel }
    };

    var buttons = new HorizontalStackLayout { Spacing = 8, Children = { loginButton, cancelButton } };
    content.Children.Add(buttons);

    var page = new ContentPage
    {
        Title = "用户登录",
        Content = content,
        BackgroundColor = ThemeManager.Background
    };

    string? errorMessage = null;

    loginButton.Clicked += async (s, e) =>
    {
        if (string.IsNullOrWhiteSpace(emailEntry.Text) || string.IsNullOrWhiteSpace(passwordEntry.Text))
        {
            errorLabel.Text = "请输入邮箱和密码";
            return;
        }

        var credentials = $"{emailEntry.Text}:{passwordEntry.Text}";
        await _authViewModel.LoginCommand.ExecuteAsync(credentials);

        if (_authViewModel.AuthState.IsAuthenticated)
        {
            await Navigation.PopModalAsync();
        }
        else
        {
            errorLabel.Text = _authViewModel.AuthState.ErrorMessage ?? "登录失败";
        }
    };

    cancelButton.Clicked += async (s, e) => await Navigation.PopModalAsync();

    _authViewModel.PropertyChanged += (s, e) =>
    {
        if (e.PropertyName == nameof(AuthViewModel.AuthState))
        {
            if (_authViewModel.AuthState.ErrorMessage != null)
            {
                errorLabel.Text = _authViewModel.AuthState.ErrorMessage;
            }
        }
    };

    await Navigation.PushModalAsync(new NavigationPage(page));
}
```

Note: You may need to add a FuncConverter class or use CommunityToolkit.Maui.Converters.

- [ ] **Step 2: Commit**

```bash
git add src/InvoiceAI.App/Pages/SettingsPage.cs
git commit -m "feat(auth): add account section UI to SettingsPage"
```

---

## Phase 5: Supabase Setup

### Task 18: Create Supabase Database Schema

**Files:**
- External: Supabase SQL Editor

- [ ] **Step 1: Create database schema in Supabase**

Execute the following SQL in Supabase SQL Editor:

```sql
-- Create keys table
CREATE TABLE IF NOT EXISTS public.keys (
    group_id TEXT PRIMARY KEY,

    -- OCR (required)
    ocr_token TEXT NOT NULL CHECK (ocr_token != ''),
    ocr_endpoint TEXT NOT NULL CHECK (ocr_endpoint != ''),

    -- GLM providers (at least one required)
    nvidia_apikey TEXT,
    nvidia_endpoint TEXT,
    nvidia_model TEXT,

    zhipu_apikey TEXT,
    zhipu_endpoint TEXT,
    zhipu_model TEXT,

    cerebras_apikey TEXT,
    cerebras_endpoint TEXT,
    cerebras_model TEXT,

    -- Metadata
    version INT DEFAULT 1,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT check_at_least_one_glm_provider CHECK (
        (nvidia_apikey IS NOT NULL AND nvidia_model IS NOT NULL) OR
        (zhipu_apikey IS NOT NULL AND zhipu_model IS NOT NULL) OR
        (cerebras_apikey IS NOT NULL AND cerebras_model IS NOT NULL)
    )
);

-- Create index
CREATE INDEX IF NOT EXISTS idx_keys_group_id ON public.keys(group_id);

-- Create audit trigger
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.version = OLD.version + 1;
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_keys_updated_at BEFORE UPDATE ON public.keys
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Create user_groups table for RLS
CREATE TABLE IF NOT EXISTS public.user_groups (
    user_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    group_id TEXT REFERENCES public.keys(group_id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, group_id)
);

-- Enable RLS
ALTER TABLE public.keys ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.user_groups ENABLE ROW LEVEL SECURITY;

-- RLS policy for keys
CREATE POLICY "Users can view keys for their group"
    ON public.keys FOR SELECT
    TO authenticated
    USING (
        group_id IN (
            SELECT group_id FROM public.user_groups WHERE user_id = auth.uid()
        )
    );

-- RLS policy for user_groups
CREATE POLICY "Users can view their own groups"
    ON public.user_groups FOR SELECT
    TO authenticated
    USING (user_id = auth.uid());

-- Insert test data
INSERT INTO public.keys (
    group_id, ocr_token, ocr_endpoint,
    nvidia_apikey, nvidia_endpoint, nvidia_model
)
VALUES (
    'test_group_1',
    'your_ocr_token_here',
    'https://aistudio.baidu.com/inference/v1/paddleocr',
    'your_nvidia_api_key_here',
    'https://integrate.api.nvidia.com/v1/chat/completions',
    'deepseek-ai/deepseek-v3.1-terminus'
)
ON CONFLICT (group_id) DO UPDATE SET
    ocr_token = EXCLUDED.ocr_token,
    nvidia_apikey = EXCLUDED.nvidia_apikey;
```

- [ ] **Step 2: Configure Supabase Auth**

In Supabase Dashboard:
1. Go to Authentication → Settings
2. Set Session expiry to 30 days
3. Enable Email/Password authentication
4. Note your Supabase URL and Anon Key

- [ ] **Step 3: Update appsettings.json with Supabase config**

```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "AnonKey": "your-anon-key-here"
  },
  "BaiduOcr": { ... },
  "Glm": { ... },
  ...
}
```

- [ ] **Step 4: Document completion**

```bash
echo "Supabase setup complete" >> SUPABASE_SETUP_DONE.md
```

---

## Phase 6: Testing & Integration

### Task 19: Write Integration Tests

**Files:**
- Test: `tests/InvoiceAI.Core.Tests/Services/AppSettingsServiceExtensionsTests.cs`

- [ ] **Step 1: Write integration tests**

```csharp
// tests/InvoiceAI.Core.Tests/Services/AppSettingsServiceExtensionsTests.cs
using InvoiceAI.Core.Services;
using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Tests.Services;

public class AppSettingsServiceExtensionsTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AppSettingsServiceExtensionsTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetEffectiveApiKeysAsync_UserLoggedInAndCloudKeysAvailable_ReturnsCloudKeys()
    {
        // Arrange
        var service = _fixture.GetSettingsServiceWithMockAuth();

        // Act
        var result = await service.GetEffectiveApiKeysAsync();

        // Assert
        Assert.Equal("cloud", result.Source);
    }

    [Fact]
    public async Task GetEffectiveApiKeysAsync_UserNotLoggedIn_ReturnsLocalKeys()
    {
        // Arrange
        var service = _fixture.GetSettingsServiceWithoutAuth();

        // Act
        var result = await service.GetEffectiveApiKeysAsync();

        // Assert
        Assert.Equal("local", result.Source);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/InvoiceAI.Core.Tests/ --filter "FullyQualifiedName~AppSettingsServiceExtensionsTests"
```

- [ ] **Step 3: Commit**

```bash
git add tests/InvoiceAI.Core.Tests/Services/AppSettingsServiceExtensionsTests.cs
git commit -m "test(auth): add integration tests for effective API keys"
```

---

### Task 20: End-to-End Testing

- [ ] **Step 1: Run the application**

```bash
dotnet run --project src/InvoiceAI.App/InvoiceAI.App.csproj -f net9.0-windows10.0.19041.0
```

- [ ] **Step 2: Test login flow**

1. Open Settings page
2. Click "登录..." button
3. Enter test credentials
4. Verify user info displays
5. Verify cloud keys indicator shows active

- [ ] **Step 3: Test key priority**

1. Import an invoice file
2. Verify cloud keys are used (check audit log)
3. Logout
4. Import another file
5. Verify local keys are used

- [ ] **Step 4: Test error scenarios**

1. Login with wrong password - verify error message
2. Login with user that has no group - verify warning
3. Login with user that has no keys - verify fallback to local

- [ ] **Step 5: Document test results**

```bash
echo "E2E tests passed" >> TEST_RESULTS.md
```

---

## Task 21: Final Cleanup

- [ ] **Step 1: Update CLAUDE.md with auth features**

Add to `CLAUDE.md`:

```markdown
## Authentication

- Users can login via Supabase (email + password)
- Logged-in users receive cloud API keys based on their group
- Cloud keys take priority over local configuration
- Session persists for 30 days
- Audit logs stored in `%LOCALAPPDATA%/InvoiceAI/audit/`
```

- [ ] **Step 2: Update MEMORY.md**

Add project state notes about new auth system.

- [ ] **Step 3: Final commit**

```bash
git add CLAUDE.md MEMORY.md
git commit -m "docs: update documentation for authentication feature"
```

---

## Task 22: Create Release Notes

- [ ] **Step 1: Create release notes**

```bash
cat > RELEASE_NOTES.md << 'EOF'
# InvoiceAI v2.0 - Authentication & Cloud Keys

## New Features

- **Supabase Authentication**: Users can now login with email and password
- **Cloud API Keys**: Logged-in users automatically receive cloud-configured API keys
- **Key Priority System**: Cloud keys take priority over locally configured keys
- **Multi-Provider Support**: Cloud keys support Zhipu, NVIDIA NIM, and Cerebras

## Technical Changes

- Added Models layer: `InvoiceAI.Models/Auth/*`
- Added Services: `IAuthService`, `ICloudKeyService`
- Added ViewModel: `AuthViewModel`
- Extended `IAppSettingsService` with `GetEffectiveApiKeysAsync()`
- Added audit logging for security compliance
- Thread-safe implementation with locks and semaphores

## Configuration

New `appsettings.json` section:
```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "AnonKey": "your-anon-key"
  }
}
```

## Migration

No migration required - existing local configuration continues to work.
EOF
```

- [ ] **Step 2: Commit release notes**

```bash
git add RELEASE_NOTES.md
git commit -m "docs: add release notes for v2.0"
```

---

## Summary

This plan implements Supabase authentication with cloud API key provisioning in ~13.5 hours across 22 tasks:

1. **Phase 1 (Models)**: 4 tasks, ~1.5h
2. **Phase 2 (Services)**: 10 tasks, ~8h
3. **Phase 3 (ViewModel)**: 1 task, ~1h
4. **Phase 4 (UI)**: 2 tasks, ~1.5h
5. **Phase 5 (Supabase)**: 1 task, ~45min
6. **Phase 6 (Testing)**: 4 tasks, ~1h

**Key Design Decisions:**
- Minimal change to existing codebase
- Thread-safe implementation for concurrent access
- Cloud keys hidden from users, used transparently
- Graceful fallback to local keys on any failure
- Comprehensive audit logging for security
EOF