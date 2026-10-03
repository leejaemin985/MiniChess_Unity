using MiniChess;

internal static partial class Program
{
    private static void RunArcherScenarios()
    {
        Run("Archer aim is lost immediately on own move and resets next turn", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            var target = turns.State.GetUnit(Two, 1);
            target.SetPosition(1, 4);
            Check(battle.GetAttackRange(One, 2) == 4 && battle.CanAttack(One, 2, Two, 1));
            Check(battle.TryMove(One, 2, 1, 0));
            Check(turns.State.GetUnit(One, 2).HasMovedThisTurn && battle.GetAttackRange(One, 2) == 3);
            Check(!battle.TryAttack(One, 2, Two, 1) && turns.CanCombat(One, 2));
            target.SetPosition(1, 3);
            Check(battle.TryAttack(One, 2, Two, 1));
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(!turns.State.GetUnit(One, 2).HasMovedThisTurn && battle.GetAttackRange(One, 2) == 4);
        });
        Run("Ally movement, failed movement and forced displacement do not cancel aim", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            Check(!battle.TryMove(One, 2, 5, 5));
            Check(battle.GetAttackRange(One, 2) == 4);
            Check(battle.TryMove(One, 0, 1, 2));
            Check(battle.GetAttackRange(One, 2) == 4);
            turns.State.GetUnit(One, 2).SetPosition(0, 0);
            Check(battle.GetAttackRange(One, 2) == 4);
            var copy = turns.State.Clone();
            Check(copy.GetUnit(One, 0).HasMovedThisTurn && new BattleSystem(new TurnSystem(copy)).GetAttackRange(One, 2) == 4);
        });
        Run("Snipe always has range four and cost two even after moving", () =>
        {
            var turns = SkillBattle(One, new BattleRules(2, 5, 1));
            var battle = new BattleSystem(turns);
            Check(battle.TryMove(One, 2, 1, 0));
            var target = turns.State.GetUnit(Two, 1);
            target.SetPosition(1, 4);
            Check(battle.GetSkillCost(One, 2) == 2 && battle.GetAttackRange(One, 2) == 3);
            Check(battle.TrySkill(One, 2, Two, 1) && target.HP == 2 && turns.State.PlayerOneCost == 0);
            Check(!battle.TryMove(One, 2, 0, 0));
            turns = SkillBattle(One, new BattleRules(1, 1, 1));
            battle = new BattleSystem(turns);
            turns.State.GetUnit(Two, 1).SetPosition(2, 4);
            Check(!battle.TrySkill(One, 2, Two, 1) && turns.State.PlayerOneCost == 1 && turns.CanCombat(One, 2));
        });
        Run("Archer basic attack blocks only own movement; clone retains own movement flag", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            turns.State.GetUnit(Two, 1).SetPosition(2, 4);
            Check(battle.TryAttack(One, 2, Two, 1));
            Check(!battle.TryMove(One, 2, 1, 0));
            Check(battle.TryMove(One, 0, 1, 2));
            turns = SkillBattle();
            battle = new BattleSystem(turns);
            Check(battle.TryMove(One, 2, 1, 0));
            var copy = turns.State.Clone();
            Check(new BattleSystem(new TurnSystem(copy)).GetAttackRange(One, 2) == 3);
            Check(turns.TryEndTurn(One));
            Check(copy.GetUnit(One, 2).HasMovedThisTurn);
        });
    }
}

