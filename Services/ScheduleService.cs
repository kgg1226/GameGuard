using GameGuard.Models;

namespace GameGuard.Services;

public sealed class ScheduleService
{
    /// <summary>
    /// Returns true if the current moment falls within any configured blocked window.
    ///
    /// Semantics:
    ///   - Empty list  → NOT blocked (fail-open: no windows = no enforcement).
    ///   - start &lt; end → same-day window:   blocked when start &lt;= now &lt; end.
    ///   - start &gt; end → overnight window:  blocked when now &gt;= start (evening on start-day)
    ///                                      OR now &lt; end (morning on start-day+1).
    ///   - start == end → degenerate, skipped.
    /// </summary>
    public bool IsCurrentlyBlocked(List<BlockedWindow> windows)
    {
        if (windows.Count == 0) return false;

        var now = DateTime.Now;
        int today = (int)now.DayOfWeek;   // 0 = Sun … 6 = Sat
        var nowTime = now.TimeOfDay;

        foreach (var w in windows)
        {
            if (w.Days.Count == 0) continue;
            if (!TimeSpan.TryParse(w.Start, out var start)) continue;
            if (!TimeSpan.TryParse(w.End, out var end)) continue;
            if (start == end) continue;

            bool inWindow;

            if (start < end)
            {
                // Same-day: blocked when today is in Days and start <= now < end
                inWindow = w.Days.Contains(today)
                        && nowTime >= start
                        && nowTime < end;
            }
            else
            {
                // Overnight (e.g. 23:00–07:00):
                //   Evening portion: today is in Days and now >= start
                //   Morning portion: yesterday is in Days and now < end
                int yesterday = (today + 6) % 7;
                inWindow = (w.Days.Contains(today)    && nowTime >= start)
                        || (w.Days.Contains(yesterday) && nowTime < end);
            }

            if (inWindow) return true;
        }

        return false;
    }
}
