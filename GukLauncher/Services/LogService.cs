namespace GukLauncher.Services;

public static class LogService
{
    private static string? _logPath;

    public static void Init(string installDir)
    {
        _logPath = Path.Combine(installDir, "GukLauncher.log");
    }

    public static void Log(string message)
    {
        if (_logPath == null) return;
        try
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* never let logging crash the app */ }
    }
}
