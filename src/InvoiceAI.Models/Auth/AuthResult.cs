namespace InvoiceAI.Models.Auth;

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UserEmail { get; set; }
    public string? UserGroup { get; set; }
}
