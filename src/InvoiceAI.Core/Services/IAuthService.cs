using InvoiceAI.Models.Auth;

namespace InvoiceAI.Core.Services;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string email, string password);
    Task<AuthResult> SignUpAsync(string email, string password);
    Task SignOutAsync();
    Task<AuthState> GetAuthStateAsync();
    Task<string?> GetCurrentUserGroupAsync();
    event EventHandler<AuthState>? AuthStateChanged;
}
