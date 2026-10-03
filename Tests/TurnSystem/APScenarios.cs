using MiniChess;

internal static partial class Program
{
    private static void RunAPScenarios()
    {
        Run("Two-tile movement checks all intermediate routes and charges actual steps", () =>
        {
            var turns = Battle();
            var state = turns.State;
            var battle = new BattleSystem(turns);
            state.GetUnit(One, 0).SetPosition(1, 1);
            state.GetUnit(One, 1).SetPosition(2, 0);
            state.GetUnit(One, 2).SetPosition(2, 1);
            state.GetUnit(One, 3).SetPosition(2, 2);
            Check(!battle.TryMove(One, 0, 3, 1));
            Check(state.GetAP(One) == 4 && !state.GetUnit(One, 0).HasMovedThisTurn);
            state.GetUnit(One, 3).SetPosition(5, 2);
            Check(battle.GetMoveDistance(One, 0, 3, 1) == 2);
            Check(battle.TryMove(One, 0, 3, 1) && state.GetAP(One) == 2);
            Check(battle.TryMove(One, 0, 4, 2) && state.GetAP(One) == 1);
            Check(!battle.TryMove(One, 0, 4, 4)); // Only one AP remains.
        });
        Run("Diagonal two-tile moves cannot jump an occupied intermediate tile", () =>
        {
            var turns = Battle();
            var battle = new BattleSystem(turns);
            turns.State.GetUnit(One, 0).SetPosition(0, 1);
            turns.State.GetUnit(Two, 1).SetPosition(1, 2);
            Check(!battle.TryMove(One, 0, 2, 3));
            turns.State.GetUnit(Two, 1).TakeDamage(3);
            Check(battle.TryMove(One, 0, 2, 3) && turns.State.GetAP(One) == 2);
        });
        Run("Three-tile requests fail but consecutive legal moves are allowed", () =>
        {
            var turns = Battle();
            var battle = new BattleSystem(turns);
            Check(!battle.TryMove(One, 0, 1, 3));
            Check(battle.TryMove(One, 0, 1, 2));
            Check(battle.TryMove(One, 0, 1, 4));
            Check(turns.State.GetAP(One) == 0 && turns.State.GetUnit(One, 0).Y == 4);
        });
        Run("Six AP supports move plus skill plus another unit attack", () =>
        {
            var turns = SkillBattle();
            var state = turns.State;
            var battle = new BattleSystem(turns);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(state.GetAP(One) == 6);
            state.GetUnit(Two, 0).SetPosition(1, 3);
            state.GetUnit(Two, 1).SetPosition(3, 2);
            Check(battle.TryMove(One, 0, 1, 2));
            Check(battle.TrySkill(One, 0, Two, 0));
            Check(battle.TryAttack(One, 1, Two, 1));
            Check(state.GetAP(One) == 0 && state.PlayerOneCost == 4);
            Check(state.GetUnit(One, 0).HasCombatActedThisTurn && state.GetUnit(One, 1).HasCombatActedThisTurn);
            Check(state.GetUnit(Two, 0).HP == 1 && state.GetUnit(Two, 1).HP == 3);
        });
        Run("Skills require both AP and optional match resource without partial spending", () =>
        {
            var turns = SkillBattle(One, new BattleRules(initialCost: 1));
            var battle = new BattleSystem(turns);
            Check(!battle.TrySkill(One, 1, One, 0) && turns.State.GetAP(One) == 4);
            turns = SkillBattle(One, new BattleRules(ap: new ActionPointRules(startAP: 2)));
            battle = new BattleSystem(turns);
            Check(!battle.TrySkill(One, 1, One, 0));
            Check(turns.State.GetAP(One) == 2 && turns.State.PlayerOneCost == 6);
            Check(!turns.State.GetUnit(One, 1).HasCombatActedThisTurn);
            turns = SkillBattle(One, new BattleRules(initialCost: 0, ap: new ActionPointRules(useSkillResource: false)));
            battle = new BattleSystem(turns);
            Check(battle.TrySkill(One, 1, One, 0));
            Check(turns.State.GetAP(One) == 1 && turns.State.PlayerOneCost == 0);
        });
        Run("Custom AP costs, recovery and per-skill override apply independently", () =>
        {
            var guardian = new UnitDefinition(UnitKind.Guardian, "Guard", UnitRole.Guardian,
                4, 1, 1, UnitSkill.Shield, "Barrier", skillAPCost: 4);
            var rules = new BattleRules(ap: new ActionPointRules(maxAP: 8, startAP: 6, recoveryAP: 2,
                moveAPPerTile: 2, maxMoveDistance: 2, attackAP: 3, defaultSkillAP: 3));
            var state = FixedDeployment.Create(rules: rules, definitions: new[] { UnitDefinitions.Warrior,
                guardian, UnitDefinitions.Ranger, UnitDefinitions.Controller });
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            Check(battle.GetSkillAPCost(One, 1) == 4);
            Check(battle.TrySkill(One, 1, One, 0) && state.GetAP(One) == 2);
            Check(battle.TryMove(One, 0, 1, 2) && state.GetAP(One) == 0);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two) && state.GetAP(One) == 2);
            Check(!battle.CanAttack(One, 0, Two, 0));
        });
        Run("Forced movement ignores target combat lock and does not consume its AP", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            var target = turns.State.GetUnit(Two, 1);
            target.SetPosition(4, 1);
            target.HasCombatActedThisTurn = true;
            Check(battle.TrySkill(One, 3, Two, 1));
            Check(target.X == 5 && target.Y == 2 && target.HasCombatActedThisTurn);
            Check(!target.HasMovedThisTurn && turns.State.GetAP(Two) == 4);
        });
        Run("Archer aim stays disabled after other allies subsequently move", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            Check(battle.TryMove(One, 2, 1, 0));
            Check(battle.TryMove(One, 0, 1, 2));
            Check(battle.GetAttackRange(One, 2) == 3 && turns.State.GetAP(One) == 2);
            Check(turns.State.Clone().GetUnit(One, 2).HasMovedThisTurn);
        });
        Run("Bot can combine two units' attacks to win within four AP", () =>
        {
            var turns = SkillBattle(Two, new BattleRules(initialCost: 0));
            // One enemy left, needing two hits, so the win takes two units' attacks.
            for (int i = 1; i < GameState.UnitsPerPlayer; i++)
                turns.State.GetUnit(One, i).TakeDamage(99);
            turns.State.GetUnit(One, 0).SetPosition(1, 2);
            turns.State.GetUnit(One, 0).TakeDamage(1);
            turns.State.GetUnit(Two, 0).SetPosition(1, 1);
            turns.State.GetUnit(Two, 2).SetPosition(0, 2);
            BotPlan plan = new DfsBot(2, 20000, 0).FindTurn(turns.State);
            int attacks = 0;
            foreach (BotAction action in plan.Actions) if (action.Kind == BotActionKind.Attack) attacks++;
            ApplyPlan(plan, turns);
            Check(turns.State.Winner == Two && attacks == 2 && turns.State.GetAP(Two) == 0);
        });
        Run("Bot uses two-tile movement before an attack and respects AP budget", () =>
        {
            var turns = SkillBattle(Two, new BattleRules(initialCost: 0));
            for (int i = 1; i < 4; i++)
            { turns.State.GetUnit(One, i).TakeDamage(99); turns.State.GetUnit(Two, i).TakeDamage(99); }
            turns.State.GetUnit(One, 0).SetPosition(0, 0);
            turns.State.GetUnit(One, 0).TakeDamage(2);
            turns.State.GetUnit(Two, 0).SetPosition(0, 3);
            BotPlan plan = new DfsBot(2, 20000, 0).FindTurn(turns.State);
            ApplyPlan(plan, turns);
            Check(turns.State.Winner == Two && turns.State.GetAP(Two) == 0);
        });
        Run("Invalid AP rules and zero-cost combat are rejected", () =>
        {
            Reject(() => new ActionPointRules(maxAP: 0));
            Reject(() => new ActionPointRules(startAP: 7));
            Reject(() => new ActionPointRules(recoveryAP: -1));
            Reject(() => new ActionPointRules(moveAPPerTile: 0));
            Reject(() => new ActionPointRules(attackAP: 0));
            Reject(() => new ActionPointRules(defaultSkillAP: 0));
        });
    }
}
