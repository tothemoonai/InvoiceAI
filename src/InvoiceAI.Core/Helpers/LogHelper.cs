using System;

namespace InvoiceAI.Core.Helpers;

/// <summary>
/// Centralized log directory helper. Finds the project root by walking up from AppContext.BaseDirectory
/// looking for .git or *.sln, then returns {projectRoot}/TEMP/errorlog.
/// Falls back to %TEMP%/errorlog if project root not found.
/// </summary>
public static class LogHelper
{
    private static readonly Lazy<string> _logDir = new(FindLogDir);

    public static string LogDir => _logDir.Value;

    /// <summary>
    /// Logs a message with timestamp to the provider_fallback.log file.
    /// Silently fails if logging encounters errors (logging should not break the app).
    /// </summary>
    public static void Log(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";
            var logFile = Path.Combine(LogDir, "provider_fallback.log");
            File.AppendAllText(logFile, logMessage + Environment.NewLine);
        }
        catch
        {
            // Silently fail - logging should not break the app
        }
    }

    private static string FindLogDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                Directory.GetFiles(dir, "*.sln").Length > 0)
            {
                return Path.GetFullPath(Path.Combine(dir, "TEMP", "errorlog"));
            }
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        // Fallback: system temp
        return Path.Combine(Path.GetTempPath(), "errorlog");
    }
}
