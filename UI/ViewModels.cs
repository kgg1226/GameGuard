using System.ComponentModel;
using GameGuard.Models;

namespace GameGuard.UI;

/// <summary>
/// Wraps BlockedApp for display in the Blocked Apps ListView.
/// Exposes PathPinned as a two-way bindable property so the checkbox
/// in the GridView cell template can toggle it directly.
/// </summary>
public sealed class BlockedAppViewModel : INotifyPropertyChanged
{
    private bool _pathPinned;

    public BlockedApp Model { get; }

    public BlockedAppViewModel(BlockedApp model)
    {
        Model = model;
        _pathPinned = model.PathPinned;
    }

    public string DisplayName => Model.DisplayName;

    public string Kind => string.IsNullOrEmpty(Model.Kind)
        ? ""
        : char.ToUpper(Model.Kind[0]) + Model.Kind[1..]; // "Launcher" / "Game"

    public string ProcessName => Model.ProcessName;

    public bool PathPinned
    {
        get => _pathPinned;
        set
        {
            if (_pathPinned == value) return;
            _pathPinned = value;
            Model.PathPinned = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PathPinned)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Wraps BlockedWindow for display in the Blocked Windows ListView.
/// </summary>
public sealed class BlockedWindowViewModel
{
    private static readonly string[] DayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    public BlockedWindow Model { get; }

    public BlockedWindowViewModel(BlockedWindow model) => Model = model;

    public string DaysDisplay =>
        string.Join(", ", Model.Days
            .OrderBy(d => d)
            .Where(d => d is >= 0 and <= 6)
            .Select(d => DayNames[d]));

    public string Start => Model.Start;
    public string End => Model.End;

    /// <summary>Shows "Overnight" when start > end (e.g. 23:00–07:00).</summary>
    public string WindowType
    {
        get
        {
            if (!TimeSpan.TryParse(Model.Start, out var s) ||
                !TimeSpan.TryParse(Model.End, out var e)) return "";
            return s > e ? "Overnight" : "Same-day";
        }
    }
}
