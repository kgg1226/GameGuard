namespace GameGuard.Models;

public class AppConfig
{
    public List<BlockedApp> BlockedApps { get; set; } = new();
    public List<ScheduleEntry> Schedule { get; set; } = new();
    public int GraceSeconds { get; set; } = 10;
    public int PollIntervalSeconds { get; set; } = 3;
    public int ToastCooldownSeconds { get; set; } = 300;
}

public class BlockedApp
{
    public string Id { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string Path { get; set; } = "";
    public bool PathPinned { get; set; } = false;
}

public class ScheduleEntry
{
    /// <summary>Days of week: 0 = Sunday … 6 = Saturday</summary>
    public List<int> Days { get; set; } = new();
    public string Start { get; set; } = "09:00";
    public string End { get; set; } = "22:00";
}
