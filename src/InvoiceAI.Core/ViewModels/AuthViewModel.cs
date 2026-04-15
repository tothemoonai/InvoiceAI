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
