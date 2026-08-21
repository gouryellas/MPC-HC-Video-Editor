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
        set
        {
            _isFlipped = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Prefix));
            OnPropertyChanged(nameof(FlipDisplay));
        }
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

    /// <summary>
    /// What the speed slider will do to this clip, in words — "half speed",
    /// "double speed", "normal".
    /// </summary>
    /// <remarks>
    /// This used to read "0.5x", and the row marker "[S0.5]", which say what
    /// the number is rather than what it does to the clip. The quarter points
    /// the slider snaps to have ordinary names, so they get them; anything else
    /// falls back to the multiplier with the direction spelled out, because
    /// "1.25x" alone leaves the reader to work out which way it goes.
    /// </remarks>
    public string SpeedDisplay => DescribeSpeed(Speed);

    /// <summary>Whether the clip will be inverted, in the same voice.</summary>
    public string FlipDisplay => IsFlipped ? "flipped" : string.Empty;

    private static bool Is(double speed, double value) => Math.Abs(speed - value) < 0.01;

    private static string DescribeSpeed(double speed) =>
        Is(speed, 1.0)  ? "normal"
      : Is(speed, 0.25) ? "quarter speed"
      : Is(speed, 0.5)  ? "half speed"
      : Is(speed, 2.0)  ? "double speed"
      : speed < 1.0     ? $"{speed:0.##}× (slower)"
                        : $"{speed:0.##}× (faster)";

    /// <summary>
    /// True when this bookmark describes a real range. The exact complement of
    /// <see cref="IsIncomplete"/> — the two cannot disagree.
    /// </summary>
    public bool IsValid => EndSeconds > StartSeconds;

    /// <summary>
    /// Everything that will be done to this clip beyond cutting it, as one
    /// phrase. Empty when it will be cut as-is, which is the usual case.
    /// </summary>
    /// <remarks>
    /// Read by the compact overlay, where there is no slider to look at and the
    /// row has to speak for itself. The main window shows the two halves in
    /// their own columns instead — see <see cref="FlipDisplay"/> and
    /// <see cref="SpeedDisplay"/> — so it does not repeat the speed twice.
    /// </remarks>
    public string Prefix
    {
        get
        {
            var flipped = IsFlipped;
            var respeed = !Is(Speed, 1.0);

            if (flipped && respeed) return $"flipped + {DescribeSpeed(Speed)}";
            if (flipped) return "flipped";
            if (respeed) return DescribeSpeed(Speed);
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

        // Rounded, not truncated. Only whole seconds are shown, and truncating
        // reported a 2.5s clip as "2s" — always short, never long.
        var ts = TimeSpan.FromSeconds(Math.Round(totalSeconds, MidpointRounding.AwayFromZero));
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
