using Aquila.Services.EditMode;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// The edit session's undo and redo (<see cref="EditHistory{T}"/>), held to its arithmetic with whole numbers for
/// states: a step taken is a new number, and whatever goes back must come back to exactly the number it left.
/// </summary>
public sealed class EditHistoryProperties
{
    /// <summary>What a person does: take a step (to a new state), undo, or redo — in any order, any number of times.
    /// </summary>
    private enum Act { Step, Undo, Redo }

    private static Gen<Act[]> Acts() => Gen.Elements(Act.Step, Act.Step, Act.Undo, Act.Redo).ArrayOf();

    /// <summary>Plays the acts from state 0, as the editor does: a step pushes the state it leaves; undo and redo
    /// trade the current state for the one they hand back.</summary>
    private static (EditHistory<int> History, int State, int Pushes, int Undone, int Redone) Play(IEnumerable<Act> acts)
    {
        var history = new EditHistory<int>();
        var (state, next, pushes, undone, redone) = (0, 1, 0, 0, 0);
        foreach (var act in acts)
            switch (act)
            {
                case Act.Step:
                    history.Push(state);
                    state = next++;
                    pushes++;
                    break;
                case Act.Undo when history.TryUndo(state, out var previous):
                    state = previous;
                    undone++;
                    break;
                case Act.Redo when history.TryRedo(state, out var following):
                    state = following;
                    redone++;
                    break;
            }
        return (history, state, pushes, undone, redone);
    }

    /// <summary>N steps undone N times come back to where the session began.</summary>
    [Property(MaxTest = 200)]
    public Property Undoing_every_step_comes_back_to_the_start() =>
        Prop.ForAll(Gen.Choose(0, 60).ToArbitrary(), steps =>
        {
            var (history, state, _, _, _) = Play(Enumerable.Repeat(Act.Step, steps));
            for (var i = 0; i < steps; i++) history.TryUndo(state, out state);
            return state == 0 && history.Steps == 0 && !history.TryUndo(state, out _);
        });

    /// <summary>Wherever the session stands, an undo followed by a redo stands there again.</summary>
    [Property(MaxTest = 300)]
    public Property Undo_then_redo_comes_back_to_where_it_was() =>
        Prop.ForAll(Acts().ToArbitrary(), acts =>
        {
            var (history, state, _, _, _) = Play(acts);
            if (!history.TryUndo(state, out var back)) return true;
            return history.TryRedo(back, out var again) && again == state;
        });

    /// <summary>A new step after an undo leaves nothing to redo.</summary>
    [Property(MaxTest = 300)]
    public Property A_step_after_an_undo_clears_the_redo() =>
        Prop.ForAll(Acts().ToArbitrary(), acts =>
        {
            var (history, state, _, _, _) = Play([.. acts, Act.Undo, Act.Step]);
            return !history.CanRedo && !history.TryRedo(state, out _);
        });

    /// <summary>The count is never negative, and is the steps taken less those undone, plus those redone — within
    /// the limit, which keeps the latest.</summary>
    [Property(MaxTest = 300)]
    public Property The_count_is_steps_less_undos_plus_redos() =>
        Prop.ForAll(Acts().ToArbitrary(), acts =>
        {
            var (history, _, pushes, undone, redone) = Play(acts);
            return history.Steps >= 0 && history.Steps <= EditHistory<int>.Limit
                   && (pushes > EditHistory<int>.Limit || history.Steps == pushes - undone + redone);
        });
}
