using System.Windows.Input;

namespace MpcHcVideoEditor.Models;

/// <summary>
/// One row of the history list: a change that can be moved to, or the present
/// sitting between the two halves.
/// </summary>
/// <remarks>
/// A row rather than a raw step, because the list has one thing in it that is
/// not a change at all. Marking where the present is turns two stacks into a
/// single readable timeline — without it, "Redo the crop" and "Undo the crop"
/// in the same list are two entries with no way to tell which side of now each
/// one is on.
/// </remarks>
/// <param name="Display">The line as it reads in the menu.</param>
/// <param name="Offset">
/// How many changes away, negative to step back. Zero for the present, which
/// is also what makes that row unselectable.
/// </param>
/// <param name="IsCurrent">Whether this row is the present.</param>
public sealed record HistoryEntry(string Display, int Offset, bool IsCurrent)
{
    /// <summary>
    /// What this row does, made per row by the ViewModel.
    /// </summary>
    /// <remarks>
    /// Carried on the row rather than reached for through the menu, the way
    /// the naming tag buttons carry theirs. A generated menu item lives in a
    /// Popup — its own visual tree — so a binding that walks up looking for the
    /// window's DataContext has nothing to walk through, and silently resolves
    /// to a command that is never invoked. Binding to the item itself cannot
    /// have that problem.
    ///
    /// Null on the row marking the present, which is not somewhere to go.
    /// </remarks>
    public ICommand? Command { get; init; }

    /// <summary>What clicking this row would do, spelled out.</summary>
    public string Hint => IsCurrent
        ? "Where the cut list stands now"
        : Offset < 0
            ? $"Step back {Changes(-Offset)}, to before this one"
            : $"Step forward {Changes(Offset)}, to after this one";

    private static string Changes(int n) => n == 1 ? "1 change" : $"{n} changes";
}
