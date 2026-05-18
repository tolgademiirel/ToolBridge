using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MusicShell.Infrastructure;

public static class AppLogger
{
    private static readonly object SyncRoot = new();

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolBridge",
        "logs");

    public static string CurrentLogPath => Path.Combine(LogDirectory, $"toolbridge-{DateTime.Now:yyyy-MM-dd}.log");

    public static string CurrentAuditLogPath => Path.Combine(LogDirectory, $"toolbridge-audit-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Log(string message)
    {
        Write("INFO", message, null);
    }

    public static void Audit(string action, string? detail = null)
    {
        var safeAction = Sanitize(action);
        var safeDetail = string.IsNullOrWhiteSpace(detail) ? "-" : Sanitize(detail);
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [AUDIT] User={Sanitize(Environment.UserName)}; Machine={Sanitize(Environment.MachineName)}; Action={safeAction}; Detail={safeDetail}";
        WriteRaw(CurrentAuditLogPath, line);
        Write("AUDIT", $"User={Sanitize(Environment.UserName)}; Machine={Sanitize(Environment.MachineName)}; Action={safeAction}; Detail={safeDetail}", null);
    }

    public static void LogException(string message, Exception exception)
    {
        Write("ERROR", message, exception);
    }

    public static void CleanupOldLogs(int keepDays = 30)
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                return;
            }

            var threshold = DateTime.Now.Date.AddDays(-Math.Max(1, keepDays));
            foreach (var pattern in new[] { "toolbridge-*.log", "toolbridge-audit-*.log" })
            {
                foreach (var file in Directory.GetFiles(LogDirectory, pattern, SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.LastWriteTime.Date < threshold)
                        {
                            info.Delete();
                        }
                    }
                    catch
                    {
                        // Keep cleanup resilient. One locked file must not stop the app.
                    }
                }
            }
        }
        catch
        {
            Debug.WriteLine("ToolBridge old log cleanup failed.");
        }
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            WriteRaw(CurrentLogPath, line);
        }
        catch
        {
            Debug.WriteLine($"ToolBridge log write failed: {message}");
        }
    }

    private static void WriteRaw(string path, string line)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            lock (SyncRoot)
            {
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            Debug.WriteLine("ToolBridge log write failed.");
        }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}