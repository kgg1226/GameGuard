using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Timers;
using System.Windows.Forms;
using GameGuard.Models;
using Timer = System.Timers.Timer;

namespace GameGuard.Services;

/// <summary>
/// Polls for blocked processes on a configurable interval.
/// All process control uses native .NET APIs — zero shell commands.
/// AutoReset=false ensures no re-entrant poll ticks.
/// </summary>
public sealed class ProcessMonitor : IDisposable
{
    private readonly ConfigService _configService;
    private readonly ScheduleService _scheduleService;
    private readonly Logger _logger;
    private readonly NotifyIcon _tray;
    private readonly Timer _timer;

    // Key: "appId|pid" → UTC time grace period started
    private readonly Dictionary<string, DateTime> _graceStartedUtc = new();

    // Key: appId → UTC time last balloon was shown (per-app cooldown)
    private readonly Dictionary<string, DateTime> _lastToastUtc = new();

    // "appId|pid" keys for which a warning balloon has already been shown this grace period
    private readonly HashSet<string> _warnedPidKeys = new();

    // "appId|pid" keys for which verify_failed has been logged (avoid per-tick log spam)
    private readonly HashSet<string> _verifyFailedLogged = new();

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

    // -----------------------------------------------------------------------

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        try { Poll(); }
        catch { /* Never let an unhandled exception kill the monitor */ }
        finally
        {
            _timer.Interval = _configService.Config.PollIntervalSeconds * 1000.0;
            _timer.Start();
        }
    }

    private void Poll()
    {
        var config = _configService.Config;
        var nowUtc = DateTime.UtcNow;

        bool isBlocked = _scheduleService.IsCurrentlyBlocked(config.BlockedWindows);

        if (!isBlocked)
        {
            // Time window lifted — cancel all pending grace timers (do not kill).
            if (_graceStartedUtc.Count > 0)
            {
                _graceStartedUtc.Clear();
                _warnedPidKeys.Clear();
                _verifyFailedLogged.Clear();
            }
            return;
        }

        // ---- Blocked window is active — enforce ----
        var activePidKeys = new HashSet<string>();

        foreach (var app in config.BlockedApps)
        {
            var nameNoExt = Path.GetFileNameWithoutExtension(app.ProcessName);

            Process[] procs;
            try { procs = Process.GetProcessesByName(nameNoExt); }
            catch { continue; }

            foreach (var proc in procs)
            {
                try
                {
                    if (proc.HasExited) continue;

                    var pidKey = $"{app.Id}|{proc.Id}";

                    // ----------------------------------------------------------
                    // Path verification for PathPinned apps.
                    // Per spec: if pinned and cannot verify → log once and SKIP.
                    // ----------------------------------------------------------
                    if (app.PathPinned && !string.IsNullOrEmpty(app.Path))
                    {
                        try
                        {
                            var modulePath = proc.MainModule?.FileName;

                            if (modulePath == null)
                            {
                                LogVerifyFailed(pidKey, app.ProcessName, proc.Id, "module_null");
                                continue; // SKIP — cannot confirm identity
                            }

                            if (!string.Equals(
                                    Path.GetFullPath(modulePath), app.Path,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                continue; // Different executable — not our target
                            }
                        }
                        catch (Exception ex)
                        {
                            LogVerifyFailed(pidKey, app.ProcessName, proc.Id, ex.GetType().Name);
                            continue; // SKIP — cannot confirm identity
                        }
                    }

                    activePidKeys.Add(pidKey);

                    // ----------------------------------------------------------
                    // Grace period tracking
                    // ----------------------------------------------------------
                    if (!_graceStartedUtc.TryGetValue(pidKey, out var graceStart))
                    {
                        // First detection in this blocked window — start grace timer.
                        _graceStartedUtc[pidKey] = nowUtc;
                        var plannedKillAt = nowUtc.AddSeconds(config.GraceSeconds);

                        _logger.Log("blocked_detected", app.ProcessName, $"pid={proc.Id}");
                        _logger.Log("grace_started", app.ProcessName,
                            $"pid={proc.Id}, plannedKillAt={plannedKillAt:o}");

                        // Show balloon once per instance, subject to per-app cooldown.
                        if (!_warnedPidKeys.Contains(pidKey))
                        {
                            bool cooldownOk =
                                !_lastToastUtc.TryGetValue(app.Id, out var lastToast) ||
                                (nowUtc - lastToast).TotalSeconds >= config.ToastCooldownSeconds;

                            if (cooldownOk)
                            {
                                _lastToastUtc[app.Id] = nowUtc;
                                _warnedPidKeys.Add(pidKey);
                                ShowWarning(app.DisplayName, config.GraceSeconds);
                            }
                        }
                    }
                    else if (nowUtc >= graceStart.AddSeconds(config.GraceSeconds))
                    {
                        // Grace period elapsed — terminate.
                        TryTerminate(proc, app, pidKey);
                        activePidKeys.Remove(pidKey); // already cleaned up in TryTerminate
                    }
                    // else: still within grace period — do nothing this tick
                }
                catch (Exception ex)
                {
                    _logger.Log("monitor_error", app.ProcessName, ex.Message);
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }

        // Remove grace entries for processes that exited on their own.
        foreach (var stale in _graceStartedUtc.Keys.Except(activePidKeys).ToList())
        {
            _graceStartedUtc.Remove(stale);
            _warnedPidKeys.Remove(stale);
            _verifyFailedLogged.Remove(stale);
        }
    }

    private void TryTerminate(Process proc, BlockedApp app, string pidKey)
    {
        try
        {
            if (proc.HasExited) return;

            proc.Kill();
            _logger.Log("terminated_success", app.ProcessName, $"pid={proc.Id}");
        }
        catch (Win32Exception w32) when (w32.NativeErrorCode == 5) // ERROR_ACCESS_DENIED
        {
            // Target is elevated or otherwise protected — skip, do not retry.
            _logger.Log("terminate_skipped", app.ProcessName,
                $"pid={proc.Id}, reason=access_denied");
        }
        catch (Exception ex)
        {
            _logger.Log("terminated_failed", app.ProcessName,
                $"pid={proc.Id}, {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _graceStartedUtc.Remove(pidKey);
            _warnedPidKeys.Remove(pidKey);
        }
    }

    private void LogVerifyFailed(string pidKey, string processName, int pid, string reason)
    {
        // Log once per (appId|pid) to avoid per-tick spam for elevated processes.
        if (_verifyFailedLogged.Add(pidKey))
            _logger.Log("verify_failed", processName, $"pid={pid}, reason={reason}");
    }

    private void ShowWarning(string displayName, int graceSeconds)
    {
        int minutes = graceSeconds / 60;
        var timeStr = minutes >= 1
            ? $"{minutes} minute{(minutes != 1 ? "s" : "")}"
            : $"{graceSeconds} seconds";

        _tray.ShowBalloonTip(
            10_000,
            "GameGuard — Blocked Time",
            $"Blocked time window active. {displayName} will close in {timeStr}.",
            ToolTipIcon.Warning);
    }

    public void Dispose() => _timer.Dispose();
}
