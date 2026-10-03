using System;
using MiniChess;

internal static partial class Program
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;
    private static int passed;

    private static void Main()
    {
        Run("Deployment and invalid players reject battle actions", () =>
        {
            var turns = new TurnSystem(CreateState());
            Check(!turns.TryConsumeMove(One, 0) && !turns.TryConsumeCombat(One, 0));
            Check(!turns.TryEndTurn(One) && !turns.TryFinishBattle(One));
            Check(!turns.TryStartBattle((PlayerId)99));
            Check(turns.State.GetAP(One) == 4 && turns.State.TurnNumber == 0);
        });
        Run("Random start gives both players exactly Start AP on their first own turn", () =>
        {
            foreach (PlayerId first in new[] { One, Two })
            {
                var turns = Battle(first);
                Check(turns.State.GetAP(first) == 4);
                Check(turns.TryEndTurn(first));
                Check(turns.State.GetAP(turns.State.CurrentPlayer.Value) == 4);
                Check(!turns.TryStartBattle());
            }
            var random = new TurnSystem(CreateState(), new Random(123));
            Check(random.TryStartBattle() && random.State.CurrentPlayer == (PlayerId)new Random(123).Next(2));
        });
        Run("Each unit may fight once; another unit can spend remaining AP", () =>
        {
            var turns = Battle();
            Check(turns.TryConsumeCombat(One, 0));
            Check(!turns.TryConsumeCombat(One, 0) && !turns.TryConsumeMove(One, 0));
            Check(turns.TryConsumeCombat(One, 1) && turns.State.GetAP(One) == 0);
            Check(!turns.TryConsumeMove(One, 2));
            Check(turns.TryEndTurn(One));
        });
        Run("Multiple movements and repeated movement are allowed until AP is spent", () =>
        {
            var turns = Battle();
            Check(turns.TryConsumeMove(One, 0) && turns.TryConsumeMove(One, 0));
            Check(turns.TryConsumeMove(One, 1, 2) && turns.State.GetAP(One) == 0);
            Check(turns.State.GetUnit(One, 0).HasMovedThisTurn && turns.State.GetUnit(One, 1).HasMovedThisTurn);
        });
        Run("Unused AP carries over and is capped; only own turn recovers it", () =>
        {
            var turns = Battle();
            Check(turns.TryConsumeCombat(One, 0) && turns.State.GetAP(One) == 2);
            Check(turns.TryEndTurn(One) && turns.State.GetAP(One) == 2);
            Check(turns.TryEndTurn(Two) && turns.State.GetAP(One) == 6);
            Check(!turns.State.GetUnit(One, 0).HasCombatActedThisTurn);
            Check(turns.TryEndTurn(One) && turns.State.GetAP(Two) == 6);
            Check(turns.TryEndTurn(Two) && turns.State.GetAP(One) == 6);
        });
        Run("Invalid slots or player preserve AP and flags", () =>
        {
            var turns = Battle();
            foreach (int index in new[] { -1, 4, int.MaxValue })
                Check(!turns.TryConsumeMove(One, index) && !turns.TryConsumeCombat(One, index));
            Check(!turns.TryConsumeMove(Two, 0) && !turns.TryConsumeCombat(Two, 0));
            Check(!turns.TryConsumeMove(One, 0, 3) && !turns.TryConsumeCombat(One, 0, 0));
            Check(turns.State.GetAP(One) == 4 && !turns.State.GetUnit(One, 0).HasMovedThisTurn);
        });
        Run("Clone isolates per-player AP and per-unit turn flags", () =>
        {
            var live = Battle();
            Check(live.TryConsumeMove(One, 0) && live.TryConsumeCombat(One, 1));
            var simulation = new TurnSystem(live.State.Clone());
            Check(simulation.TryConsumeMove(One, 2));
            Check(live.State.GetAP(One) == 1 && simulation.State.GetAP(One) == 0);
            Check(!live.State.GetUnit(One, 2).HasMovedThisTurn);
            Check(simulation.TryEndTurn(One) && simulation.TryEndTurn(Two));
            Check(live.State.GetUnit(One, 1).HasCombatActedThisTurn);
        });
        Run("Finished games reject changes and successful actions notify once", () =>
        {
            var turns = Battle();
            int changes = 0;
            turns.StateChanged += () => changes++;
            Check(!turns.TryEndTurn(Two) && changes == 0);
            Check(turns.TryConsumeMove(One, 0) && changes == 1);
            Check(turns.TryFinishBattle(Two) && changes == 2);
            Check(!turns.TryConsumeMove(One, 0) && !turns.TryConsumeCombat(Two, 0));
            Check(!turns.TryEndTurn(One) && !turns.TryFinishBattle(One));
            Check(turns.State.Winner == Two && changes == 2);
        });
        RunUnitScenarios();
        RunBattleScenarios();
        RunSkillScenarios();
        RunArcherScenarios();
        RunDeploymentScenarios();
        RunBotScenarios();
        RunDefinitionScenarios();
        RunAPScenarios();
        RunConfigScenarios();
        RunNewUnitScenarios();
        RunObstacleScenarios();
        RunCaptureScenarios();
        Console.WriteLine($"Passed {passed} game-rule scenarios.");
    }

    private static TurnSystem Battle(PlayerId first = One)
    {
        var turns = new TurnSystem(CreateState());
        Check(turns.TryStartBattle(first));
        return turns;
    }
    private static void Check(bool condition)
    { if (!condition) throw new Exception("Rule assertion failed."); }
    private static void Run(string name, Action test)
    { test(); passed++; Console.WriteLine($"PASS: {name}"); }
}
