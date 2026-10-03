using System;
using MiniChess;

internal static partial class Program
{
    private static void RunSkillScenarios()
    {
        Run("Typed definitions determine stats and skills independently of roster slot", () =>
        {
            var unit = new UnitState(One, 3, UnitDefinitions.Warrior, 0, 0);
            Check(unit.Definition.Skill == UnitSkill.StrongStrike && unit.HP == 3);
            unit = new UnitState(One, 0, UnitDefinitions.Ranger, 0, 0);
            Check(unit.Definition.Kind == UnitKind.Ranger && unit.AttackRange == 3 && unit.HP == 2);
            Check(UnitDefinitions.Guardian.Role == UnitRole.Guardian && UnitDefinitions.Guardian.MaxHP == 4);
            Check(UnitDefinitions.Controller.Role == UnitRole.Controller && UnitDefinitions.Controller.MaxHP == 2);
        });
        Run("Strong strike deals two damage and commits shared cost before notifying", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            UnitState target = turns.State.GetUnit(Two, 1);
            target.SetPosition(2, 2);
            int events = 0;
            turns.StateChanged += () =>
            {
                events++;
                Check(target.HP == 2 && turns.State.PlayerOneCost == 4 && turns.State.GetUnit(One, 0).HasCombatActedThisTurn);
            };
            Check(battle.TrySkill(One, 0, Two, 1) && events == 1);
            Check(!battle.CanMove(One, 0, 1, 1));
            Check(!battle.TryAttack(One, 1, Two, 1));
            Check(turns.State.PlayerTwoCost == 6 && turns.State.GetAP(turns.State.CurrentPlayer.Value) == 1);
        });
        Run("Snipe deals two through intervening units and observes diagonal range four", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            UnitState target = turns.State.GetUnit(Two, 1);
            target.SetPosition(2, 2); // Allied warrior at (2,1) does not block ranger (2,0).
            Check(battle.TrySkill(One, 2, Two, 1) && target.HP == 2);
            turns = SkillBattle();
            battle = new BattleSystem(turns);
            target = turns.State.GetUnit(Two, 1);
            target.SetPosition(4, 4);
            Check(battle.CanSkill(One, 2, Two, 1));
            target.SetPosition(5, 5);
            Check(!battle.TrySkill(One, 2, Two, 1) && turns.State.PlayerOneCost == 6);
        });
        Run("Guardian shields only adjacent allies and shield absorbs before HP", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            UnitState ally = turns.State.GetUnit(One, 0);
            Check(!battle.CanSkill(One, 1, One, 1));
            Check(!battle.CanSkill(One, 1, Two, 0));
            Check(battle.TrySkill(One, 1, One, 0) && ally.Shield == 1);
            Check(ally.TakeDamage(1) == 0 && ally.HP == 3 && ally.Shield == 0);
            Check(ally.TakeDamage(1) == 1 && ally.HP == 2);
        });
        Run("Shield expires at the end of the target side's next turn", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            UnitState ally = turns.State.GetUnit(One, 0);
            Check(battle.TrySkill(One, 1, One, 0));
            Check(turns.TryEndTurn(One) && ally.Shield == 1);
            Check(turns.TryEndTurn(Two) && ally.Shield == 1);
            Check(!battle.TrySkill(One, 1, One, 0)); // Cannot stack or waste cost.
            Check(turns.State.PlayerOneCost == 4 && turns.CanCombat(One, 1));
            Check(turns.TryEndTurn(One) && ally.Shield == 0);
        });
        Run("Knockback pushes one straight or diagonal tile without dealing damage", () =>
        {
            foreach (PlayerId owner in new[] { One, Two })
                foreach (int dx in new[] { 0, 1 })
                {
                    var turns = SkillBattle(owner);
                    var battle = new BattleSystem(turns);
                    PlayerId enemy = owner == One ? Two : One;
                    UnitState caster = turns.State.GetUnit(owner, 3);
                    UnitState target = turns.State.GetUnit(enemy, 1);
                    // Clear central positions are set after deployment to isolate the push rule.
                    caster.SetPosition(0, 2);
                    target.SetPosition(dx, 3);
                    turns.State.GetUnitAt(dx * 2, 4)?.SetPosition(5, 5);
                    Check(battle.TrySkill(owner, 3, enemy, 1));
                    Check(target.X == dx * 2 && target.Y == 4 && target.HP == 4);
                    Check(turns.State.GetCost(owner) == 4 && turns.State.GetAP(turns.State.CurrentPlayer.Value) == 1);
                }
        });
        Run("Blocked, off-board and invalid skills do not consume actions or cost", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            UnitState caster = turns.State.GetUnit(One, 3);
            UnitState target = turns.State.GetUnit(Two, 1);
            caster.SetPosition(0, 1);
            target.SetPosition(0, 0); // Push goes off-board.
            Check(!battle.TrySkill(One, 3, Two, 1));
            caster.SetPosition(0, 2);
            target.SetPosition(0, 3);
            turns.State.GetUnit(One, 2).SetPosition(0, 4);
            Check(!battle.TrySkill(One, 3, Two, 1));
            Check(!battle.TrySkill(Two, 1, One, 0));
            Check(!battle.TrySkill(One, 99, Two, 0));
            Check(!battle.TrySkill(One, 3, Two, 99));
            Check(turns.State.PlayerOneCost == 6 && turns.CanCombat(One, 1) && target.Y == 3);
        });
        Run("Shared cost does not regenerate and basic attacks do not spend skill resource", () =>
        {
            var turns = SkillBattle(One, new BattleRules(2, 2, 1));
            var battle = new BattleSystem(turns);
            Check(battle.TrySkill(One, 1, One, 0));
            Check(turns.State.PlayerOneCost == 0);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(!battle.CanUseSkill(One, 0) && !battle.CanUseSkill(One, 2));
            turns.State.GetUnit(Two, 1).SetPosition(2, 2);
            Check(battle.TryAttack(One, 0, Two, 1) && turns.State.PlayerOneCost == 0);
        });
        Run("Lethal skill ends battle and cloning isolates shield, cost and event listeners", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            Check(battle.TrySkill(One, 1, One, 0));
            GameState clone = turns.State.Clone();
            clone.GetUnit(One, 0).TakeDamage(1);
            clone.PlayerOneCost = 0;
            Check(turns.State.GetUnit(One, 0).Shield == 1 && turns.State.PlayerOneCost == 4);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            // The skill must take out the final enemy for the match to end.
            for (int index = 1; index < GameState.UnitsPerPlayer; index++)
                turns.State.GetUnit(Two, index).TakeDamage(99);
            UnitState last = turns.State.GetUnit(Two, 0);
            last.SetPosition(2, 2);
            last.TakeDamage(1);
            last.Changed += () => Check(turns.State.Winner == One && last.HP == 0);
            Check(battle.TrySkill(One, 0, Two, 0));
            Check(turns.State.Phase == GamePhase.Finished && turns.State.PlayerOneCost == 2);
            Check(!battle.TrySkill(One, 1, One, 0));
            GameState reset = FixedDeployment.Create();
            Check(reset.PlayerOneCost == 6 && reset.GetUnit(One, 0).Shield == 0);
        });
        Run("Balance configuration changes cost and shield absorption without changing definitions", () =>
        {
            var turns = SkillBattle(One, new BattleRules(9, 3, 2));
            var battle = new BattleSystem(turns);
            Check(battle.TrySkill(One, 1, One, 0));
            UnitState ally = turns.State.GetUnit(One, 0);
            Check(turns.State.PlayerOneCost == 6 && ally.Shield == 2);
            Check(ally.TakeDamage(3) == 1 && ally.HP == 2 && ally.Shield == 0);
            Reject(() => new BattleRules(-1));
            Reject(() => new BattleRules(6, 0));
            Reject(() => new BattleRules(6, 2, 0));
        });
    }

    private static TurnSystem SkillBattle(PlayerId first = One, BattleRules rules = null)
    {
        var turns = new TurnSystem(FixedDeployment.Create(6, rules));
        Check(turns.TryStartBattle(first));
        return turns;
    }
}


