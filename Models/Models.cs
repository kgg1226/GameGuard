namespace GameGuard.Models;

public class AppConfig
{
    public List<BlockedApp> BlockedApps { get; set; } = new();
    public List<BlockedWindow> BlockedWindows { get; set; } = new();
    public int PollIntervalSeconds { get; set; } = 3;
    public int GraceSeconds { get; set; } = 300;
    public int ToastCooldownSeconds { get; set; } = 300;
}

public class BlockedApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DisplayName { get; set; } = "";
    /// <summary>"launcher" or "game"</summary>
    public string Kind { get; set; } = "launcher";
    public string ProcessName { get; set; } = "";
    public string Path { get; set; } = "";
    /// <summary>
    /// When true, enforcement only applies if proc.MainModule.FileName matches Path.
    /// Default: false for launchers, true for games.
    /// </summary>
    public bool PathPinned { get; set; } = false;
}

public class BlockedWindow
{
    /// <summary>Days of week: 0 = Sunday … 6 = Saturday</summary>
    public List<int> Days { get; set; } = new();
    /// <summary>HH:mm — 24-hour format</summary>
    public string Start { get; set; } = "23:00";
    /// <summary>HH:mm — may be less than Start for overnight windows</summary>
    public string End { get; set; } = "07:00";
}
