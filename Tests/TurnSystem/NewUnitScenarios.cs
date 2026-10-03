using System;
using MiniChess;

internal static partial class Program
{
    private static void RunNewUnitScenarios()
    {
        Run("Draft takes exactly one unit per position from the pool", () =>
        {
            var seen = new System.Collections.Generic.HashSet<UnitKind>();
            for (int seed = 0; seed < 40; seed++)
            {
                UnitDefinition[] roster = UnitDraft.Draft(UnitDefinitions.Pool, new Random(seed));
                Check(roster.Length == GameState.UnitsPerPlayer);
                for (int slot = 0; slot < roster.Length; slot++)
                {
                    Check(roster[slot] != null && roster[slot].Position == UnitDraft.Positions[slot]);
                    seen.Add(roster[slot].Kind);
                }
            }
            // Over many draws every candidate in the pool should show up at least once.
            Check(seen.Contains(UnitKind.Assassin) && seen.Contains(UnitKind.Breaker));
            Check(seen.Contains(UnitKind.Pyromancer) && seen.Contains(UnitKind.Ranger));
            Check(seen.Contains(UnitKind.Warper) && seen.Contains(UnitKind.Trapper));
            // An empty pool still yields a complete, legal roster.
            UnitDefinition[] fallback = UnitDraft.Draft(new UnitDefinition[0], new Random(1));
            Check(fallback.Length == 4 && Array.TrueForAll(fallback, d => d != null));
        });
        Run("Random placement fills distinct home tiles and starts a legal battle", () =>
        {
            for (int seed = 0; seed < 25; seed++)
            {
                var random = new Random(seed);
                GameState state = UnitDraft.CreateRandomMatch(7, NoResourceRules(),
                    UnitDefinitions.Pool, random);
                Check(state.IsDeploymentReady());
                for (int side = 0; side < 2; side++)
                    for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                    {
                        UnitState unit = state.GetUnit((PlayerId)side, index);
                        Check(unit != null && state.GetUnitAt(unit.X, unit.Y) == unit);
                        Check(side == 0 ? unit.Y <= 1 : unit.Y >= state.BoardSize - 2);
                    }
                Check(new TurnSystem(state).TryStartBattle(One));
            }
        });
        Run("Shadow Leap teleports past blockers and still leaves the attack available", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Assassin), One, startAP: 6);
            var battle = new BattleSystem(turns);
            UnitState assassin = turns.State.GetUnit(One, 0);
            // Wall off every route so only a teleport can cross.
            turns.State.GetUnit(One, 1).SetPosition(2, 2);
            turns.State.GetUnit(One, 2).SetPosition(1, 2);
            turns.State.GetUnit(One, 3).SetPosition(3, 2);
            Check(battle.GetMoveDistance(One, 0, 2, 3) == -1);
            Check(battle.CanSkillAt(One, 0, 2, 4) && battle.TrySkillAt(One, 0, 2, 4));
            Check(assassin.X == 2 && assassin.Y == 4 && turns.State.GetAP(One) == 3);
            // A movement skill does not spend the combat action.
            Check(!assassin.HasCombatActedThisTurn && turns.CanCombat(One, 0));
            Check(!battle.CanSkillAt(One, 0, 2, 2)); // Destination must be empty.
        });
        Run("Shadow Leap is refused once the assassin has already fought", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Assassin), One, startAP: 6);
            var battle = new BattleSystem(turns);
            UnitState prey = turns.State.GetUnit(Two, 0);
            prey.SetPosition(2, 2);
            Check(battle.TryAttack(One, 0, Two, 0));
            Check(!battle.CanUseSkill(One, 0) && !battle.TrySkillAt(One, 0, 2, 4));
            Check(turns.State.GetAP(One) == 4);
        });
        Run("Isolation Hunt adds damage only against an unsupported target", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Assassin), One, startAP: 6);
            var battle = new BattleSystem(turns);
            UnitState prey = turns.State.GetUnit(Two, 0);
            UnitState escort = turns.State.GetUnit(Two, 1);
            prey.SetPosition(2, 2);
            escort.SetPosition(3, 2);
            Check(battle.GetAttackDamage(One, 0, Two, 0) == 2);
            escort.SetPosition(6, 6);
            Check(battle.GetAttackDamage(One, 0, Two, 0) == 3);
            int before = prey.HP;
            Check(battle.TryAttack(One, 0, Two, 0) && prey.HP == before - 3);
        });
        Run("Fire Zone covers a plus shape and burns enemies that walk in", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Warrior, UnitDefinitions.Guardian,
                UnitDefinitions.Pyromancer, UnitDefinitions.Controller), One, startAP: 6);
            var battle = new BattleSystem(turns);
            Check(battle.TrySkillAt(One, 2, 2, 3));
            GameState state = turns.State;
            foreach (int[] tile in new[] { new[] { 2, 3 }, new[] { 1, 3 }, new[] { 3, 3 },
                new[] { 2, 2 }, new[] { 2, 4 } })
                Check(state.HasAreaAt(tile[0], tile[1]));
            Check(!state.HasAreaAt(1, 2) && !state.HasAreaAt(3, 4));
            Check(turns.TryEndTurn(One));
            UnitState walker = state.GetUnit(Two, 0);
            walker.SetPosition(2, 6);
            int before = walker.HP;
            Check(new BattleSystem(turns).TryMove(Two, 0, 2, 5));
            Check(walker.HP == before); // Not in the fire yet.
            Check(new BattleSystem(turns).TryMove(Two, 0, 2, 4));
            Check(walker.HP == before - 1);
            // The same field cannot burn the same unit twice in one turn.
            Check(new BattleSystem(turns).TryMove(Two, 0, 2, 3) && walker.HP == before - 1);
        });
        Run("Fire burns at turn start, spares its owner, and expires after two own turns", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Warrior, UnitDefinitions.Guardian,
                UnitDefinitions.Pyromancer, UnitDefinitions.Controller), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            Check(battle.TrySkillAt(One, 2, 2, 3));
            UnitState ally = state.GetUnit(One, 0);
            UnitState enemy = state.GetUnit(Two, 0);
            ally.SetPosition(1, 3);
            enemy.SetPosition(3, 3);
            int allyHP = ally.HP, enemyHP = enemy.HP;
            Check(turns.TryEndTurn(One));
            // Two's turn starts with its unit standing in the flames.
            Check(enemy.HP == enemyHP - 1 && ally.HP == allyHP);
            Check(turns.TryEndTurn(Two));
            Check(ally.HP == allyHP); // Own fire never burns its caster's side.
            Check(state.HasAreaAt(2, 3));
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            // Second own turn start: the field is gone.
            Check(!state.HasAreaAt(2, 3) && state.Areas.Count == 0);
        });
        Run("Ember adds damage against an enemy standing in the caster's own fire", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Warrior, UnitDefinitions.Guardian,
                UnitDefinitions.Pyromancer, UnitDefinitions.Controller), One, startAP: 6);
            var battle = new BattleSystem(turns);
            UnitState prey = turns.State.GetUnit(Two, 0);
            prey.SetPosition(2, 3);
            Check(battle.GetAttackDamage(One, 2, Two, 0) == 2);
            Check(battle.TrySkillAt(One, 2, 2, 3));
            Check(prey.HP == prey.MaxHP - 1); // Caught by the flames immediately.
            Check(battle.GetAttackDamage(One, 2, Two, 0) == 3);
        });
        Run("Swap exchanges two allies and shields them both with Spatial Echo", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Warrior, UnitDefinitions.Guardian,
                UnitDefinitions.Ranger, UnitDefinitions.Warper), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState warper = state.GetUnit(One, 3);
            UnitState first = state.GetUnit(One, 0);
            UnitState second = state.GetUnit(One, 1);
            int firstX = first.X, firstY = first.Y, secondX = second.X, secondY = second.Y;
            Check(Math.Max(Math.Abs(warper.X - firstX), Math.Abs(warper.Y - firstY)) <= 3);
            Check(battle.TrySkillAt(One, 3, firstX, firstY, secondX, secondY));
            Check(first.X == secondX && first.Y == secondY);
            Check(second.X == firstX && second.Y == firstY);
            Check(first.EchoCharges == 1 && second.EchoCharges == 1);
            int before = first.HP;
            first.TakeDamage(2);
            Check(first.HP == before - 1 && first.EchoCharges == 0); // First hit softened.
            first.TakeDamage(1);
            Check(first.HP == before - 2);
        });
        Run("Swap refuses enemies, the same tile and targets out of range", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Warrior, UnitDefinitions.Guardian,
                UnitDefinitions.Ranger, UnitDefinitions.Warper), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState first = state.GetUnit(One, 0);
            UnitState enemy = state.GetUnit(Two, 0);
            Check(!battle.CanSkillAt(One, 3, first.X, first.Y, enemy.X, enemy.Y));
            Check(!battle.CanSkillAt(One, 3, first.X, first.Y, first.X, first.Y));
            UnitState far = state.GetUnit(One, 1);
            far.SetPosition(6, 5);
            Check(!battle.CanSkillAt(One, 3, first.X, first.Y, 6, 5));
            Check(state.GetAP(One) == 6);
        });
        Run("Charge runs straight, stops at blockers and keeps the attack available", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Breaker), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState breaker = state.GetUnit(One, 0);
            Check(!battle.CanSkillAt(One, 0, 4, 2)); // Not a straight line.
            state.GetUnit(One, 1).SetPosition(2, 3);
            Check(!battle.CanSkillAt(One, 0, 2, 4)); // Path is blocked.
            state.GetUnit(One, 1).SetPosition(6, 0);
            Check(battle.TrySkillAt(One, 0, 2, 4));
            Check(breaker.X == 2 && breaker.Y == 4 && state.GetAP(One) == 3);
            Check(!breaker.HasCombatActedThisTurn && breaker.LongestMoveThisTurn == 3);
        });
        Run("Impact rewards a long approach with extra attack damage", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Breaker), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState prey = state.GetUnit(Two, 0);
            prey.SetPosition(2, 5);
            Check(battle.GetAttackDamage(One, 0, Two, 0) == 2);
            Check(battle.TrySkillAt(One, 0, 2, 4)); // Charge three tiles.
            Check(battle.GetAttackDamage(One, 0, Two, 0) == 3);
            int before = prey.HP;
            Check(battle.TryAttack(One, 0, Two, 0) && prey.HP == before - 3);
        });
        Run("Traps damage and mark the enemy that steps on them", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Warrior, UnitDefinitions.Guardian,
                UnitDefinitions.Ranger, UnitDefinitions.Trapper), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState trapper = state.GetUnit(One, 3);
            trapper.SetPosition(2, 3);
            Check(battle.TrySkillAt(One, 3, 2, 4) && state.GetAP(One) == 4);
            Check(state.GetTrapAt(2, 4) != null);
            Check(!battle.CanSkillAt(One, 3, 2, 4)); // A tile holds one trap only.
            Check(turns.TryEndTurn(One));
            UnitState walker = state.GetUnit(Two, 0);
            walker.SetPosition(2, 6);
            int before = walker.HP;
            Check(new BattleSystem(turns).TryMove(Two, 0, 2, 5));
            Check(walker.HP == before && state.GetTrapAt(2, 4) != null);
            Check(new BattleSystem(turns).TryMove(Two, 0, 2, 4));
            Check(walker.HP == before - 1 && state.GetTrapAt(2, 4) == null);
            Check(walker.IsTrapMarkedBy(trapper));
            Check(turns.TryEndTurn(Two));
            // Marked Prey pays off on the trapper's follow-up.
            Check(new BattleSystem(turns).GetAttackDamage(One, 3, Two, 0) == 3);
        });
        Run("A side keeps at most two traps and teleports do not spring them", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Assassin, UnitDefinitions.Guardian,
                UnitDefinitions.Ranger, UnitDefinitions.Trapper), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            state.GetUnit(One, 3).SetPosition(2, 3);
            Check(battle.TrySkillAt(One, 3, 1, 4));
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(battle.TrySkillAt(One, 3, 2, 4));
            Check(state.Traps.Count == 2);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(battle.TrySkillAt(One, 3, 3, 4));
            // The third trap pushes out the oldest, not the newest.
            Check(state.Traps.Count == 2 && state.GetTrapAt(1, 4) == null);
            Check(state.GetTrapAt(2, 4) != null && state.GetTrapAt(3, 4) != null);
            // An assassin blinking onto a friendly trap tile is unaffected either way;
            // an enemy teleport also skips the trigger, which is the rule under test.
            UnitState assassin = state.GetUnit(One, 0);
            int before = assassin.HP;
            Check(battle.TrySkillAt(One, 0, 2, 4));
            Check(assassin.X == 2 && assassin.Y == 4 && assassin.HP == before);
            Check(state.GetTrapAt(2, 4) != null);
        });
        Run("A trap on the way stops the walk on its own tile and charges one tile", () =>
        {
            var turns = StraightMoveBattle();
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState walker = state.GetUnit(Two, 0);
            walker.SetPosition(2, 6);
            state.AddTrap(new TrapEffect(One, 3, 2, 5, 1));
            int before = walker.HP;
            // (2,4) was the request, but the trap on (2,5) interrupts the walk there,
            // and the quoted distance says so before a single AP is spent.
            Check(battle.GetMoveDistance(Two, 0, 2, 4) == 1);
            Check(battle.TryMove(Two, 0, 2, 4));
            Check(walker.X == 2 && walker.Y == 5);
            Check(state.GetAP(Two) == 5); // One tile travelled, one AP.
            Check(walker.HP == before - 1 && state.GetTrapAt(2, 5) == null);
            Check(walker.IsTrapMarkedBy(state.GetUnit(One, 3)));
        });
        Run("A unit a trap stopped may still fight but cannot walk again this turn", () =>
        {
            var turns = StraightMoveBattle();
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState walker = state.GetUnit(Two, 0);
            walker.SetPosition(2, 6);
            state.AddTrap(new TrapEffect(One, 3, 2, 5, 1));
            Check(battle.TryMove(Two, 0, 2, 4));
            Check(walker.MovementStoppedThisTurn);
            // Plenty of AP left, and the tile ahead is empty, but it is held in place.
            Check(state.GetAP(Two) == 5 && state.GetUnitAt(2, 4) == null);
            Check(!turns.CanMove(Two, 0) && !battle.CanMove(Two, 0, 2, 4));
            Check(!battle.TryMove(Two, 0, 2, 4) && walker.Y == 5);
            // Fighting from where it was caught is still allowed.
            Check(turns.CanCombat(Two, 0));
            // Other units are unaffected, and next turn clears the hold.
            Check(turns.CanMove(Two, 1));
            Check(turns.TryEndTurn(Two) && turns.TryEndTurn(One));
            Check(!walker.MovementStoppedThisTurn && turns.CanMove(Two, 0));
        });
        Run("A unit killed on the way stops on the trap that killed it", () =>
        {
            var turns = StraightMoveBattle();
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState walker = state.GetUnit(Two, 0);
            walker.SetPosition(2, 6);
            walker.TakeDamage(walker.HP - 1);
            state.AddTrap(new TrapEffect(One, 3, 2, 5, 1));
            Check(battle.TryMove(Two, 0, 2, 4));
            Check(!walker.IsAlive && walker.X == 2 && walker.Y == 5);
            Check(state.GetUnitAt(2, 4) == null);
        });
        Run("A detour around a blocker is stopped by a trap on the detour", () =>
        {
            var turns = new TurnSystem(FixedDeployment.Create(7, NoResourceRules(6)));
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState mover = ClearBoardFor(turns, 0, 0);
            state.GetUnit(Two, 0).SetPosition(0, 1); // Blocks the straight run.
            // Without the trap the detour reaches (0,2) in two steps.
            Check(battle.GetMoveDistance(One, 0, 0, 2) == 2);
            state.AddTrap(new TrapEffect(Two, 1, 1, 1, 1)); // The only two-step detour.
            Check(battle.GetMoveDistance(One, 0, 0, 2) == 1);
            int before = mover.HP;
            Check(battle.TryMove(One, 0, 0, 2));
            Check(mover.X == 1 && mover.Y == 1 && state.GetAP(One) == 5);
            Check(mover.HP == before - 1 && state.GetTrapAt(1, 1) == null);
            Check(mover.MovementStoppedThisTurn);
        });
        Run("A trap in the lane cuts a charge short and pins the charger", () =>
        {
            var turns = UnitBattle(Lineup(UnitDefinitions.Breaker), One, startAP: 6);
            var battle = new BattleSystem(turns);
            GameState state = turns.State;
            UnitState breaker = state.GetUnit(One, 0); // Starts on (2,1).
            state.GetUnit(One, 1).SetPosition(6, 0);
            state.AddTrap(new TrapEffect(Two, 1, 2, 3, 1)); // Mid-lane, not the landing tile.
            int before = breaker.HP;
            Check(battle.TrySkillAt(One, 0, 2, 4));
            Check(breaker.X == 2 && breaker.Y == 3);
            Check(breaker.HP == before - 1 && state.GetTrapAt(2, 3) == null);
            // Impact reads the distance actually covered, two tiles rather than three.
            Check(breaker.LongestMoveThisTurn == 2 && breaker.MovementStoppedThisTurn);
            // A charge keeps the combat action, and being pinned does not take it away.
            Check(!breaker.HasCombatActedThisTurn && turns.CanCombat(One, 0));
            // Its movement skill is spent for the turn, though: it cannot dash onward.
            Check(!battle.CanUseSkill(One, 0) && !battle.CanSkillAt(One, 0, 2, 5));
        });
        Run("A side's view of the board hides the other side's traps", () =>
        {
            var turns = StraightMoveBattle();
            GameState state = turns.State;
            state.AddTrap(new TrapEffect(One, 3, 2, 5, 1));
            state.AddTrap(new TrapEffect(Two, 3, 4, 4, 1));
            GameState botView = state.CloneAsSeenBy(Two);
            // It keeps what it laid and loses what it was never told about.
            Check(botView.Traps.Count == 1 && botView.GetTrapAt(4, 4) != null);
            Check(botView.GetTrapAt(2, 5) == null);
            Check(state.CloneAsSeenBy(One).GetTrapAt(2, 5) != null);
            Check(state.CloneAsSeenBy(One).GetTrapAt(4, 4) == null);
            // The real board is untouched by being looked at.
            Check(state.Traps.Count == 2);
        });
        Run("A bot planning on its own view walks into a trap it cannot see", () =>
        {
            var turns = StraightMoveBattle();
            GameState state = turns.State;
            UnitState walker = state.GetUnit(Two, 0);
            walker.SetPosition(2, 6);
            state.AddTrap(new TrapEffect(One, 3, 2, 5, 1));
            // On the real board the walk to (2,4) is cut to one tile by the trap.
            Check(new BattleSystem(turns).GetMoveDistance(Two, 0, 2, 4) == 1);
            // In the bot's view the same request is a clean two-tile move, so nothing
            // steers its search away from the trap.
            GameState view = state.CloneAsSeenBy(Two);
            var viewTurns = new TurnSystem(view);
            Check(new BattleSystem(viewTurns).GetMoveDistance(Two, 0, 2, 4) == 2);
            // Planning on that view and playing it for real springs the trap.
            BotPlan plan = new DfsBot(2, 8000, 0).FindTurn(view);
            bool walked = false;
            foreach (BotAction action in plan.Actions)
            {
                if (!action.Apply(turns)) break; // A sprung trap can invalidate the rest.
                walked = true;
            }
            Check(walked);
        });
        Run("Every skill, passive, target and position has Korean display text", () =>
        {
            var rules = new BattleRules();
            foreach (UnitSkill skill in Enum.GetValues(typeof(UnitSkill)))
            {
                var definition = new UnitDefinition(UnitKind.Test, "표본", UnitRole.Combat,
                    3, 1, 1, skill, "표본기술", 2, 1);
                CheckKorean(definition.GetSkillEffectText(rules));
                CheckKorean(definition.GetDescription(rules));
                if (skill == UnitSkill.None)
                {
                    Check(definition.GetSkillSummary(rules) == "스킬 없음");
                    Check(definition.GetSkillCostText(rules) == "");
                    continue;
                }
                // Cost, range, target and effect must all be present and readable.
                string summary = definition.GetSkillSummary(rules);
                CheckKorean(summary);
                Check(summary.Contains("사거리") && summary.Contains("대상")
                    && summary.Contains("AP") && summary.StartsWith("표본기술"));
                CheckKorean(definition.GetSkillTargetText());
            }
            foreach (UnitPassive passive in Enum.GetValues(typeof(UnitPassive)))
            {
                var definition = new UnitDefinition(UnitKind.Test, "표본", UnitRole.Combat,
                    3, 1, 1, UnitSkill.None, "없음", passive: passive);
                string text = definition.GetPassiveText();
                Check(passive == UnitPassive.None ? text == "" : text.Contains(":"));
                CheckKorean(text);
                // A unit with a passive shows it in the detail panel too.
                if (passive != UnitPassive.None) Check(definition.GetDescription(rules).Contains(text));
            }
            foreach (UnitPosition position in Enum.GetValues(typeof(UnitPosition)))
                CheckKorean(UnitDefinition.GetPositionName(position));
            // The shipped roster is fully translated, names and all.
            foreach (UnitDefinition definition in UnitDefinitions.Pool)
            {
                CheckKorean(definition.Name);
                CheckKorean(definition.SkillName);
                CheckKorean(definition.GetDescription(rules));
            }
        });
        Run("Every drafted unit can be simulated by the bot without illegal actions", () =>
        {
            for (int seed = 0; seed < 12; seed++)
            {
                var random = new Random(seed);
                GameState state = UnitDraft.CreateRandomMatch(7, NoResourceRules(),
                    UnitDefinitions.Pool, random);
                var turns = new TurnSystem(state);
                Check(turns.TryStartBattle((PlayerId)(seed % 2)));
                for (int ply = 0; ply < 8 && state.Phase == GamePhase.Battle; ply++)
                {
                    BotPlan plan = new DfsBot(2, 6000, 0).FindTurn(state);
                    foreach (BotAction action in plan.Actions) Check(action.Apply(turns));
                    if (state.Phase == GamePhase.Battle && plan.Actions.Count == 0)
                        Check(turns.TryEndTurn(state.CurrentPlayer.Value));
                }
            }
        });
    }

    // Player-facing text must be Korean: Hangul present, and no Latin words left
    // behind apart from the AP/SP resource tokens the HUD deliberately keeps.
    private static void CheckKorean(string text)
    {
        if (text.Length == 0) return;
        bool hangul = false;
        foreach (char c in text) if (c >= 0xAC00 && c <= 0xD7A3) { hangul = true; break; }
        Check(hangul);
        string stripped = text.Replace("AP", "").Replace("SP", "");
        foreach (char c in stripped)
            Check(!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')));
    }

    private static BattleRules NoResourceRules(int startAP = 4) =>
        new BattleRules(ap: new ActionPointRules(startAP: startAP, useSkillResource: false));

    // Roster padded to four slots with the plain starting units.
    private static UnitDefinition[] Lineup(params UnitDefinition[] chosen)
    {
        var roster = new[] { UnitDefinitions.Warrior, UnitDefinitions.Guardian,
            UnitDefinitions.Ranger, UnitDefinitions.Controller };
        for (int index = 0; index < chosen.Length && index < roster.Length; index++)
            roster[index] = chosen[index];
        return roster;
    }

    // Straight-only movement, so the tiles a move passes over are unambiguous.
    private static TurnSystem StraightMoveBattle()
    {
        var rules = new BattleRules(ap: new ActionPointRules(startAP: 6,
            useSkillResource: false, movePattern: MovePattern.FourWay));
        var turns = new TurnSystem(FixedDeployment.Create(7, rules));
        Check(turns.TryStartBattle(Two));
        return turns;
    }

    private static TurnSystem UnitBattle(UnitDefinition[] lineup, PlayerId first = One, int startAP = 4)
    {
        var turns = new TurnSystem(FixedDeployment.Create(7, NoResourceRules(startAP),
            definitions: lineup));
        Check(turns.TryStartBattle(first));
        return turns;
    }
}
