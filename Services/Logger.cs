using System.IO;
using System.Text.Json;

namespace GameGuard.Services;

public sealed class Logger
{
    private readonly string _logPath;
    private readonly object _writeLock = new();

    public string LogDirectory { get; }

    public Logger()
    {
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameGuard");
        Directory.CreateDirectory(LogDirectory);
        _logPath = Path.Combine(LogDirectory, "log.jsonl");
    }

    public void Log(string eventType, string? processName = null, string? detail = null)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            @event = eventType,
            process = processName,
            detail
        };

        var line = JsonSerializer.Serialize(entry);

        lock (_writeLock)
        {
            try { File.AppendAllText(_logPath, line + Environment.NewLine); }
            catch { /* Never crash on logging failure */ }
        }
    }
}
