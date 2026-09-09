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
                : $"[{Index}]{name} {StartDisplay} - {EndDisplay}  ({DurationDisplay})";
        }
    }

    // ---- Time formatting -------------------------------------------------
    //
    // Three styles, and one rule for choosing between them, so that a time
    // written in one corner of the app reads the same as the same time written
    // in another:
    //
    //   spoken   4s · 1m 35s · 4hr 25m 30s   how long something lasts
    //   clock    45s · 2:05 · 1:22:05        where you are in the video
    //   precise  00:00:05 · 01:15:30         fixed width, for editing and logs
    //
    // The clock style has one rule about zeros: a zero may follow a figure
    // greater than zero (5:00) or sit between two of them (1:00:04), but a
    // clock reading never opens with one. So there is no 0:04 and no 0:45 —
    // below a minute there is no minutes figure to lead with, and the reading
    // is simply "4s", "45s". Nor is there 01:45; the leading figure is not
    // padded, so a minute and three quarters is "1:45".
    //
    // That is why the sub-minute fallback lives in FormatClock rather than in
    // its callers: a caller that forgot the rule would print "0:04", and every
    // caller has to remember it for the app to read consistently. Past a
    // minute the clock form is the shorter of the two and no less clear, so
    // that is where it takes over.

    /// <summary>
    /// Hours, minutes and seconds of a length in seconds, with hours allowed
    /// to run past 24 — <see cref="TimeSpan.Hours"/> rolls over into Days,
    /// which would report a 25-hour recording as one hour in.
    /// </summary>
    private static (int Hours, int Minutes, int Seconds) Split(double totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var whole = (long)totalSeconds;
        return ((int)(whole / 3600), (int)(whole / 60 % 60), (int)(whole % 60));
    }

    /// <summary>
    /// A length in words — <c>4s</c>, <c>1m 35s</c>, <c>4hr 25m 30s</c>.
    /// Empty parts are left out, so an hour and five seconds is
    /// <c>1hr 5s</c> rather than <c>1hr 0m 5s</c>.
    /// </summary>
    public static string FormatSpoken(double totalSeconds)
    {
        var (h, m, s) = Split(totalSeconds);

        var parts = new List<string>(3);
        if (h > 0) parts.Add($"{h}hr");
        if (m > 0) parts.Add($"{m}m");
        if (s > 0 || parts.Count == 0) parts.Add($"{s}s");
        return string.Join(" ", parts);
    }

    /// <summary>
    /// A position on the clock — <c>45s</c>, <c>1:45</c>, <c>5:00</c>,
    /// <c>1:22:05</c>. The leading figure is never padded and never zero:
    /// below a minute the reading falls back to the spoken style, because
    /// <c>0:45</c> would lead with a zero and <c>01:45</c> would pad the
    /// figure that leads.
    /// </summary>
    public static string FormatClock(double totalSeconds)
    {
        var (h, m, s) = Split(totalSeconds);

        if (h > 0) return $"{h}:{m:D2}:{s:D2}";
        if (m > 0) return $"{m}:{s:D2}";
        return FormatSpoken(totalSeconds);
    }

    /// <summary>
    /// Fixed width, every field padded — <c>00:00:05</c>, <c>01:15:30</c>.
    /// For places that are read a column at a time or typed back in, where a
    /// value that changes length between readings is the wrong shape.
    /// </summary>
    public static string FormatPrecise(double totalSeconds)
    {
        var (h, m, s) = Split(totalSeconds);
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    /// <summary>
    /// A timestamp: where this is in the video. The clock style, under the
    /// name the rest of the app already calls it by.
    /// </summary>
    public static string FormatTime(double totalSeconds) => FormatClock(totalSeconds);

    /// <summary>How long something runs, always in the spoken style.</summary>
    public static string FormatDuration(double totalSeconds)
    {
        // Rounded, not truncated. Only whole seconds are shown, and truncating
        // reported a 2.5s clip as "2s" — always short, never long.
        if (totalSeconds < 0) totalSeconds = 0;
        return FormatSpoken(Math.Round(totalSeconds, MidpointRounding.AwayFromZero));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
