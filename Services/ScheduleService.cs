using GameGuard.Models;

namespace GameGuard.Services;

public sealed class ScheduleService
{
    /// <summary>
    /// Returns true if the current moment falls inside any configured allowed window.
    /// An empty schedule means nothing is ever allowed (all-blocked by default).
    /// </summary>
    public bool IsCurrentlyAllowed(List<ScheduleEntry> schedule)
    {
        if (schedule.Count == 0) return false;

        var now = DateTime.Now;
        int today = (int)now.DayOfWeek; // 0=Sun … 6=Sat
        var nowTime = now.TimeOfDay;

        foreach (var entry in schedule)
        {
            if (!entry.Days.Contains(today)) continue;
            if (!TimeSpan.TryParse(entry.Start, out var start)) continue;
            if (!TimeSpan.TryParse(entry.End, out var end)) continue;
            if (nowTime >= start && nowTime <= end) return true;
        }

        return false;
    }
}
