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

    /// <summary>
    /// One change, and how far from here it is.
    /// </summary>
    /// <param name="Offset">
    /// Negative to step back, positive to step forward, counted in changes.
    /// −1 is what Undo would do; +1 is Redo. Never zero: the present is not a
    /// change and has no entry of its own.
    /// </param>
    public readonly record struct Step(string Description, int Offset);

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
    /// Every change still reachable, newest first: the furthest-forward redo
    /// down to the oldest undo, with the present in between them.
    /// </summary>
    /// <remarks>
    /// Newest first because that is the end being worked on. The row nearest
    /// the present is the one most likely wanted, and a list that grew downward
    /// would move it further away with every edit.
    ///
    /// Ordinary <see cref="List{T}"/> rather than anything observable: this is
    /// rebuilt from scratch whenever the stacks move, which is cheap at fifty
    /// entries and leaves no chance of a view holding a stale half of one.
    /// </remarks>
    public IReadOnlyList<Step> Timeline
    {
        get
        {
            var steps = new List<Step>(_undo.Count + _redo.Count);

            // _redo[^1] is the next one forward, so the list reads furthest
            // first — the same direction the undo half reads in.
            for (int i = 0; i < _redo.Count; i++)
                steps.Add(new Step(_redo[i].Description, _redo.Count - i));

            for (int i = _undo.Count - 1; i >= 0; i--)
                steps.Add(new Step(_undo[i].Description, -(_undo.Count - i)));

            return steps;
        }
    }

    /// <summary>
    /// Moves by <paramref name="offset"/> changes in one go.
    /// </summary>
    /// <remarks>
    /// Walks one step at a time rather than reaching straight into the stack.
    /// Each step pushes the state it passed through onto the other side, so
    /// every change crossed stays individually reversible — jumping back six
    /// and then wanting only five of them back is an ordinary Redo afterwards,
    /// not a branch that was thrown away.
    ///
    /// Stops early rather than failing if the offset runs past what is there,
    /// and reports whether it moved at all.
    /// </remarks>
    public bool TryMove(int offset, TState current, out TState restore)
    {
        restore = current;
        if (offset == 0) return false;

        var moved = false;
        for (int i = 0; i < Math.Abs(offset); i++)
        {
            // Into a local first. A failed TryUndo clears its out parameter on
            // the way to returning false, so stepping directly into `restore`
            // would throw away everything walked so far on the step that found
            // the end of the stack.
            var stepped = offset < 0
                ? TryUndo(restore, out var next)
                : TryRedo(restore, out next);

            if (!stepped) break;
            restore = next;
            moved = true;
        }

        return moved;
    }

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
