using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GameGuard.Models;
using GameGuard.Services;
using MessageBox = System.Windows.MessageBox;

namespace GameGuard.UI;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly ObservableCollection<BlockedAppViewModel> _apps;
    private readonly ObservableCollection<BlockedWindowViewModel> _windows;

    public SettingsWindow(ConfigService configService)
    {
        InitializeComponent();
        _configService = configService;

        var cfg = configService.Config;

        _apps = new ObservableCollection<BlockedAppViewModel>(
            cfg.BlockedApps.Select(a => new BlockedAppViewModel(a)));

        _windows = new ObservableCollection<BlockedWindowViewModel>(
            cfg.BlockedWindows.Select(w => new BlockedWindowViewModel(w)));

        AppsListView.ItemsSource = _apps;
        WindowsListView.ItemsSource = _windows;

        PollIntervalBox.Text = cfg.PollIntervalSeconds.ToString();
        GraceSecondsBox.Text = cfg.GraceSeconds.ToString();
        ToastCooldownBox.Text = cfg.ToastCooldownSeconds.ToString();
    }

    // ---- Blocked Apps tab --------------------------------------------------

    private void AddLauncher_Click(object sender, RoutedEventArgs e) =>
        AddApp(kind: "launcher", pathPinnedDefault: false);

    private void AddGame_Click(object sender, RoutedEventArgs e) =>
        AddApp(kind: "game", pathPinnedDefault: true);

    private void AddApp(string kind, bool pathPinnedDefault)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Select {char.ToUpper(kind[0]) + kind[1..]} Executable",
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog(this) != true) return;

        var fullPath = Path.GetFullPath(dlg.FileName);

        if (_apps.Any(a => string.Equals(a.Model.Path, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "That application is already in the list.",
                "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var model = new BlockedApp
        {
            Id = Guid.NewGuid().ToString(),
            DisplayName = Path.GetFileNameWithoutExtension(fullPath),
            Kind = kind,
            ProcessName = Path.GetFileName(fullPath),
            Path = fullPath,
            PathPinned = pathPinnedDefault
        };

        _apps.Add(new BlockedAppViewModel(model));
        _configService.Logger.Log("app_registered", model.ProcessName, $"kind={kind}");
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (AppsListView.SelectedItem is BlockedAppViewModel vm)
        {
            _configService.Logger.Log("app_removed", vm.Model.ProcessName);
            _apps.Remove(vm);
        }
    }

    // ---- Blocked Windows tab -----------------------------------------------

    private void AddWindow_Click(object sender, RoutedEventArgs e)
    {
        var days = CollectDays();
        if (days.Count == 0)
        {
            MessageBox.Show(this, "Select at least one day.",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ConfigService.TryParseTime(StartTimeBox.Text, out var start))
        {
            MessageBox.Show(this, "Start time must be in HH:mm format (e.g. 23:00).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ConfigService.TryParseTime(EndTimeBox.Text, out var end))
        {
            MessageBox.Show(this, "End time must be in HH:mm format (e.g. 07:00).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (start == end)
        {
            MessageBox.Show(this, "Start and end times cannot be the same.",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var model = new BlockedWindow
        {
            Days = days,
            Start = StartTimeBox.Text.Trim(),
            End = EndTimeBox.Text.Trim()
        };

        _windows.Add(new BlockedWindowViewModel(model));
        _configService.Logger.Log("blocked_window_added",
            detail: $"days=[{string.Join(",", days)}], {model.Start}-{model.End}");
    }

    private void RemoveWindow_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsListView.SelectedItem is BlockedWindowViewModel vm)
        {
            _configService.Logger.Log("blocked_window_removed",
                detail: $"{vm.Model.Start}-{vm.Model.End}");
            _windows.Remove(vm);
        }
    }

    private List<int> CollectDays()
    {
        var days = new List<int>();
        if (DaySun.IsChecked == true) days.Add(0);
        if (DayMon.IsChecked == true) days.Add(1);
        if (DayTue.IsChecked == true) days.Add(2);
        if (DayWed.IsChecked == true) days.Add(3);
        if (DayThu.IsChecked == true) days.Add(4);
        if (DayFri.IsChecked == true) days.Add(5);
        if (DaySat.IsChecked == true) days.Add(6);
        return days;
    }

    // ---- Save / Cancel -----------------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PollIntervalBox.Text, out var poll) || poll < 1)
        {
            MessageBox.Show(this, "Poll interval must be a positive integer (seconds).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(GraceSecondsBox.Text, out var grace) || grace < 1)
        {
            MessageBox.Show(this, "Grace period must be a positive integer (seconds).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(ToastCooldownBox.Text, out var cooldown) || cooldown < 0)
        {
            MessageBox.Show(this, "Toast cooldown must be 0 or greater (seconds).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var config = new AppConfig
        {
            BlockedApps = _apps.Select(vm => vm.Model).ToList(),
            BlockedWindows = _windows.Select(vm => vm.Model).ToList(),
            PollIntervalSeconds = poll,
            GraceSeconds = grace,
            ToastCooldownSeconds = cooldown
        };

        try
        {
            _configService.Save(config);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save settings:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
