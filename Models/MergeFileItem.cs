using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace MpcHcVideoEditor.Models;

/// <summary>
/// How far a file has got through a merge.
/// </summary>
public enum MergeFileState
{
    /// <summary>Queued, nothing done to it yet.</summary>
    Waiting,

    /// <summary>Being normalised into a segment right now.</summary>
    Preparing,

    /// <summary>Its segment is written and waiting for the join.</summary>
    Merged
}

/// <summary>
/// One file in a merge of whole files, as the cut list shows it while the
/// merge runs.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a <see cref="Bookmark"/>. A merge of arbitrary files has
/// no loaded video and no cut list, so the area that normally shows the cuts
/// sat empty through the one operation whose inputs are least obvious — the
/// files were picked from anywhere rather than cut from something on screen.
/// Borrowing Bookmark to fill it would have meant rows carrying start and end
/// times that are not times in any video the program has open, and every
/// command that acts on a bookmark would have accepted them.
/// </para>
/// <para>
/// So this is its own type with its own templates, and the merge list is shown
/// instead of the cut list rather than inside it. The one thing it copies on
/// purpose is <see cref="IsCurrentStep"/>, which drives the same yellow frame
/// the storyboard already puts on the cut being worked on — the marker means
/// "this is the one happening now" in both places, and it should look the same
/// in both.
/// </para>
/// </remarks>
public sealed class MergeFileItem : INotifyPropertyChanged
{
    public MergeFileItem(int index, string path)
    {
        Index = index;
        FullPath = path;
        FileName = Path.GetFileName(path);
    }

    /// <summary>Position in the merge, counted from one.</summary>
    public int Index { get; }

    public string FullPath { get; }

    /// <summary>The name alone; the full path is the tooltip.</summary>
    public string FileName { get; }

    private double _durationSeconds;

    /// <summary>
    /// How long the file runs. Zero until the probe comes back, which is why
    /// <see cref="DurationDisplay"/> has something to say for that case.
    /// </summary>
    public double DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            if (Math.Abs(_durationSeconds - value) < 0.0005) return;
            _durationSeconds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DurationDisplay));
        }
    }

    /// <summary>
    /// The length, or an en dash while it is still being measured. Not "0:00",
    /// which is a length a file could honestly have.
    /// </summary>
    public string DurationDisplay =>
        _durationSeconds > 0 ? Bookmark.FormatTime(_durationSeconds) : "–";

    private MergeFileState _state = MergeFileState.Waiting;

    public MergeFileState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StateDisplay));
            OnPropertyChanged(nameof(IsMerged));
            OnPropertyChanged(nameof(IsCurrentStep));
        }
    }

    /// <summary>
    /// The state in words, for the one layout with room to spell it out.
    /// </summary>
    public string StateDisplay => _state switch
    {
        MergeFileState.Preparing => "preparing",
        MergeFileState.Merged => "merged",
        _ => "waiting"
    };

    /// <summary>Finished with — drives the struck-through name.</summary>
    public bool IsMerged => _state == MergeFileState.Merged;

    /// <summary>
    /// The one being worked on. Named to match <see cref="Bookmark"/> so the
    /// StepFrame style applies to both without a second copy of it.
    /// </summary>
    public bool IsCurrentStep => _state == MergeFileState.Preparing;

    private ImageSource? _thumbnail;

    /// <summary>A frame from near the start, for the picture layouts.</summary>
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; OnPropertyChanged(); }
    }

    private ImageSource? _endThumbnail;

    /// <summary>
    /// A frame from near the end, for the filmstrip — which shows two frames
    /// per item and would otherwise show the same one twice.
    /// </summary>
    public ImageSource? EndThumbnail
    {
        get => _endThumbnail;
        set { _endThumbnail = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
