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
