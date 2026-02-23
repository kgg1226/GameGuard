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
    private readonly ObservableCollection<BlockedApp> _apps;
    private readonly ObservableCollection<ScheduleViewModel> _schedule;

    public SettingsWindow(ConfigService configService)
    {
        InitializeComponent();
        _configService = configService;

        var cfg = configService.Config;
        _apps = new ObservableCollection<BlockedApp>(cfg.BlockedApps);
        _schedule = new ObservableCollection<ScheduleViewModel>(
            cfg.Schedule.Select(s => new ScheduleViewModel(s)));

        AppsListView.ItemsSource = _apps;
        ScheduleListView.ItemsSource = _schedule;

        PollIntervalBox.Text = cfg.PollIntervalSeconds.ToString();
        GraceSecondsBox.Text = cfg.GraceSeconds.ToString();
        ToastCooldownBox.Text = cfg.ToastCooldownSeconds.ToString();
    }

    // ---- Blocked Apps tab ------------------------------------------------

    private void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Game Launcher Executable",
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog(this) != true) return;

        var fullPath = Path.GetFullPath(dlg.FileName);

        if (_apps.Any(a => string.Equals(a.Path, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "That application is already in the list.",
                "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _apps.Add(new BlockedApp
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            ProcessName = Path.GetFileName(fullPath),
            Path = fullPath,
            PathPinned = PinPathCheckBox.IsChecked == true
        });
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (AppsListView.SelectedItem is BlockedApp app)
            _apps.Remove(app);
    }

    // ---- Schedule tab ----------------------------------------------------

    private void AddSchedule_Click(object sender, RoutedEventArgs e)
    {
        var days = CollectSelectedDays();
        if (days.Count == 0)
        {
            MessageBox.Show(this, "Select at least one day.",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ConfigService.TryParseHHmm(StartTimeBox.Text, out var start))
        {
            MessageBox.Show(this, "Start time must be in HH:mm format (e.g. 21:00).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ConfigService.TryParseHHmm(EndTimeBox.Text, out var end))
        {
            MessageBox.Show(this, "End time must be in HH:mm format (e.g. 23:00).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (end <= start)
        {
            MessageBox.Show(this, "End time must be later than start time.",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _schedule.Add(new ScheduleViewModel(new ScheduleEntry
        {
            Days = days,
            Start = StartTimeBox.Text.Trim(),
            End = EndTimeBox.Text.Trim()
        }));
    }

    private void RemoveSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (ScheduleListView.SelectedItem is ScheduleViewModel vm)
            _schedule.Remove(vm);
    }

    private List<int> CollectSelectedDays()
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

    // ---- Bottom bar ------------------------------------------------------

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
            BlockedApps = _apps.ToList(),
            Schedule = _schedule.Select(vm => vm.Model).ToList(),
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

// ---------------------------------------------------------------------------
// View model used only for displaying schedule rows in the ListView
// ---------------------------------------------------------------------------

public sealed class ScheduleViewModel
{
    private static readonly string[] DayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    public ScheduleEntry Model { get; }

    public ScheduleViewModel(ScheduleEntry model) => Model = model;

    public string DaysDisplay =>
        string.Join(", ", Model.Days.OrderBy(d => d)
                                    .Where(d => d is >= 0 and <= 6)
                                    .Select(d => DayNames[d]));

    public string Start => Model.Start;
    public string End => Model.End;
}
