using System.IO;
using System.Text.Json;
using GameGuard.Models;

namespace GameGuard.Services;

public sealed class ConfigService
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };
    private AppConfig _config;

    public AppConfig Config => _config;

    /// <summary>Non-null if the config file failed to load. UI should surface this to the user.</summary>
    public string? LoadError { get; private set; }

    /// <summary>Exposes the logger so the UI layer can record add/remove events.</summary>
    public Logger Logger { get; }

    public ConfigService(Logger logger)
    {
        Logger = logger;

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
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, _jsonOpts);
            if (cfg == null)
            {
                LoadError = "File deserialized to null.";
                Logger.Log("config_load_error", detail: LoadError);
                return new AppConfig();
            }
            return cfg;
        }
        catch (Exception ex)
        {
            LoadError = $"{ex.GetType().Name}: {ex.Message}";
            Logger.Log("config_load_error", detail: LoadError);
            return new AppConfig();
        }
    }

    /// <summary>Validate, normalize paths, persist, replace in-memory config.</summary>
    public void Save(AppConfig config)
    {
        Validate(config);

        foreach (var app in config.BlockedApps)
            if (!string.IsNullOrEmpty(app.Path))
                app.Path = Path.GetFullPath(app.Path);

        File.WriteAllText(_configPath, JsonSerializer.Serialize(config, _jsonOpts));
        _config = config;
        Logger.Log("config_changed");
    }

    private static void Validate(AppConfig config)
    {
        if (config.GraceSeconds < 1)
            throw new ArgumentException("Grace period must be at least 1 second.");
        if (config.PollIntervalSeconds < 1)
            throw new ArgumentException("Poll interval must be at least 1 second.");

        foreach (var w in config.BlockedWindows)
        {
            if (w.Days.Count == 0)
                throw new ArgumentException("Each blocked window must have at least one day.");
            if (!TimeSpan.TryParse(w.Start, out var start))
                throw new ArgumentException($"Invalid start time: '{w.Start}'. Use HH:mm.");
            if (!TimeSpan.TryParse(w.End, out var end))
                throw new ArgumentException($"Invalid end time: '{w.End}'. Use HH:mm.");
            if (start == end)
                throw new ArgumentException("Start and end times cannot be equal.");
        }
    }

    /// <summary>Parses a HH:mm time string into a TimeSpan. Accepts "9:00" and "09:00".</summary>
    internal static bool TryParseTime(string text, out TimeSpan result)
    {
        if (!TimeSpan.TryParse(text.Trim(), out result)) return false;
        return result.Days == 0 && result.TotalHours < 24 && result.Seconds == 0;
    }
}
