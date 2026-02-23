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
        menu.Items.Add("Current Status",  null, (_, _) => ShowStatus());
        menu.Items.Add("Open Log Folder", null, (_, _) => OpenLogFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",            null, (_, _) => ExitApp());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenSettings();

        _monitor = new ProcessMonitor(_configService, _scheduleService, _logger, _tray);
        _monitor.Start();
    }

    // ---- Startup registry helpers ----------------------------------------

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
        catch { /* Ignore registry errors silently */ }
    }

    // ---- Tray actions -------------------------------------------------------

    private void OpenSettings()
    {
        var win = new SettingsWindow(_configService!);
        win.ShowDialog();

        // Re-apply config in case poll interval changed
        _monitor?.Stop();
        _monitor?.Start();
    }

    private void ShowStatus()
    {
        bool allowed = _scheduleService!.IsCurrentlyAllowed(_configService!.Config.Schedule);
        var msg = allowed
            ? "ALLOWED — games may run right now."
            : "BLOCKED — games will be terminated.";
        _tray?.ShowBalloonTip(4000, "GameGuard Status", msg,
            allowed ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    private void OpenLogFolder()
    {
        if (_logger == null) return;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_logger.LogDirectory}\"",
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch { /* Ignore */ }
    }

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
