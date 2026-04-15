using InvoiceAI.Core.Helpers;
using InvoiceAI.Models.Auth;
using Supabase;
using Supabase.Gotrue;
using System.Threading;
using SupabaseClient = Supabase.Client;

namespace InvoiceAI.Core.Services;

public class SupabaseAuthService : IAuthService
{
    private readonly SupabaseClient _supabaseClient;
    private AuthState _currentAuthState = new();
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public event EventHandler<AuthState>? AuthStateChanged;

    public SupabaseAuthService(SupabaseClient supabaseClient)
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
