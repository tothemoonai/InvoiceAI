using InvoiceAI.Core.Helpers;
using InvoiceAI.Models.Auth;
using Supabase;
using Supabase.Gotrue;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Threading;
using SupabaseClient = Supabase.Client;

namespace InvoiceAI.Core.Services;

public class SupabaseAuthService : IAuthService
{
    private readonly SupabaseClient _supabaseClient;
    private readonly ICloudKeyService? _cloudKeyService;
    private AuthState _currentAuthState = new();
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public event EventHandler<AuthState>? AuthStateChanged;

    public SupabaseAuthService(SupabaseClient supabaseClient, ICloudKeyService? cloudKeyService = null)
    {
        _supabaseClient = supabaseClient;
        _cloudKeyService = cloudKeyService;
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        try
        {
            var session = await _supabaseClient.Auth.SignIn(email, password);

            if (session?.User != null)
            {
                var groupId = await ExtractGroupIdAsync(session.User);

                // Fetch cloud keys to verify they exist for this group
                var cloudKeys = groupId != null && _cloudKeyService != null
                    ? await _cloudKeyService.GetCloudKeysAsync(groupId)
                    : null;

                var hasCloudKeys = cloudKeys != null && _cloudKeyService.IsCloudKeyValid(cloudKeys);

                var newState = new AuthState
                {
                    IsAuthenticated = true,
                    UserEmail = session.User.Email,
                    UserGroup = groupId,
                    CloudKeysAvailable = hasCloudKeys,
                    IsUsingCloudKeys = hasCloudKeys,
                    ActiveCloudProvider = hasCloudKeys ? GetFirstAvailableProvider(cloudKeys!) : null
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

    public async Task<AuthResult> SignUpAsync(string email, string password)
    {
        try
        {
            // Sign up with Supabase Auth
            // Database trigger will automatically insert user_groups record with group_id = "1"
            var session = await _supabaseClient.Auth.SignUp(email, password);

            if (session?.User == null)
            {
                return new AuthResult { Success = false, ErrorMessage = "注册失败" };
            }

            // Log the signup (user_groups is handled by database trigger)
            AuthAuditLogger.LogSignUp(email, "1", true);

            // Fetch cloud keys for group "1"
            var cloudKeys = _cloudKeyService != null
                ? await _cloudKeyService.GetCloudKeysAsync("1")
                : null;

            var hasCloudKeys = cloudKeys != null && _cloudKeyService!.IsCloudKeyValid(cloudKeys);

            // Update auth state
            var newState = new AuthState
            {
                IsAuthenticated = true,
                UserEmail = session.User.Email,
                UserGroup = "1",
                CloudKeysAvailable = hasCloudKeys,
                IsUsingCloudKeys = hasCloudKeys,
                ActiveCloudProvider = hasCloudKeys ? GetFirstAvailableProvider(cloudKeys!) : null
            };

            await UpdateAuthStateAsync(newState);

            return new AuthResult
            {
                Success = true,
                UserEmail = session.User.Email,
                UserGroup = "1"
            };
        }
        catch (Exception ex)
        {
            AuthAuditLogger.LogSignUp(email, null, false);
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

    private async Task<string?> ExtractGroupIdAsync(User user)
    {
        // First try to get from metadata (fast path)
        if (user?.UserMetadata != null &&
            user.UserMetadata.TryGetValue("group_id", out var groupId))
        {
            var metadataGroup = groupId?.ToString();
            // Optionally verify against user_groups table for security
            if (!string.IsNullOrEmpty(metadataGroup))
            {
                return metadataGroup;
            }
        }

        // Fallback: Query user_groups table using Postgrest
        try
        {
            var userId = user?.Id;
            if (!string.IsNullOrEmpty(userId))
            {
                var response = await _supabaseClient
                    .From<user_groups_row>()
                    .Select("*")
                    .Where(x => x.user_id == userId)
                    .Get();

                if (response.Models?.Count > 0)
                {
                    return response.Models[0].group_id;
                }
            }
        }
        catch
        {
            // Silently fail and return null
        }

        return null;
    }

    // Postgrest model for user_groups table
    [Table("user_groups")]
    private class user_groups_row : BaseModel
    {
        public string user_id { get; set; } = string.Empty;
        public string group_id { get; set; } = string.Empty;
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

    private string? GetFirstAvailableProvider(CloudKeyConfig config)
    {
        if (!string.IsNullOrEmpty(config.ZhipuApiKey)) return "zhipu";
        if (!string.IsNullOrEmpty(config.NvidiaApiKey)) return "nvidia";
        if (!string.IsNullOrEmpty(config.CerebrasApiKey)) return "cerebras";
        return null;
    }
}
