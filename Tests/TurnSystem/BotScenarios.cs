using System;
using System.Threading;
using MiniChess;

internal static partial class Program
{
    private static void RunBotScenarios()
    {
        Run("Bot finds lethal skill and never changes live state or listeners", () =>
        {
            var turns = SkillBattle(Two);
            // Leave one enemy standing so a single lethal skill decides the match.
            for (int i = 1; i < GameState.UnitsPerPlayer; i++)
                turns.State.GetUnit(One, i).TakeDamage(99);
            UnitState prey = turns.State.GetUnit(One, 0);
            prey.TakeDamage(1);
            int events = 0;
            turns.StateChanged += () => events++;
            prey.Changed += () => events++;
            BotPlan plan = new DfsBot(2, 100000, 0).FindTurn(turns.State);
            Check(turns.State.Phase == GamePhase.Battle && prey.HP == 2 && events == 0);
            Check(turns.State.PlayerTwoCost == 6 && turns.State.GetAP(Two) == 4);
            ApplyPlan(plan, turns);
            Check(turns.State.Winner == Two && turns.State.PlayerTwoCost == 4);
        });
        Run("Bot searches move then attack to reach an otherwise unreachable enemy", () =>
        {
            var turns = SkillBattle(Two, new BattleRules(0));
            for (int i = 1; i < 4; i++)
            { turns.State.GetUnit(One, i).TakeDamage(99); turns.State.GetUnit(Two, i).TakeDamage(99); }
            turns.State.GetUnit(One, 0).SetPosition(0, 0);
            turns.State.GetUnit(One, 0).TakeDamage(2);
            turns.State.GetUnit(Two, 0).SetPosition(0, 2);
            BotPlan plan = new DfsBot(2, 100000, 0).FindTurn(turns.State);
            Check(plan.Actions.Count == 2 && plan.Actions[0].Kind == BotActionKind.Move
                && plan.Actions[1].Kind == BotActionKind.Attack);
            ApplyPlan(plan, turns);
            Check(turns.State.Winner == Two);
        });
        Run("Depth two considers the opponent's complete move and attack reply", () =>
        {
            var turns = SkillBattle(Two, new BattleRules(0));
            for (int i = 1; i < 4; i++)
            { turns.State.GetUnit(One, i).TakeDamage(99); turns.State.GetUnit(Two, i).TakeDamage(99); }
            turns.State.GetUnit(One, 0).SetPosition(0, 3);
            turns.State.GetUnit(Two, 0).SetPosition(0, 0);
            turns.State.GetUnit(Two, 0).TakeDamage(2);
            BotPlan plan = new DfsBot(2, 100000, 0).FindTurn(turns.State);
            Check(plan.CompletedDepth == 2);
            ApplyPlan(plan, turns);
            Check(turns.State.CurrentPlayer == One);
            ApplyPlan(new DfsBot(1, 100000, 0).FindTurn(turns.State), turns);
            Check(turns.State.Phase == GamePhase.Battle && turns.State.GetUnit(Two, 0).IsAlive);
        });
        Run("Tight node budget returns a legal complete fallback turn", () =>
        {
            var turns = SkillBattle(Two);
            BotPlan plan = new DfsBot(3, 1, 0).FindTurn(turns.State);
            Check(plan.VisitedNodes <= 1 && plan.Actions.Count >= 1 && plan.CompletedDepth == 0);
            ApplyPlan(plan, turns);
            Check(turns.State.CurrentPlayer == One && turns.State.TurnNumber == 2);
        });
        Run("Bot resumes partially consumed turns and can only pass at zero AP", () =>
        {
            var turns = SkillBattle(Two);
            Check(turns.TryConsumeCombat(Two, 0));
            BotPlan plan = new DfsBot(1, 10000, 0).FindTurn(turns.State);
            foreach (BotAction action in plan.Actions)
                Check(action.Kind == BotActionKind.EndTurn
                    || action.UnitIndex != 0);
            ApplyPlan(plan, turns);
            Check(turns.TryEndTurn(One));
            Check(turns.TryConsumeMove(Two, 1) && turns.TryConsumeCombat(Two, 0));
            turns.State.GetPlayerTurn(Two).CurrentAP = 0;
            plan = new DfsBot(2, 10000, 0).FindTurn(turns.State);
            Check(plan.Actions.Count == 1 && plan.Actions[0].Kind == BotActionKind.EndTurn);
        });
        Run("Cancellation and inactive matches never produce executable actions", () =>
        {
            Check(new DfsBot().FindTurn(FixedDeployment.Create()).Actions.Count == 0);
            var turns = SkillBattle(Two);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                bool canceled = false;
                try { new DfsBot().FindTurn(turns.State, cancellation.Token); }
                catch (OperationCanceledException) { canceled = true; }
                Check(canceled && turns.State.TurnNumber == 1);
            }
            turns.TryFinishBattle(One);
            Check(new DfsBot().FindTurn(turns.State).Actions.Count == 0);
        });
        Run("Bounded bot-vs-bot smoke match always applies legal full turns", () =>
        {
            var turns = SkillBattle(One);
            for (int turn = 0; turn < 16 && turns.State.Phase == GamePhase.Battle; turn++)
            {
                int before = turns.State.TurnNumber;
                BotPlan plan = new DfsBot(2, 3000, 0).FindTurn(turns.State);
                ApplyPlan(plan, turns);
                Check(plan.VisitedNodes <= 3000);
                Check(turns.State.Phase == GamePhase.Finished || turns.State.TurnNumber == before + 1);
                Check(turns.State.PlayerOneCost >= 0 && turns.State.PlayerTwoCost >= 0);
            }
        });
    }

    private static void ApplyPlan(BotPlan plan, TurnSystem turns)
    {
        foreach (BotAction action in plan.Actions) Check(action.Apply(turns));
    }
}

