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
            AnnounceOpenState();
        }
    }

    /// <summary>
    /// The closing time. Zero — or anything not after <see cref="StartSeconds"/>
    /// — means the bookmark is still open.
    /// </summary>
    /// <remarks>
    /// This is the single fact that decides whether a bookmark is open, and
    /// therefore whether the next timestamp opens a new one or closes this
    /// one. Setting it is the only way to close a bookmark, and zeroing it is
    /// the only way to reopen one.
    /// </remarks>
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
            AnnounceOpenState();
        }
    }

    /// <summary>
    /// Raises the notifications for everything derived from whether this
    /// bookmark is open, and drops the selection if it no longer has a range.
    /// </summary>
    private void AnnounceOpenState()
    {
        // An open bookmark has no range to act on, so it cannot stay checked.
        if (IsIncomplete && _isSelected)
        {
            _isSelected = false;
            OnPropertyChanged(nameof(IsSelected));
        }

        OnPropertyChanged(nameof(IsIncomplete));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(DisplayText));
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

    /// <summary>
    /// True while this bookmark is still waiting for its closing timestamp.
    /// </summary>
    /// <remarks>
    /// Derived, not stored. It used to be an independently settable flag,
    /// which meant a bookmark could claim to be complete while holding an end
    /// time of zero — and it did: the row rendered as "6:11 → 0:00 (0s)" and
    /// the app, asking the flag rather than the data, decided the next
    /// timestamp should open a new bookmark instead of closing that one.
    ///
    /// A flag that can contradict the data it describes will eventually
    /// contradict it. Computing the answer from <see cref="EndSeconds"/>
    /// leaves nothing to keep in sync: it is right after a CSV reload, after
    /// an undo, after a hand-edit of the file, and after any code path nobody
    /// remembered to update — because there is nothing to update.
    /// </remarks>
    public bool IsIncomplete => EndSeconds <= StartSeconds;

    // Computed properties
    public string StartDisplay => FormatTime(StartSeconds);
    public string EndDisplay => FormatTime(EndSeconds);
    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);
    public string DurationDisplay => FormatDuration(DurationSeconds);
    public string EffectiveDurationDisplay => FormatDuration(DurationSeconds / Speed);
    public string SpeedDisplay => Math.Abs(Speed - 1.0) < 0.01 ? "1x" : $"{Speed:0.##}x";

    /// <summary>
    /// True when this bookmark describes a real range. The exact complement of
    /// <see cref="IsIncomplete"/> — the two cannot disagree.
    /// </summary>
    public bool IsValid => EndSeconds > StartSeconds;

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
