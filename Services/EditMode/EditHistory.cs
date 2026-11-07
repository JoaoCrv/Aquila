namespace Aquila.Services.EditMode;

/// <summary>
/// The steps of an edit session, to go back through and forward again. Generic and pure — it keeps states and
/// knows nothing of what they hold — so the tests hold it to its arithmetic with plain numbers.
///
/// It keeps the state BEFORE each step: <see cref="Push"/> is given the state an edit is about to leave behind,
/// and undoing hands it back in exchange for the current one, which becomes the step to redo. Any new step after
/// an undo clears the redo, as everywhere else.
/// </summary>
public sealed class EditHistory<T>
{
    /// <summary>How many steps are kept. A session of hours keeps the last two hundred: the cost of each is a copy
    /// of the layout and the presets, and nobody undoes further than that by hand.</summary>
    public const int Limit = 200;

    private readonly List<T> _undo = [];
    private readonly List<T> _redo = [];

    /// <summary>How many steps there are to undo — the edit bar's count of unsaved changes.</summary>
    public int Steps => _undo.Count;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>A step was taken, leaving <paramref name="before"/> behind. Clears what could be redone.</summary>
    public void Push(T before)
    {
        _undo.Add(before);
        if (_undo.Count > Limit) _undo.RemoveAt(0);
        _redo.Clear();
    }

    /// <summary>The state before the last step, in exchange for <paramref name="current"/>, which becomes the next
    /// step to redo. False, and nothing changed, when there is nothing to undo.</summary>
    public bool TryUndo(T current, out T previous)
    {
        if (_undo.Count == 0)
        {
            previous = current;
            return false;
        }

        previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(current);
        return true;
    }

    /// <summary>The state the last undo left, in exchange for <paramref name="current"/>. False, and nothing
    /// changed, when there is nothing to redo.</summary>
    public bool TryRedo(T current, out T next)
    {
        if (_redo.Count == 0)
        {
            next = current;
            return false;
        }

        next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(current);
        return true;
    }

    /// <summary>Forgets every step — at the start and at the end of a session.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
