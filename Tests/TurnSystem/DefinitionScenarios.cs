using MiniChess;

internal static partial class Program
{
    private static void RunDefinitionScenarios()
    {
        Run("Configured stats and damage skill values drive both sides and clones", () =>
        {
            var custom = new UnitDefinition(UnitKind.Warrior, "Heavy", UnitRole.Combat,
                9, 3, 2, UnitSkill.StrongStrike, "Smash", 3, 4, 1);
            var lineup = new[] { custom, UnitDefinitions.Guardian, UnitDefinitions.Ranger, UnitDefinitions.Controller };
            var state = FixedDeployment.Create(definitions: lineup);
            Check(state.GetUnit(One, 0).HP == 9 && state.GetUnit(Two, 0).AttackDamage == 3);
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            state.GetUnit(Two, 0).SetPosition(2, 3);
            var battle = new BattleSystem(turns);
            Check(battle.TrySkill(One, 0, Two, 0));
            Check(state.GetUnit(Two, 0).HP == 5 && state.PlayerOneCost == 5);
            GameState copy = state.Clone();
            Check(copy.GetUnit(One, 0).Definition == custom && copy.GetUnit(Two, 0).HP == 5);
        });
        Run("Custom archer passive, snipe range and cost replace old constants", () =>
        {
            var custom = new UnitDefinition(UnitKind.Ranger, "Scout", UnitRole.Combat,
                5, 2, 1, UnitSkill.Snipe, "Shot", 2, 3, 0, 2);
            var state = FixedDeployment.Create(definitions: new[] { UnitDefinitions.Warrior,
                UnitDefinitions.Guardian, custom, UnitDefinitions.Controller });
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            Check(battle.GetAttackRange(One, 2) == 3 && battle.GetSkillCost(One, 2) == 0);
            state.GetUnit(Two, 1).SetPosition(2, 3);
            Check(!battle.CanSkill(One, 2, Two, 1));
            state.GetUnit(Two, 1).SetPosition(2, 2);
            Check(battle.TrySkill(One, 2, Two, 1));
            Check(state.GetUnit(Two, 1).HP == 1 && state.PlayerOneCost == 6);
        });
        Run("Configured shield and knockback powers control their effects", () =>
        {
            var guardian = new UnitDefinition(UnitKind.Guardian, "Guard", UnitRole.Guardian,
                4, 1, 1, UnitSkill.Shield, "Barrier", 2, 3, 1);
            var controller = new UnitDefinition(UnitKind.Controller, "Pusher", UnitRole.Controller,
                2, 1, 1, UnitSkill.Knockback, "Push", 2, 2, 3);
            var state = FixedDeployment.Create(definitions: new[] { UnitDefinitions.Warrior,
                guardian, UnitDefinitions.Ranger, controller });
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            state.GetUnit(One, 0).SetPosition(1, 1);
            Check(battle.TrySkill(One, 1, One, 0) && state.GetUnit(One, 0).Shield == 3);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            state.GetUnit(One, 3).SetPosition(0, 1);
            state.GetUnit(Two, 1).SetPosition(0, 2);
            Check(battle.TrySkill(One, 3, Two, 1));
            Check(state.GetUnit(Two, 1).Y == 4 && state.PlayerOneCost == 2);
        });
        Run("Deployment snapshots configured lineup and description reflects its numbers", () =>
        {
            var custom = new UnitDefinition(UnitKind.Warrior, "Heavy", UnitRole.Combat,
                8, 3, 2, UnitSkill.StrongStrike, "Smash", 2, 5, 3, description: "Frontline fighter.");
            var lineup = new[] { custom, UnitDefinitions.Guardian, UnitDefinitions.Ranger, UnitDefinitions.Controller };
            var state = FixedDeployment.Create(includePlayerOne: false, definitions: lineup);
            var deploy = new DeploymentSystem(state, lineup);
            lineup[0] = UnitDefinitions.Warrior;
            Check(deploy.TryStart() && deploy.TryPlace(0, 0, 0));
            Check(state.GetUnit(One, 0).MaxHP == 8 && state.GetUnit(Two, 0).MaxHP == 8);
            string text = custom.GetDescription(state.Rules);
            Check(text.Contains("체력 8") && text.Contains("3 SP") && text.Contains("사거리 2")
                && text.Contains("5 피해") && text.Contains("Frontline fighter."));
        });
    }
}

