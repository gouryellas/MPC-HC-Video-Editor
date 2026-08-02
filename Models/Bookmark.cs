using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MpcHcVideoEditor.Models;

public class Bookmark : INotifyPropertyChanged
{
    private int _index;
    private double _startSeconds;
    private double _endSeconds;
    private bool _isSelected;
    private bool _isFlipped;
    private double _speed = 1.0;
    private bool _isIncomplete;

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    public double StartSeconds
    {
        get => _startSeconds;
        set
        {
            _startSeconds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartDisplay));
            OnPropertyChanged(nameof(DurationSeconds));
            OnPropertyChanged(nameof(DurationDisplay));
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public double EndSeconds
    {
        get => _endSeconds;
        set
        {
            _endSeconds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndDisplay));
            OnPropertyChanged(nameof(DurationSeconds));
            OnPropertyChanged(nameof(DurationDisplay));
            OnPropertyChanged(nameof(IsValid));
        }
    }

    /// <summary>
    /// Whether the user has checked this bookmark for the actions that work
    /// on a selection (merge, split, play selected, delete, edit times).
    /// </summary>
    /// <remarks>
    /// A bookmark with only an opening timestamp has no range to act on, so
    /// it cannot be selected — assigning <c>true</c> to an incomplete
    /// bookmark is ignored. The rule lives here rather than at each call site
    /// so the checkbox, "Select all", and any future caller all obey it.
    /// </remarks>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var effective = value && IsValid;
            if (_isSelected == effective) return;
            _isSelected = effective;
            OnPropertyChanged();
        }
    }

    public bool IsFlipped
    {
        get => _isFlipped;
        set { _isFlipped = value; OnPropertyChanged(); OnPropertyChanged(nameof(Prefix)); }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.25, 2.0);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpeedDisplay));
            OnPropertyChanged(nameof(Prefix));
            OnPropertyChanged(nameof(EffectiveDurationDisplay));
            OnPropertyChanged(nameof(DurationDisplay)); // in case UI binds to it
        }
    }

    public bool IsIncomplete
    {
        get => _isIncomplete;
        set
        {
            _isIncomplete = value;

            // Reopening a bookmark takes its range away, so it can no longer
            // be part of a selection.
            if (value) IsSelected = false;

            OnPropertyChanged();

            // IsValid reads IsIncomplete, so it has to be announced here too.
            // Without this, closing a bookmark left the row's one-click split
            // button (bound to IsValid) hidden until something else forced a
            // refresh.
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    // Computed properties
    public string StartDisplay => FormatTime(StartSeconds);
    public string EndDisplay => FormatTime(EndSeconds);
    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);
    public string DurationDisplay => FormatDuration(DurationSeconds);
    public string EffectiveDurationDisplay => FormatDuration(DurationSeconds / Speed);
    public string SpeedDisplay => Math.Abs(Speed - 1.0) < 0.01 ? "1x" : $"{Speed:0.##}x";
    public bool IsValid => EndSeconds > StartSeconds && !IsIncomplete;

    public string Prefix
    {
        get
        {
            if (IsFlipped && Math.Abs(Speed - 1.0) > 0.01)
                return $"[FS{Speed:0.##}]";
            if (IsFlipped)
                return "[F]";
            if (Math.Abs(Speed - 1.0) > 0.01)
                return $"[S{Speed:0.##}]";
            return string.Empty;
        }
    }

    public string DisplayText => IsIncomplete
        ? $"[{Index}] {StartDisplay}  (incomplete)"
        : $"[{Index}] {StartDisplay} → {EndDisplay}  ({DurationDisplay})";

    public static string FormatTime(double totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    public static string FormatDuration(double totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        var parts = new List<string>();
        if (ts.Hours > 0) parts.Add($"{ts.Hours}h");
        if (ts.Minutes > 0) parts.Add($"{ts.Minutes}m");
        if (ts.Seconds > 0 || parts.Count == 0) parts.Add($"{ts.Seconds}s");
        return string.Join(" ", parts);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
