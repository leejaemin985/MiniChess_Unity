using System;
using MiniChess;

internal static partial class Program
{
    private static void RunBattleScenarios()
    {
        Run("Fixed deployment contains eight valid sample units on supported board sizes", () =>
        {
            foreach (int size in new[] { 4, 5, 6, 7 })
            {
                GameState state = FixedDeployment.Create(size);
                Check(state.IsDeploymentReady());
                Check(state.GetUnit(One, 0).Definition.Kind == UnitKind.Warrior);
                Check(state.GetUnit(One, 1).HP == 4 && state.GetUnit(Two, 2).AttackRange == 3);
            }
            Reject(() => FixedDeployment.Create(3));
        });
        Run("Incomplete, overlapping and out-of-zone deployment cannot start", () =>
        {
            Check(!new TurnSystem(new GameState()).TryStartBattle());
            var state = FixedDeployment.Create();
            state.GetUnit(One, 0).SetPosition(0, 2);
            Check(!new TurnSystem(state).TryStartBattle(One));
            state = FixedDeployment.Create();
            state.GetUnit(One, 0).SetPosition(3, 1);
            Check(!state.IsDeploymentReady());
            state = FixedDeployment.Create();
            state.GetUnit(One, 0).TakeDamage(3);
            Check(!state.IsDeploymentReady());
            // No leader is required any more: a full roster in its home rows is ready.
            state = new GameState();
            for (int player = 0; player < 2; player++)
                for (int index = 0; index < 4; index++)
                    Check(state.TryAddUnit(MakeUnit((PlayerId)player, index, index,
                        player == 0 ? 0 : state.BoardSize - 2)));
            Check(state.IsDeploymentReady());
            Check(new TurnSystem(state).TryStartBattle(One));
        });
        Run("All eight adjacent empty destinations are legal movement", () =>
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var turns = Battle();
                    UnitState unit = turns.State.GetUnit(One, 0);
                    unit.SetPosition(2, 2);
                    var battle = new BattleSystem(turns);
                    Check(battle.TryMove(One, 0, 2 + dx, 2 + dy));
                    Check(unit.X == 2 + dx && unit.Y == 2 + dy && turns.State.GetAP(One) == 3);
                }
        });
        Run("Invalid moves never change position, resources or notify", () =>
        {
            var turns = Battle();
            var battle = new BattleSystem(turns);
            int changes = 0;
            turns.StateChanged += () => changes++;
            Check(!battle.TryMove(One, 0, -1, 0));
            Check(!battle.TryMove(One, 0, 6, 0));
            Check(!battle.TryMove(One, 0, 1, 0)); // Own tile.
            Check(!battle.TryMove(One, 0, 2, 0)); // Ally.
            Check(!battle.TryMove(One, 0, 1, 3)); // Too far.
            Check(!battle.TryMove(Two, 0, 1, 4));
            Check(!battle.TryMove(One, 99, 0, 0));
            Check(turns.State.GetAP(One) == 4 && turns.State.GetUnit(One, 0).X == 1 && changes == 0);
        });
        Run("Friendly, missing, dead and distant attacks do not spend combat action", () =>
        {
            var turns = Battle();
            var battle = new BattleSystem(turns);
            Check(!battle.TryAttack(One, 0, One, 1));
            Check(!battle.TryAttack(One, 0, Two, 0));
            Check(!battle.TryAttack(One, 0, Two, 9));
            turns.State.GetUnit(Two, 1).TakeDamage(3);
            Check(!battle.TryAttack(One, 0, Two, 1));
            Check(turns.State.GetAP(One) == 4 && turns.State.GetUnit(Two, 0).HP == 3);
        });
        Run("Ranged attacks use diagonal distance and ignore intervening units", () =>
        {
            var turns = new TurnSystem(FixedDeployment.Create());
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            turns.State.GetUnit(Two, 1).SetPosition(2, 2);
            // Ranger at (2,0), allied warrior at (2,1), target at (2,2).
            Check(battle.TryAttack(One, 2, Two, 1));
            Check(turns.State.GetUnit(Two, 1).HP == 3);
            Check(!battle.TryMove(One, 2, 1, 0)); // Attack then same-unit move forbidden.
            Check(battle.TryMove(One, 0, 1, 2)); // Different unit still moves.
            Check(!battle.TryAttack(One, 1, Two, 1));
        });
        Run("Non-leader death frees tile and observers see committed action state", () =>
        {
            var turns = Battle();
            var battle = new BattleSystem(turns);
            UnitState target = turns.State.GetUnit(Two, 1);
            target.SetPosition(0, 1);
            target.TakeDamage(2);
            int changes = 0;
            turns.StateChanged += () =>
            {
                changes++;
                Check(target.HP == 0 && turns.State.GetUnitAt(0, 1) == null);
                Check(turns.State.GetUnit(One, 0).HasCombatActedThisTurn);
            };
            Check(battle.TryAttack(One, 0, Two, 1));
            Check(changes == 1 && turns.State.Phase == GamePhase.Battle);
        });
        Run("Wiping out the last enemy ends the game before observers run", () =>
        {
            var turns = Battle();
            var battle = new BattleSystem(turns);
            // Only the last surviving enemy decides the match now.
            for (int index = 1; index < GameState.UnitsPerPlayer; index++)
                turns.State.GetUnit(Two, index).TakeDamage(3);
            UnitState target = turns.State.GetUnit(Two, 0);
            target.SetPosition(0, 1);
            target.TakeDamage(2);
            Check(turns.State.Phase == GamePhase.Battle);
            int notifications = 0;
            Action verify = () =>
            {
                notifications++;
                Check(!target.IsAlive && turns.State.Phase == GamePhase.Finished && turns.State.Winner == One);
            };
            turns.StateChanged += verify;
            target.Changed += verify;
            Check(battle.TryAttack(One, 0, Two, 0) && notifications == 2);
            Check(!battle.TryMove(One, 1, 2, 1) && !battle.TryAttack(Two, 1, One, 0));
            Check(!turns.TryEndTurn(One));
        });
        Run("Victory needs the last enemy, not any one unit, and restart is fresh", () =>
        {
            var turns = new TurnSystem(FixedDeployment.Create());
            var battle = new BattleSystem(turns);
            Check(turns.TryStartBattle(One));
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            // Three of the four fall, which under the old leader rule would have ended it.
            for (int index = 1; index < GameState.UnitsPerPlayer; index++)
                turns.State.GetUnit(Two, index).TakeDamage(99);
            Check(turns.State.Phase == GamePhase.Battle && turns.State.Winner == null);
            UnitState prey = turns.State.GetUnit(Two, 0);
            prey.TakeDamage(prey.HP - 1);
            UnitState hunter = turns.State.GetUnit(One, 0);
            hunter.SetPosition(prey.X, prey.Y - 1);
            Check(battle.TryAttack(One, 0, Two, 0));
            Check(turns.State.Phase == GamePhase.Finished && turns.State.Winner == One);
            Check(!turns.TryEndTurn(One) && !battle.TryMove(One, 1, 2, 2));
            GameState fresh = FixedDeployment.Create();
            Check(fresh.IsDeploymentReady() && fresh.Winner == null && fresh.TurnNumber == 0);
            Check(fresh.GetUnit(One, 0).HP == 3 && fresh.GetUnit(One, 0).Y == 1);
            Check(new TurnSystem(fresh).TryStartBattle(Two));
        });
    }
}


