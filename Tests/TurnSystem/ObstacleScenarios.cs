using MiniChess;

internal static partial class Program
{
    private static void RunObstacleScenarios()
    {
        Run("Obstacles are a deployment-time layout choice only", () =>
        {
            var state = new GameState(7, NoResourceRules());
            Check(state.TryAddObstacle(3, 3) && state.HasObstacleAt(3, 3));
            Check(state.Obstacles.Count == 1 && state.IsBlocked(3, 3));
            Check(!state.TryAddObstacle(3, 3)); // Already solid.
            Check(!state.TryAddObstacle(-1, 0) && !state.TryAddObstacle(0, 7));
            Check(state.RemoveObstacle(3, 3) && !state.HasObstacleAt(3, 3));
            Check(!state.RemoveObstacle(3, 3));
            // A tile a unit stands on cannot become a wall.
            GameState deployed = FixedDeployment.Create(7, NoResourceRules());
            UnitState unit = deployed.GetUnit(One, 0);
            Check(!deployed.TryAddObstacle(unit.X, unit.Y));
            Check(deployed.TryAddObstacle(3, 3));
            // Once the battle starts the layout is frozen, which is what lets Clone
            // share the list instead of copying it.
            var turns = new TurnSystem(deployed);
            Check(turns.TryStartBattle(One));
            Check(!deployed.TryAddObstacle(4, 4) && !deployed.RemoveObstacle(3, 3));
            deployed.ClearObstacles();
            Check(deployed.HasObstacleAt(3, 3));
            Check(deployed.Clone().HasObstacleAt(3, 3));
        });
        Run("A wall blocks the tile itself and every route through it", () =>
        {
            var turns = ObstacleBattle(MovePattern.EightWay);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState mover = ClearBoardFor(turns, 0, 0);
            AddWall(state, 0, 1);
            // Nothing may stand on it.
            Check(battle.GetMoveDistance(One, 0, 0, 1) == -1 && !battle.TryMove(One, 0, 0, 1));
            // The detour around it still works, exactly as a unit blocker would allow.
            Check(battle.GetMoveDistance(One, 0, 0, 2) == 2);
            AddWall(state, 1, 1);
            // With both neighbours walled there is no two-step route left.
            Check(battle.GetMoveDistance(One, 0, 0, 2) == -1);
            Check(mover.X == 0 && mover.Y == 0 && state.GetAP(One) == 6);
        });
        Run("A straight run stops dead at a wall instead of rounding it", () =>
        {
            var turns = ObstacleBattle(MovePattern.FourWay);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            ClearBoardFor(turns, 0, 0);
            AddWall(state, 0, 1);
            Check(battle.GetMoveDistance(One, 0, 0, 2) == -1);
            Check(battle.GetMoveDistance(One, 0, 1, 0) == 1); // Sideways is still open.
        });
        Run("A charge cannot pass through a wall and cannot land on one", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Breaker), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            state.GetUnit(One, 1).SetPosition(6, 0);
            Check(battle.CanSkillAt(One, 0, 2, 4)); // Clear lane from (2,1).
            AddWall(state, 2, 3);
            Check(!battle.CanSkillAt(One, 0, 2, 4)); // The wall is mid-lane.
            Check(!battle.CanSkillAt(One, 0, 2, 3)); // And it cannot be the landing tile.
            Check(battle.CanSkillAt(One, 0, 2, 2)); // Short of the wall is fine.
        });
        Run("Shadow Leap clears units but not walls", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Assassin), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState assassin = state.GetUnit(One, 0); // On (2,1), skill range 2.
            state.GetUnit(One, 1).SetPosition(6, 0);
            state.GetUnit(Two, 0).SetPosition(2, 2); // A unit in the way is leapt over.
            Check(battle.CanSkillAt(One, 0, 2, 3));
            state.GetUnit(Two, 0).SetPosition(6, 6);
            // A wall is different: it has to be gone around, and a full row of walls
            // leaves no way around at all within range 2.
            AddWall(state, 1, 2);
            AddWall(state, 2, 2);
            AddWall(state, 3, 2);
            Check(!battle.CanSkillAt(One, 0, 2, 3));
            Check(!battle.CanSkillAt(One, 0, 1, 2)); // Nor onto a wall itself.
            Check(battle.CanSkillAt(One, 0, 1, 0)); // The open side still works.
            Check(assassin.X == 2 && assassin.Y == 1);
        });
        Run("Walls keep traps and fire off their tiles", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Trapper, UnitDefinitions.Pyromancer),
                One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            AddWall(state, 2, 3);
            state.GetUnit(One, 0).SetPosition(2, 2);
            Check(!battle.CanSkillAt(One, 0, 2, 3)); // No trap on a wall.
            Check(battle.CanSkillAt(One, 0, 1, 3));
            state.GetUnit(One, 1).SetPosition(4, 2);
            Check(!battle.CanSkillAt(One, 1, 2, 3)); // No fire centred on a wall.
            // A fire zone beside the wall is clipped so no flame sits on it.
            Check(battle.TrySkillAt(One, 1, 3, 3));
            Check(state.HasAreaAt(3, 3) && !state.HasAreaAt(2, 3));
        });
        Run("A knockback into a wall is refused, as into any solid tile", () =>
        {
            var turns = SkillBattle();
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState target = state.GetUnit(Two, 1);
            target.SetPosition(4, 1);
            Check(battle.CanSkill(One, 3, Two, 1)); // Push lands on (5,2).
            AddWall(state, 5, 2);
            Check(!battle.CanSkill(One, 3, Two, 1));
            Check(target.X == 4 && target.Y == 1);
        });
        Run("Deployment and the random draft both refuse walled tiles", () =>
        {
            GameState state = FixedDeployment.Create(7, NoResourceRules(), includePlayerOne: false);
            Check(state.TryAddObstacle(0, 0));
            var deployment = new DeploymentSystem(state, Lineup());
            Check(deployment.TryStart());
            Check(!deployment.CanPlace(0, 0, 0) && !deployment.TryPlace(0, 0, 0));
            Check(deployment.TryPlace(0, 1, 0));
            // A unit standing on a wall would be an invalid formation.
            Check(!state.TryAddUnit(new UnitState(One, 3, UnitDefinitions.Warrior, 0, 0)));
            // The draft picks only free home tiles.
            var walled = new GameState(7, NoResourceRules());
            for (int x = 0; x < 7; x++) Check(walled.TryAddObstacle(x, 0));
            UnitDraft.PlaceRandomly(walled, One, Lineup(), new System.Random(7));
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                Check(walled.GetUnit(One, index).Y == 1);
        });
        Run("Every unit still plays legally on a walled board", () =>
        {
            for (int seed = 0; seed < 12; seed++)
            {
                var random = new System.Random(seed);
                GameState state = UnitDraft.CreateRandomMatch(7, NoResourceRules(6),
                    UnitDefinitions.Pool, random);
                // Scatter walls across the middle rows, where the fighting happens.
                for (int i = 0; i < 6; i++) state.TryAddObstacle(random.Next(7), 2 + random.Next(3));
                var turns = new TurnSystem(state);
                Check(turns.TryStartBattle((PlayerId)(seed % 2)));
                var bot = new DfsBot(2, 4000, 0);
                for (int ply = 0; ply < 24 && state.Winner == null; ply++)
                {
                    PlayerId side = state.CurrentPlayer.Value;
                    BotPlan plan = bot.FindTurn(state);
                    foreach (BotAction action in plan.Actions)
                    {
                        Check(action.Apply(turns));
                        // Nothing may ever come to rest inside a wall.
                        for (int s = 0; s < 2; s++)
                            for (int i = 0; i < GameState.UnitsPerPlayer; i++)
                            {
                                UnitState unit = state.GetUnit((PlayerId)s, i);
                                if (unit != null && unit.IsAlive)
                                    Check(!state.HasObstacleAt(unit.X, unit.Y));
                            }
                    }
                    if (plan.Actions.Count == 0 || state.CurrentPlayer == side) turns.TryEndTurn(side);
                }
            }
        });
    }

    // Walls are a deployment-phase edit, so a mid-battle scenario steps back briefly.
    private static void AddWall(GameState state, int x, int y)
    {
        GamePhase phase = state.Phase;
        state.Phase = GamePhase.Deployment;
        Check(state.TryAddObstacle(x, y));
        state.Phase = phase;
    }

    private static TurnSystem ObstacleBattle(MovePattern pattern)
    {
        var rules = new BattleRules(ap: new ActionPointRules(maxAP: 8, startAP: 6,
            useSkillResource: false, movePattern: pattern));
        var turns = new TurnSystem(FixedDeployment.Create(7, rules));
        Check(turns.TryStartBattle(One));
        return turns;
    }
}
