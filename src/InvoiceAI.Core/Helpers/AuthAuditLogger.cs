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
