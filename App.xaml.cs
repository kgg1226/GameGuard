using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using GameGuard.Services;
using GameGuard.UI;
using Microsoft.Win32;

namespace GameGuard;

public partial class App : System.Windows.Application
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "GameGuard";

    private NotifyIcon? _tray;
    private Logger? _logger;
    private ConfigService? _configService;
    private ScheduleService? _scheduleService;
    private ProcessMonitor? _monitor;
    private ToolStripMenuItem? _startupMenuItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger = new Logger();
        _configService = new ConfigService(_logger);
        _scheduleService = new ScheduleService();

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "GameGuard",
            Visible = true
        };

        _startupMenuItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = IsStartupEnabled(),
            CheckOnClick = false
        };
        _startupMenuItem.Click += ToggleStartup;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Settings",   null, (_, _) => OpenSettings());
        menu.Items.Add("Status",          null, (_, _) => ShowStatus());
        menu.Items.Add("Open Log Folder", null, (_, _) => OpenLogFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",            null, (_, _) => ExitApp());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenSettings();

        _monitor = new ProcessMonitor(_configService, _scheduleService, _logger, _tray);
        _monitor.Start();

        // Surface a config load error after the tray is ready.
        if (_configService.LoadError != null)
        {
            _tray.ShowBalloonTip(8000,
                "GameGuard — Config Warning",
                $"config.json could not be loaded ({_configService.LoadError}). Default settings applied.",
                ToolTipIcon.Warning);
        }
    }

    // ---- Tray actions ------------------------------------------------------

    private void OpenSettings()
    {
        var win = new SettingsWindow(_configService!);
        win.ShowDialog();

        // Restart monitor so any changed poll interval takes effect immediately.
        _monitor?.Stop();
        _monitor?.Start();
    }

    private void ShowStatus()
    {
        bool blocked = _scheduleService!.IsCurrentlyBlocked(_configService!.Config.BlockedWindows);
        var msg = blocked
            ? "BLOCKED NOW — enforcement is active."
            : "ALLOWED NOW — no restrictions in effect.";
        _tray?.ShowBalloonTip(4000, "GameGuard Status", msg,
            blocked ? ToolTipIcon.Warning : ToolTipIcon.Info);
    }

    private void OpenLogFolder()
    {
        if (_logger == null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_logger.LogDirectory}\"",
                UseShellExecute = false
            });
        }
        catch { /* Ignore */ }
    }

    // ---- Startup registry --------------------------------------------------

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: false);
        return key?.GetValue(StartupValueName) is string;
    }

    private void ToggleStartup(object? sender, EventArgs e)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: true);
            if (key == null) return;

            if (IsStartupEnabled())
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
                _startupMenuItem!.Checked = false;
            }
            else
            {
                var exePath = Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath == null) return;

                key.SetValue(StartupValueName, $"\"{exePath}\"");
                _startupMenuItem!.Checked = true;
            }
        }
        catch { /* Ignore registry errors */ }
    }

    // ---- Lifecycle ---------------------------------------------------------

    private void ExitApp()
    {
        _monitor?.Stop();
        _monitor?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Stop();
        _monitor?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
