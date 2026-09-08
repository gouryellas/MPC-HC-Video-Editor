namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Two stacks of past states, with a description of the change that left each
/// one behind.
/// </summary>
/// <remarks>
/// Stores whole states rather than reversible operations. The alternative — a
/// command object per action, each knowing how to undo itself — would need
/// every mutation site in the program to be routed through it, and would still
/// miss anything edited straight through a data binding, which never passes a
/// command at all. A state is small enough here that keeping fifty of them
/// costs nothing worth measuring.
///
/// The description belongs to the change that moved <em>away</em> from the
/// stored state, so "Undo <see cref="NextUndo"/>" names what is about to be
/// reversed rather than what will be restored.
/// </remarks>
public sealed class UndoHistory<TState>
{
    private readonly record struct Entry(string Description, TState State);

    private readonly List<Entry> _undo = new();
    private readonly List<Entry> _redo = new();
    private readonly int _capacity;

    /// <param name="capacity">
    /// How many steps back are kept. Oldest are dropped first — an undo stack
    /// that grows without limit is a slow leak in a program left running for
    /// days, which this one is designed to be.
    /// </param>
    public UndoHistory(int capacity = 50) => _capacity = Math.Max(1, capacity);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>What Undo would reverse, for the menu. Null when there is nothing.</summary>
    public string? NextUndo => CanUndo ? _undo[^1].Description : null;

    /// <summary>What Redo would reapply, for the menu. Null when there is nothing.</summary>
    public string? NextRedo => CanRedo ? _redo[^1].Description : null;

    /// <summary>
    /// Records the state as it was before <paramref name="description"/>
    /// happened.
    /// </summary>
    /// <remarks>
    /// Clears the redo stack: once the user has struck out in a new direction,
    /// the branch they had redone their way back toward is unreachable, and
    /// offering to "redo" into it would apply a change built on a state that no
    /// longer exists.
    /// </remarks>
    public void Push(string description, TState stateBefore)
    {
        _undo.Add(new Entry(description, stateBefore));
        if (_undo.Count > _capacity) _undo.RemoveAt(0);
        _redo.Clear();
    }

    /// <summary>
    /// Steps back. Hand in the current state; the state to restore comes back.
    /// </summary>
    public bool TryUndo(TState current, out TState restore)
    {
        restore = default!;
        if (!CanUndo) return false;

        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(new Entry(entry.Description, current));

        restore = entry.State;
        return true;
    }

    /// <summary>Steps forward again, on the same terms as <see cref="TryUndo"/>.</summary>
    public bool TryRedo(TState current, out TState restore)
    {
        restore = default!;
        if (!CanRedo) return false;

        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(new Entry(entry.Description, current));

        restore = entry.State;
        return true;
    }

    /// <summary>
    /// Forgets everything. Called when the list stops being the same list —
    /// a different video, a reload from disk — where stepping back would
    /// restore cuts belonging to a file nobody has open.
    /// </summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
