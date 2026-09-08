using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MpcHcVideoEditor.Models;

/// <summary>
/// A quarter-turn applied to a clip when it is written.
/// </summary>
/// <remarks>
/// Quarter turns only. Anything else has to pad or crop to fit a rectangle,
/// which is a different feature with its own questions — and the reason to
/// rotate here is almost always footage that arrived a quarter turn out.
///
/// Serialized by name, so the order of these members is not load-bearing.
/// </remarks>
public enum Rotation
{
    /// <summary>Written as recorded.</summary>
    None,

    /// <summary>A quarter turn clockwise.</summary>
    Clockwise,

    /// <summary>A quarter turn anticlockwise.</summary>
    Counterclockwise,

    /// <summary>A half turn. Direction does not apply.</summary>
    UpsideDown
}

public class Bookmark : INotifyPropertyChanged
{
    private int _index;
    private double _startSeconds;
    private double _endSeconds;
    private bool _isSelected;
    private bool _isFlipped;
    private double _speed = 1.0;
    private Rotation _rotation;
    private bool _isMuted;
    private string? _label;

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
            OnPropertyChanged(nameof(RowMarkers));
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

    /// <summary>Quarter turn applied when this clip is written.</summary>
    public Rotation Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value) return;
            _rotation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RotationDisplay));
            OnPropertyChanged(nameof(Prefix));
            OnPropertyChanged(nameof(RowMarkers));
        }
    }

    /// <summary>
    /// Whether this clip is silent in the output.
    /// </summary>
    /// <remarks>
    /// Silenced, not stripped. The audio stream stays in place carrying
    /// nothing — see the note in <c>FFmpegService.CreateSegmentAsync</c> for
    /// why dropping it outright would break a merge.
    /// </remarks>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MuteDisplay));
            OnPropertyChanged(nameof(Prefix));
            OnPropertyChanged(nameof(RowMarkers));
        }
    }

    /// <summary>
    /// This clip's name — a chapter title, and something to tell twenty cuts
    /// apart by. Null or empty when the range is unnamed, which is the state
    /// the whole list is in while "use chapter names" is off.
    /// </summary>
    public string? Label
    {
        get => _label;
        set
        {
            var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (_label == trimmed) return;
            _label = trimmed;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLabel));
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>Whether this bookmark carries a name.</summary>
    public bool HasLabel => !string.IsNullOrWhiteSpace(_label);

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

    /// <summary>Which way the clip will be turned, or nothing at all.</summary>
    public string RotationDisplay => DescribeRotation(Rotation);

    /// <summary>
    /// The row's marker column: everything that will be done to the clip
    /// except the speed, which has a slider and a reading of its own two
    /// columns further along and would otherwise be stated twice.
    /// </summary>
    public string RowMarkers
    {
        get
        {
            var parts = new List<string>(3);
            if (IsFlipped) parts.Add("flipped");
            if (Rotation != Rotation.None) parts.Add(DescribeRotation(Rotation));
            if (IsMuted) parts.Add("muted");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Whether the clip will be silent.</summary>
    public string MuteDisplay => IsMuted ? "muted" : string.Empty;

    public static string DescribeRotation(Rotation rotation) => rotation switch
    {
        Rotation.Clockwise => "turned right",
        Rotation.Counterclockwise => "turned left",
        Rotation.UpsideDown => "upside down",
        _ => string.Empty
    };

    /// <summary>
    /// The next rotation in the cycle, so one button can reach all four
    /// states in the order someone correcting footage would try them.
    /// </summary>
    public static Rotation NextRotation(Rotation current) => current switch
    {
        Rotation.None => Rotation.Clockwise,
        Rotation.Clockwise => Rotation.UpsideDown,
        Rotation.UpsideDown => Rotation.Counterclockwise,
        _ => Rotation.None
    };

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
    /// <remarks>
    /// Joined rather than special-cased per combination: with flip, rotation,
    /// mute and speed there are sixteen of them, and a sentence built from a
    /// list reads the same as the hand-written pairs did without anyone having
    /// to enumerate the cases.
    /// </remarks>
    public string Prefix
    {
        get
        {
            var parts = new List<string>(4);

            if (IsFlipped) parts.Add("flipped");
            if (Rotation != Rotation.None) parts.Add(DescribeRotation(Rotation));
            if (IsMuted) parts.Add("muted");
            if (!Is(Speed, 1.0)) parts.Add(DescribeSpeed(Speed));

            return string.Join(" + ", parts);
        }
    }

    /// <summary>
    /// The row as one line. The name leads when there is one: with twenty cuts
    /// open, "Chapter 7" identifies a clip in a way two timestamps cannot.
    /// </summary>
    public string DisplayText
    {
        get
        {
            var name = HasLabel ? $" {Label}" : string.Empty;

            return IsIncomplete
                ? $"[{Index}]{name} {StartDisplay}  (incomplete)"
                : $"[{Index}]{name} {StartDisplay} → {EndDisplay}  ({DurationDisplay})";
        }
    }

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
