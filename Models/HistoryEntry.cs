using System.Windows.Input;

namespace MpcHcVideoEditor.Models;

/// <summary>
/// One row of the history list: a change that can be moved to.
/// </summary>
/// <remarks>
/// A row rather than a raw step, because what the reader needs is the sentence
/// — "Undo the crop", "Redo the fade" — and the step only carries the bare
/// description. The verb is also what says which side of the present a row is
/// on, which is why the list needs no marker between its two halves.
/// </remarks>
/// <param name="Display">The line as it reads in the menu.</param>
/// <param name="Offset">How many changes away, negative to step back.</param>
public sealed record HistoryEntry(string Display, int Offset)
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
    /// </remarks>
    public ICommand? Command { get; init; }

    /// <summary>What clicking this row would do, spelled out.</summary>
    public string Hint => Offset < 0
        ? $"Step back {Changes(-Offset)}, to before this one"
        : $"Step forward {Changes(Offset)}, to after this one";

    private static string Changes(int n) => n == 1 ? "1 change" : $"{n} changes";
}
