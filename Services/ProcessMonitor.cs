using System.Diagnostics;
using System.IO;
using System.Timers;
using System.Windows.Forms;
using GameGuard.Models;
using Timer = System.Timers.Timer;

namespace GameGuard.Services;

/// <summary>
/// Polls for blocked processes every N seconds.
/// All process control uses native .NET APIs — no shell commands.
/// </summary>
public sealed class ProcessMonitor : IDisposable
{
    private readonly ConfigService _configService;
    private readonly ScheduleService _scheduleService;
    private readonly Logger _logger;
    private readonly NotifyIcon _tray;
    private readonly Timer _timer;

    // Key: "blockedAppId|pid"  →  time of first detection in a blocked window
    private readonly Dictionary<string, DateTime> _detectionTime = new();

    // Key: blockedAppId  →  time the last balloon warning was shown
    private readonly Dictionary<string, DateTime> _lastWarningTime = new();

    public ProcessMonitor(
        ConfigService configService,
        ScheduleService scheduleService,
        Logger logger,
        NotifyIcon tray)
    {
        _configService = configService;
        _scheduleService = scheduleService;
        _logger = logger;
        _tray = tray;

        _timer = new Timer { AutoReset = false };
        _timer.Elapsed += OnTick;
    }

    public void Start()
    {
        _timer.Interval = _configService.Config.PollIntervalSeconds * 1000.0;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        try { Poll(); }
        catch { /* Keep the monitor alive on unexpected errors */ }
        finally
        {
            _timer.Interval = _configService.Config.PollIntervalSeconds * 1000.0;
            _timer.Start();
        }
    }

    private void Poll()
    {
        var config = _configService.Config;
        bool allowed = _scheduleService.IsCurrentlyAllowed(config.Schedule);
        var now = DateTime.Now;
        var activePidKeys = new HashSet<string>();

        foreach (var app in config.BlockedApps)
        {
            // GetProcessesByName expects name without extension
            var nameNoExt = Path.GetFileNameWithoutExtension(app.ProcessName);

            Process[] procs;
            try { procs = Process.GetProcessesByName(nameNoExt); }
            catch { continue; }

            foreach (var proc in procs)
            {
                try
                {
                    if (proc.HasExited) continue;

                    // Optional strict path match
                    if (app.PathPinned && !string.IsNullOrEmpty(app.Path))
                    {
                        try
                        {
                            var module = proc.MainModule?.FileName;
                            if (module != null &&
                                !string.Equals(Path.GetFullPath(module), app.Path,
                                               StringComparison.OrdinalIgnoreCase))
                                continue; // Different executable — not our target
                        }
                        catch
                        {
                            // Access denied reading MainModule.
                            // Fall through: name matched, proceed with enforcement.
                        }
                    }

                    if (allowed)
                    {
                        _logger.Log("allowed_execution", app.ProcessName, $"pid={proc.Id}");
                        continue;
                    }

                    // --- Outside allowed window ---
                    var pidKey = $"{app.Id}|{proc.Id}";
                    activePidKeys.Add(pidKey);

                    if (!_detectionTime.TryGetValue(pidKey, out var detectedAt))
                    {
                        // First detection — warn and start grace period
                        _detectionTime[pidKey] = now;
                        _logger.Log("app_detected", app.ProcessName, $"pid={proc.Id}");

                        var cooldownExpired =
                            !_lastWarningTime.TryGetValue(app.Id, out var lastWarn) ||
                            (now - lastWarn).TotalSeconds >= config.ToastCooldownSeconds;

                        if (cooldownExpired)
                        {
                            _lastWarningTime[app.Id] = now;
                            ShowWarning(app.ProcessName, config.GraceSeconds);
                        }
                    }
                    else if ((now - detectedAt).TotalSeconds >= config.GraceSeconds)
                    {
                        // Grace period elapsed — terminate
                        try
                        {
                            if (!proc.HasExited)
                            {
                                proc.Kill();
                                _logger.Log("app_terminated", app.ProcessName, $"pid={proc.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Log("terminate_failed", app.ProcessName, ex.Message);
                        }

                        _detectionTime.Remove(pidKey);
                        activePidKeys.Remove(pidKey);
                    }
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }

        // Remove stale entries for processes that exited on their own
        foreach (var stale in _detectionTime.Keys.Except(activePidKeys).ToList())
            _detectionTime.Remove(stale);
    }

    private void ShowWarning(string processName, int graceSeconds)
    {
        // ShowBalloonTip is safe to call from a background thread
        _tray.ShowBalloonTip(
            graceSeconds * 1000,
            "GameGuard — Access Restricted",
            $"{processName} is not allowed right now and will be closed in {graceSeconds} seconds.",
            ToolTipIcon.Warning);
    }

    public void Dispose() => _timer.Dispose();
}
