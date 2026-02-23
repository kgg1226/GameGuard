using System.IO;
using System.Text.Json;
using GameGuard.Models;

namespace GameGuard.Services;

public sealed class ConfigService
{
    private readonly string _configPath;
    private readonly Logger _logger;
    private readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    private AppConfig _config;

    public AppConfig Config => _config;

    public ConfigService(Logger logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameGuard");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "config.json");
        _config = Load();
    }

    private AppConfig Load()
    {
        if (!File.Exists(_configPath))
            return new AppConfig();
        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, _jsonOpts) ?? new AppConfig();
        }
        catch { return new AppConfig(); }
    }

    /// <summary>Validate, normalize, persist, and replace in-memory config.</summary>
    public void Save(AppConfig config)
    {
        Validate(config);

        // Normalize all stored paths
        foreach (var app in config.BlockedApps)
        {
            if (!string.IsNullOrEmpty(app.Path))
                app.Path = Path.GetFullPath(app.Path);
        }

        var json = JsonSerializer.Serialize(config, _jsonOpts);
        File.WriteAllText(_configPath, json);
        _config = config;
        _logger.Log("config_changed");
    }

    private static void Validate(AppConfig config)
    {
        if (config.GraceSeconds < 1)
            throw new ArgumentException("Grace period must be at least 1 second.");

        if (config.PollIntervalSeconds < 1)
            throw new ArgumentException("Poll interval must be at least 1 second.");

        foreach (var entry in config.Schedule)
        {
            if (entry.Days.Count == 0)
                throw new ArgumentException("Each schedule entry must have at least one day.");

            if (!TryParseHHmm(entry.Start, out var start))
                throw new ArgumentException($"Invalid start time: '{entry.Start}'. Use HH:mm.");

            if (!TryParseHHmm(entry.End, out var end))
                throw new ArgumentException($"Invalid end time: '{entry.End}'. Use HH:mm.");

            if (end <= start)
                throw new ArgumentException("Schedule end time must be later than start time.");
        }
    }

    internal static bool TryParseHHmm(string text, out TimeSpan result)
    {
        // Accept "9:00", "09:00", "21:30" etc.
        if (!TimeSpan.TryParse(text.Trim(), out result)) return false;
        return result.Days == 0 && result.TotalHours < 24 && result.Seconds == 0;
    }
}
