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
/// <remarks>
/// Everything here has to be everything: a property left out is not merely
/// missed by undo, it is <em>destroyed</em> by it, because restoring any
/// snapshot rebuilds every cut from this record and whatever is absent comes
/// back as the default. The fades and the crop were both missing, so undoing a
/// change to the timing quietly cleared them.
/// </remarks>
public sealed record BookmarkState(
    double StartSeconds,
    double EndSeconds,
    bool IsSelected,
    bool IsFlipped,
    double Speed,
    Rotation Rotation,
    bool IsMuted,
    string? Label,
    double FadeInSeconds = 0,
    double FadeOutSeconds = 0,
    double CropX = 0,
    double CropY = 0,
    double CropWidth = 1,
    double CropHeight = 1)
{
    public static BookmarkState From(Bookmark b) => new(
        b.StartSeconds, b.EndSeconds, b.IsSelected, b.IsFlipped,
        b.Speed, b.Rotation, b.IsMuted, b.Label,
        b.FadeInSeconds, b.FadeOutSeconds,
        b.CropX, b.CropY, b.CropWidth, b.CropHeight);

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
            Label = Label,
            FadeInSeconds = FadeInSeconds,
            FadeOutSeconds = FadeOutSeconds
        };

        // One call rather than four assignments: the rectangle's parts clamp
        // against each other, so a width set before its x would be measured
        // against the wrong edge.
        bookmark.SetCrop(CropX, CropY, CropWidth, CropHeight);

        // Last, and only once the range is in place: an incomplete bookmark
        // refuses to be selected, so assigning this any earlier would silently
        // drop a selection that was genuinely there.
        bookmark.IsSelected = IsSelected;
        return bookmark;
    }
}
