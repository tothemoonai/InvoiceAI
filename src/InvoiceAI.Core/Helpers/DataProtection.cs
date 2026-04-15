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
