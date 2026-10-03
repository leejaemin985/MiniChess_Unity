using System;
using MiniChess;

internal static partial class Program
{
    private static GameState CreateState()
    {
        var state = new GameState();
        foreach (PlayerId owner in new[] { One, Two })
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                Check(state.TryAddUnit(MakeUnit(owner, index,
                    index + 1, owner == One ? 0 : 5)));
        return state;
    }

    private static UnitState MakeUnit(PlayerId owner = One, int index = 0, int x = 0, int y = 0)
    {
        return new UnitState(owner, index, UnitRole.Combat, 3, 1, 1, x, y);
    }

    private static void RunUnitScenarios()
    {
        Run("Damage/healing clamp HP, ignore no-ops, and do not revive", () =>
        {
            var unit = MakeUnit();
            int changes = 0;
            unit.Changed += () => changes++;
            Check(unit.HP == 3 && unit.IsAlive);
            Check(unit.TakeDamage(0) == 0 && unit.Heal(1) == 0 && changes == 0);
            Check(unit.TakeDamage(2) == 2 && unit.HP == 1 && changes == 1);
            Check(unit.Heal(int.MaxValue) == 2 && unit.HP == 3 && changes == 2);
            Check(unit.TakeDamage(int.MaxValue) == 3 && unit.HP == 0 && !unit.IsAlive);
            Check(unit.Heal(3) == 0 && unit.TakeDamage(1) == 0 && changes == 3);
        });
        Run("Invalid unit configuration and negative HP operations are rejected", () =>
        {
            Reject(() => MakeUnit((PlayerId)99));
            Reject(() => MakeUnit(index: -1));
            Reject(() => MakeUnit(index: 4));
            Reject(() => MakeUnit(x: -1));
            Reject(() => MakeUnit(y: -1));
            Reject(() => new UnitState(One, 0, (UnitRole)99, 3, 1, 1, 0, 0));
            Reject(() => new UnitState(One, 0, UnitRole.Combat, 0, 1, 1, 0, 0));
            Reject(() => new UnitState(One, 0, UnitRole.Combat, 3, -1, 1, 0, 0));
            Reject(() => new UnitState(One, 0, UnitRole.Combat, 3, 1, 0, 0, 0));
            var unit = MakeUnit();
            Reject(() => unit.TakeDamage(-1));
            Reject(() => unit.Heal(-1));
            Check(unit.HP == 3);
        });
        Run("Roster rejects duplicate slots, occupied tiles, and out-of-bounds tiles", () =>
        {
            var state = new GameState();
            var first = MakeUnit();
            Check(state.TryAddUnit(first));
            Check(!state.TryAddUnit(null));
            Check(!state.TryAddUnit(MakeUnit(x: 1)));
            Check(!state.TryAddUnit(MakeUnit(Two)));
            // No leader rule any more: a second unit on a free tile simply registers.
            Check(state.TryAddUnit(MakeUnit(index: 1, x: 1)));
            Check(!state.TryAddUnit(MakeUnit(index: 2, x: state.BoardSize)));
            Check(!state.TryAddUnit(MakeUnit(index: 2, y: state.BoardSize)));
            Check(state.TryAddUnit(MakeUnit(Two, y: 5)));
            Check(state.GetUnit(One, 0) == first && state.GetUnitAt(0, 0) == first);
            Check(state.GetUnit((PlayerId)99, 0) == null && state.GetUnit(One, 4) == null);
            Check(state.GetUnitAt(-1, 0) == null);
            var dead = MakeUnit(index: 2, x: 2);
            dead.TakeDamage(3);
            Check(!state.TryAddUnit(dead));
            var turns = new TurnSystem(state);
            Check(!turns.TryStartBattle(One)); // Incomplete rosters cannot start.
            state = CreateState();
            turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            Check(!state.TryAddUnit(MakeUnit(index: 1, x: 1)));
        });
        Run("Dead and missing units cannot act or consume turn resources", () =>
        {
            var state = CreateState();
            var unit = state.GetUnit(One, 0);
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            Check(turns.CanMove(One, 0));
            Check(!turns.TryConsumeMove(One, 4) && !turns.TryConsumeCombat(One, 4));
            unit.TakeDamage(3);
            Check(!turns.TryConsumeMove(One, 0) && !turns.TryConsumeCombat(One, 0));
            Check(state.GetAP(One) == 4);
            Check(state.GetUnitAt(unit.X, unit.Y) == null && state.GetUnit(One, 0) == unit);
        });
        Run("Game clone deep-copies HP, position, roster, and detaches scene listeners", () =>
        {
            var live = CreateState();
            var original = live.GetUnit(One, 0);
            int liveChanges = 0;
            original.Changed += () => liveChanges++;
            original.TakeDamage(1);
            var copy = live.Clone();
            var simulated = copy.GetUnit(One, 0);
            Check(!ReferenceEquals(original, simulated) && simulated.HP == 2);
            simulated.SetPosition(3, 3);
            Check(copy.GetUnitAt(3, 3) == simulated && live.GetUnitAt(3, 3) == null);
            simulated.TakeDamage(2);
            Check(!simulated.IsAlive && original.HP == 2 && original.X == 1 && original.Y == 0);
            Check(liveChanges == 1);
            original.Heal(1);
            Check(liveChanges == 2 && simulated.HP == 0);
        });
        Run("Every role registers without any leader designation", () =>
        {
            foreach (UnitRole role in new[] { UnitRole.Combat, UnitRole.Guardian, UnitRole.Controller })
            {
                var unit = new UnitState(One, 0, role, 4, 1, 1, 0, 0);
                Check(unit.Role == role);
                Check(new GameState().TryAddUnit(unit));
            }
        });
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Expected invalid unit input to be rejected.");
    }
}

