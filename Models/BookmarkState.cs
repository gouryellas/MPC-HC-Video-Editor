namespace MpcHcVideoEditor.Models;

/// <summary>
/// Everything about one bookmark that a user can change, frozen.
/// </summary>
/// <remarks>
/// A record rather than a copied <see cref="Bookmark"/>: the live object raises
/// change notifications and is watched by the history, so keeping one in an
/// undo stack would mean the stack held something that could still move. This
/// holds values and nothing else.
///
/// <see cref="Bookmark.Index"/> is deliberately absent. It is a position in the
/// list, not a property of the cut, and restoring a list renumbers it anyway.
/// </remarks>
public sealed record BookmarkState(
    double StartSeconds,
    double EndSeconds,
    bool IsSelected,
    bool IsFlipped,
    double Speed,
    Rotation Rotation,
    bool IsMuted,
    string? Label)
{
    public static BookmarkState From(Bookmark b) => new(
        b.StartSeconds, b.EndSeconds, b.IsSelected, b.IsFlipped,
        b.Speed, b.Rotation, b.IsMuted, b.Label);

    public Bookmark ToBookmark(int index)
    {
        var bookmark = new Bookmark
        {
            Index = index,
            StartSeconds = StartSeconds,
            EndSeconds = EndSeconds,
            IsFlipped = IsFlipped,
            Speed = Speed,
            Rotation = Rotation,
            IsMuted = IsMuted,
            Label = Label
        };

        // Last, and only once the range is in place: an incomplete bookmark
        // refuses to be selected, so assigning this any earlier would silently
        // drop a selection that was genuinely there.
        bookmark.IsSelected = IsSelected;
        return bookmark;
    }
}
