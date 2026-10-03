using System;
using MiniChess;

internal static partial class Program
{
    private static void RunConfigScenarios()
    {
        Run("Move limit one rejects two-tile moves in the rules and in the highlights", () =>
        {
            var turns = MoveLimitBattle(1);
            var battle = new BattleSystem(turns);
            UnitState mover = ClearBoardFor(turns, 0, 0);
            // CanMove drives the green tiles, so it must agree with TryMove.
            Check(!battle.CanMove(One, 0, 0, 2));
            Check(battle.GetMoveDistance(One, 0, 0, 2) == -1);
            Check(!battle.TryMove(One, 0, 0, 2));
            Check(turns.State.GetAP(One) == 4 && !mover.HasMovedThisTurn);
            Check(battle.TryMove(One, 0, 0, 1) && turns.State.GetAP(One) == 3);
        });
        Run("Move limit one keeps every generated bot action legal", () =>
        {
            // A scan radius wider than the limit used to make the bot throw on its own plan.
            var turns = MoveLimitBattle(1, Two);
            BotPlan plan = new DfsBot(2, 20000, 0).FindTurn(turns.State);
            foreach (BotAction action in plan.Actions)
            {
                UnitState unit = turns.State.GetUnit(Two, action.UnitIndex);
                Check(action.Kind != BotActionKind.Move
                    || Math.Max(Math.Abs(action.X - unit.X), Math.Abs(action.Y - unit.Y)) == 1);
                Check(action.Apply(turns));
            }
        });
        Run("The limit caps one move action, not the tiles a unit covers in a turn", () =>
        {
            // AP is what bounds total travel, so a 1-tile limit still crosses two tiles.
            var turns = MoveLimitBattle(1);
            var battle = new BattleSystem(turns);
            UnitState mover = ClearBoardFor(turns, 0, 0);
            Check(battle.TryMove(One, 0, 0, 1) && battle.TryMove(One, 0, 0, 2));
            Check(mover.X == 0 && mover.Y == 2 && turns.State.GetAP(One) == 2);
        });
        Run("Move limit three charges three AP and still refuses a fourth tile", () =>
        {
            var turns = MoveLimitBattle(3);
            var battle = new BattleSystem(turns);
            UnitState mover = ClearBoardFor(turns, 0, 0);
            Check(battle.GetMoveDistance(One, 0, 3, 0) == 3);
            Check(battle.GetMoveDistance(One, 0, 4, 0) == -1);
            Check(battle.TryMove(One, 0, 3, 0));
            Check(turns.State.GetAP(One) == 1 && mover.X == 3 && mover.Y == 0);
        });
        Run("Long moves detour around blockers and pay for the extra steps", () =>
        {
            // Straight-line distance is two, but a wall forces a four-step path.
            var wide = MoveLimitBattle(4);
            ClearBoardFor(wide, 0, 0);
            wide.State.GetUnit(Two, 0).SetPosition(1, 0);
            wide.State.GetUnit(Two, 1).SetPosition(1, 1);
            Check(new BattleSystem(wide).GetMoveDistance(One, 0, 2, 0) == 4);
            var tight = MoveLimitBattle(3);
            ClearBoardFor(tight, 0, 0);
            tight.State.GetUnit(Two, 0).SetPosition(1, 0);
            tight.State.GetUnit(Two, 1).SetPosition(1, 1);
            // Same geometry, but the limit cannot pay for the detour.
            var tightBattle = new BattleSystem(tight);
            Check(tightBattle.GetMoveDistance(One, 0, 2, 0) == -1 && !tightBattle.CanMove(One, 0, 2, 0));
        });
        Run("A per-turn tile budget is what actually caps how far a unit travels", () =>
        {
            var turns = MoveLimitBattle(2, maxMoveTilesPerTurn: 2);
            var battle = new BattleSystem(turns);
            UnitState mover = ClearBoardFor(turns, 0, 0);
            Check(turns.GetRemainingMoveDistance(One, 0) == 2);
            Check(battle.TryMove(One, 0, 0, 1) && mover.MoveTilesThisTurn == 1);
            // One tile of budget left, so the second move may only be a single tile.
            Check(turns.GetRemainingMoveDistance(One, 0) == 1);
            Check(!battle.CanMove(One, 0, 0, 3) && !battle.TryMove(One, 0, 0, 3));
            Check(battle.TryMove(One, 0, 0, 2) && mover.MoveTilesThisTurn == 2);
            // Budget spent: the unit is anchored even though AP and the turn remain.
            Check(!turns.CanMove(One, 0) && !battle.CanMove(One, 0, 0, 3));
            Check(turns.State.GetAP(One) == 2);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(mover.MoveTilesThisTurn == 0 && turns.CanMove(One, 0));
        });
        Run("The per-turn budget stops the bot from chaining moves too", () =>
        {
            var turns = MoveLimitBattle(2, Two, maxMoveTilesPerTurn: 1);
            BotPlan plan = new DfsBot(2, 20000, 0).FindTurn(turns.State);
            var moved = new System.Collections.Generic.HashSet<int>();
            foreach (BotAction action in plan.Actions)
            {
                if (action.Kind == BotActionKind.Move) Check(moved.Add(action.UnitIndex));
                Check(action.Apply(turns));
            }
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                Check(turns.State.GetUnit(Two, index).MoveTilesThisTurn <= 1);
        });
        Run("FourWay reaches no diagonal tile at all, by detour or otherwise", () =>
        {
            var turns = MoveLimitBattle(2, pattern: MovePattern.FourWay);
            var battle = new BattleSystem(turns);
            UnitState mover = ClearBoardFor(turns, 0, 0);
            // The whole point: a diagonal neighbour is unreachable, not merely pricier.
            // Two orthogonal steps would land there, so only the straight-run rule bans it.
            Check(battle.GetMoveDistance(One, 0, 1, 1) == -1);
            Check(!battle.TryMove(One, 0, 1, 1) && turns.State.GetAP(One) == 4);
            Check(battle.GetMoveDistance(One, 0, 2, 1) == -1 && battle.GetMoveDistance(One, 0, 2, 2) == -1);
            // Straight runs still work, up to the configured limit.
            Check(battle.GetMoveDistance(One, 0, 0, 2) == 2 && battle.GetMoveDistance(One, 0, 1, 0) == 1);
            Check(battle.TryMove(One, 0, 0, 2) && turns.State.GetAP(One) == 2);
            Check(mover.X == 0 && mover.Y == 2);
        });
        Run("A FourWay run stops at a blocker instead of walking around it", () =>
        {
            var turns = MoveLimitBattle(3, pattern: MovePattern.FourWay);
            var battle = new BattleSystem(turns);
            ClearBoardFor(turns, 0, 0);
            turns.State.GetUnit(Two, 0).SetPosition(0, 2);
            // (0,3) is a clear straight destination, but the run passes through (0,2).
            Check(battle.GetMoveDistance(One, 0, 0, 3) == -1);
            Check(battle.GetMoveDistance(One, 0, 0, 1) == 1);
            // EightWay would detour around the same blocker, which is the contrast.
            var eightTurns = MoveLimitBattle(3, pattern: MovePattern.EightWay);
            var eight = new BattleSystem(eightTurns);
            ClearBoardFor(eightTurns, 0, 0);
            eightTurns.State.GetUnit(Two, 0).SetPosition(0, 2);
            Check(eight.GetMoveDistance(One, 0, 0, 3) == 3);
        });
        Run("EightWay keeps diagonals at one step, so the pattern really is a setting", () =>
        {
            var turns = MoveLimitBattle(2, pattern: MovePattern.EightWay);
            var battle = new BattleSystem(turns);
            ClearBoardFor(turns, 0, 0);
            Check(battle.GetMoveDistance(One, 0, 1, 1) == 1);
            Check(battle.GetMoveDistance(One, 0, 2, 2) == 2);
            Check(new ActionPointRules().MovePattern == MovePattern.EightWay);
            Check(new ActionPointRules(movePattern: MovePattern.FourWay).MovePattern == MovePattern.FourWay);
            Reject(() => new ActionPointRules(movePattern: (MovePattern)99));
            // The move line in the detail panel names the pattern in Korean.
            Check(UnitDefinitions.Warrior.GetDescription(
                new BattleRules(ap: new ActionPointRules(movePattern: MovePattern.FourWay)))
                .Contains("상하좌우 직선 1~2칸"));
        });
        Run("FourWay blocks a diagonal charge and keeps every bot action legal", () =>
        {
            var rules = new BattleRules(ap: new ActionPointRules(startAP: 6,
                useSkillResource: false, movePattern: MovePattern.FourWay));
            var turns = new TurnSystem(FixedDeployment.Create(7, rules,
                definitions: Lineup(UnitDefinitions.Breaker)));
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            UnitState breaker = turns.State.GetUnit(One, 0);
            turns.State.GetUnit(One, 1).SetPosition(6, 0);
            Check(!battle.CanSkillAt(One, 0, breaker.X + 3, breaker.Y + 3)); // Diagonal dash.
            Check(battle.CanSkillAt(One, 0, breaker.X, breaker.Y + 3));      // Straight dash.
            // Ranges stay diagonal-inclusive even though walking does not.
            UnitState prey = turns.State.GetUnit(Two, 0);
            prey.SetPosition(breaker.X + 1, breaker.Y + 1);
            Check(battle.CanAttack(One, 0, Two, 0));
            for (int seed = 0; seed < 4; seed++)
            {
                var match = new TurnSystem(UnitDraft.CreateRandomMatch(7, rules,
                    UnitDefinitions.Pool, new Random(seed)));
                Check(match.TryStartBattle(One));
                for (int ply = 0; ply < 6 && match.State.Phase == GamePhase.Battle; ply++)
                {
                    BotPlan plan = new DfsBot(2, 6000, 0).FindTurn(match.State);
                    foreach (BotAction action in plan.Actions) Check(action.Apply(match));
                    if (match.State.Phase == GamePhase.Battle && plan.Actions.Count == 0)
                        Check(match.TryEndTurn(match.State.CurrentPlayer.Value));
                }
            }
        });
        Run("Move limit is data, not a constant, and invalid values are rejected", () =>
        {
            Check(new ActionPointRules().MaxMoveDistance == 2);
            Check(new ActionPointRules(maxMoveDistance: 5).MaxMoveDistance == 5);
            Reject(() => new ActionPointRules(maxMoveDistance: 0));
            var rules = new BattleRules(ap: new ActionPointRules(maxMoveDistance: 4));
            Check(UnitDefinitions.Warrior.GetDescription(rules).Contains("1~4칸"));
        });
        Run("Skill text reads before use and matches the numbers combat will apply", () =>
        {
            var rules = new BattleRules(skillCost: 2, ap: new ActionPointRules(defaultSkillAP: 3));
            UnitDefinition controller = UnitDefinitions.Controller;
            string summary = controller.GetSkillSummary(rules);
            // Cost, range and effect must all be visible before the skill is committed.
            Check(summary.Contains("넉백") && summary.Contains("3 AP") && summary.Contains("2 SP"));
            Check(summary.Contains("사거리 1") && summary.Contains("1칸 밀어냅니다"));
            Check(controller.GetSkillCostText(rules) == "3 AP + 2 SP");
            // With the skill resource off the SP half disappears from both texts.
            var apOnly = new BattleRules(ap: new ActionPointRules(useSkillResource: false));
            Check(controller.GetSkillCostText(apOnly) == "3 AP");
            Check(!controller.GetSkillSummary(apOnly).Contains("SP"));
            // Shield power 0 resolves to the rules' shield amount, not a literal zero.
            var shieldRules = new BattleRules(shieldAmount: 4);
            Check(UnitDefinitions.Guardian.GetSkillPower(shieldRules) == 4);
            Check(UnitDefinitions.Guardian.GetSkillSummary(shieldRules).Contains("4만큼"));
            Check(UnitDefinitions.Test.GetSkillSummary(rules) == "스킬 없음");
            Check(UnitDefinitions.Test.GetSkillCostText(rules) == "");
        });
        Run("Per-unit AP overrides and custom names flow into the displayed skill text", () =>
        {
            var strong = new UnitDefinition(UnitKind.Warrior, "중갑병", UnitRole.Combat,
                8, 3, 2, UnitSkill.StrongStrike, "분쇄", 2, 5, 3, skillAPCost: 4);
            var rules = new BattleRules(ap: new ActionPointRules(defaultSkillAP: 3));
            string summary = strong.GetSkillSummary(rules);
            Check(summary.Contains("분쇄") && summary.Contains("4 AP") && summary.Contains("3 SP"));
            Check(summary.Contains("사거리 2") && summary.Contains("5 피해"));
            // The detail panel embeds the same line, so the two can never disagree.
            Check(strong.GetDescription(rules).Contains(summary));
        });
        Run("Bot settings carry difficulty presets and reject impossible budgets", () =>
        {
            var normal = new BotSettings();
            Check(normal.DifficultyName == "Normal" && normal.SearchDepth == 2
                && normal.NodeLimit == 20000 && normal.TimeLimitMs == 300);
            var hard = new BotSettings("Hard", 4, 200000, 2000, 0.05f);
            Check(hard.DifficultyName == "Hard" && hard.SearchDepth == 4 && hard.CreateBot() != null);
            Check(new BotSettings(" ").DifficultyName == "Custom");
            Reject(() => new BotSettings(searchDepth: 0));
            Reject(() => new BotSettings(searchDepth: 5));
            Reject(() => new BotSettings(nodeLimit: 0));
            Reject(() => new BotSettings(timeLimitMs: -1));
            Reject(() => new BotSettings(actionDelay: -1f));
        });
        Run("Search depth bounds how far a preset actually looks ahead", () =>
        {
            var turns = SkillBattle(Two, new BattleRules(initialCost: 0));
            // No time limit, so only the depth setting decides where the search stops.
            foreach (int depth in new[] { 1, 2, 3, 4 })
            {
                BotPlan plan = new BotSettings("Preset", depth, 400000, 0).CreateBot().FindTurn(turns.State);
                Check(plan.CompletedDepth >= 1 && plan.CompletedDepth <= depth);
                Check(plan.Actions.Count > 0);
            }
            Check(new BotSettings("Easy", 1, 400000, 0).CreateBot()
                .FindTurn(turns.State).CompletedDepth == 1);
        });
    }

    // Leaves only Player One's leader on the board so move geometry is explicit.
    private static UnitState ClearBoardFor(TurnSystem turns, int x, int y)
    {
        for (int index = 1; index < GameState.UnitsPerPlayer; index++)
            turns.State.GetUnit(One, index).TakeDamage(99);
        UnitState mover = turns.State.GetUnit(One, 0);
        mover.SetPosition(x, y);
        return mover;
    }

    private static TurnSystem MoveLimitBattle(int maxMoveDistance, PlayerId first = One,
        int startAP = 4, int moveAPPerTile = 1, int maxMoveTilesPerTurn = 0,
        MovePattern pattern = MovePattern.EightWay)
    {
        var rules = new BattleRules(ap: new ActionPointRules(maxAP: 8, startAP: startAP,
            recoveryAP: 4, moveAPPerTile: moveAPPerTile, maxMoveDistance: maxMoveDistance,
            maxMoveTilesPerTurn: maxMoveTilesPerTurn, movePattern: pattern));
        var turns = new TurnSystem(FixedDeployment.Create(6, rules));
        Check(turns.TryStartBattle(first));
        return turns;
    }
}
