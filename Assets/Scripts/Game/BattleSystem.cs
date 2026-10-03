using System;
using System.Collections.Generic;

namespace MiniChess
{
    // Complete, validated actions for both human input and future AI simulation.
    public sealed class BattleSystem
    {
        private readonly TurnSystem turns;
        // Reused search grid and route buffer. One instance belongs to one caller,
        // never two threads, and no walk starts while another is resolving.
        private int[] reach;
        private int[] leapReach;
        private readonly List<int> route = new List<int>();
        public GameState State => turns.State;

        public BattleSystem(TurnSystem turns)
        {
            this.turns = turns ?? throw new ArgumentNullException(nameof(turns));
        }

        public bool CanMove(PlayerId player, int index, int x, int y)
        {
            int steps = GetMoveDistance(player, index, x, y);
            return steps > 0 && (long)steps * State.Rules.AP.MoveAPPerTile <= State.GetAP(player);
        }

        // Steps over empty tiles, up to the configured basic-move limit. FourWay walks
        // one straight run and never turns, so a blocked tile simply ends the run.
        // EightWay takes the fewest steps it can, detouring around blockers, which
        // costs extra steps and can push an otherwise in-range tile out of reach.
        // A trap on the way cuts the walk short, so this reports the tiles the unit will
        // really cover, which is what the move costs in AP.
        public int GetMoveDistance(PlayerId player, int index, int x, int y)
            => ResolveMove(player, index, x, y);

        // The work behind GetMoveDistance. Also leaves in route every tile the unit
        // enters, in travel order, so the caller can make things react to being walked
        // over rather than landed on. The last entry is where the unit ends up, which is
        // the requested tile unless a trap stopped it earlier.
        private int ResolveMove(PlayerId player, int index, int x, int y)
        {
            route.Clear();
            if (!turns.CanMove(player, index) || !IsOnBoard(x, y) || State.IsBlocked(x, y))
                return -1;
            UnitState unit = State.GetUnit(player, index);
            ActionPointRules ap = State.Rules.AP;
            // Honours both the per-action limit and whatever the unit has left this turn.
            int limit = turns.GetRemainingMoveDistance(player, index);
            int straight = ap.MoveSteps(unit.X, unit.Y, x, y);
            if (straight < 1 || straight > limit) return -1;
            // A straight-only pattern turns no corners, so the whole run must be clear
            // and there is no alternative route to look for.
            if (ap.MovePattern == MovePattern.FourWay)
            {
                int stepX = Math.Sign(x - unit.X), stepY = Math.Sign(y - unit.Y);
                for (int step = 1; step <= straight; step++)
                {
                    int tileX = unit.X + stepX * step, tileY = unit.Y + stepY * step;
                    if (step < straight && State.IsBlocked(tileX, tileY))
                    { route.Clear(); return -1; }
                    route.Add(Packed(tileX, tileY));
                }
                return StopAtTrap(unit.Owner);
            }
            // One step to an empty neighbour needs no search, and the bot asks for a lot
            // of those.
            if (straight == 1) { route.Add(Packed(x, y)); return 1; }
            // Breadth-first over a window just large enough to hold every legal path.
            int span = limit * 2 + 1;
            if (reach == null || reach.Length < span * span) reach = new int[span * span];
            Array.Clear(reach, 0, span * span);
            int originX = unit.X - limit, originY = unit.Y - limit;
            reach[limit * span + limit] = 1; // Marks "reached in step - 1 moves".
            for (int step = 1; step <= limit; step++)
                for (int cellY = 0; cellY < span; cellY++)
                    for (int cellX = 0; cellX < span; cellX++)
                    {
                        if (reach[cellY * span + cellX] != step) continue;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nextX = cellX + dx, nextY = cellY + dy;
                                if (!ap.IsMoveStep(dx, dy) || nextX < 0 || nextY < 0
                                    || nextX >= span || nextY >= span
                                    || reach[nextY * span + nextX] != 0) continue;
                                int boardX = originX + nextX, boardY = originY + nextY;
                                if (!IsOnBoard(boardX, boardY)
                                    || State.IsBlocked(boardX, boardY)) continue;
                                reach[nextY * span + nextX] = step + 1;
                            }
                    }
            // The destination sits inside the window whenever it is within the limit,
            // which MoveSteps already checked, so this only reads a marked cell.
            int mark = reach[(y - originY) * span + (x - originX)];
            if (mark < 2) return -1;
            TraceRoute(ap, route, span, originX, originY, x, y, mark);
            return StopAtTrap(unit.Owner);
        }

        // An enemy trap ends the walk on its own tile, so everything past it is dropped
        // from the route. Deciding this before any AP is spent keeps the charge honest:
        // a move interrupted after one tile costs one tile.
        private int StopAtTrap(PlayerId walker)
        {
            for (int i = 0; i < route.Count; i++)
            {
                TrapEffect trap = State.GetTrapAt(route[i] % State.BoardSize, route[i] / State.BoardSize);
                if (trap == null || trap.Owner == walker) continue;
                route.RemoveRange(i + 1, route.Count - i - 1);
                break;
            }
            return route.Count;
        }

        // Walks the search marks back from the destination to the unit, then flips the
        // result so the caller reads it in travel order. Ties break in scan order, so
        // the same request always yields the same route.
        private void TraceRoute(ActionPointRules ap, List<int> route, int span,
            int originX, int originY, int x, int y, int mark)
        {
            int cellX = x - originX, cellY = y - originY;
            for (int step = mark; step > 1; step--)
            {
                route.Add(Packed(originX + cellX, originY + cellY));
                if (step == 2) break; // Its predecessor is the unit's own tile.
                bool stepped = false;
                for (int dy = -1; dy <= 1 && !stepped; dy++)
                    for (int dx = -1; dx <= 1 && !stepped; dx++)
                    {
                        int prevX = cellX + dx, prevY = cellY + dy;
                        if (!ap.IsMoveStep(dx, dy) || prevX < 0 || prevY < 0
                            || prevX >= span || prevY >= span
                            || reach[prevY * span + prevX] != step - 1) continue;
                        cellX = prevX; cellY = prevY; stepped = true;
                    }
                if (!stepped) break; // Unreachable: every mark above 1 has a predecessor.
            }
            route.Reverse();
        }

        private int Packed(int x, int y) => y * State.BoardSize + x;

        // True when some chain of single steps of at most maxSteps reaches the target
        // without entering an obstacle. Units are ignored: a leap passes over them, so
        // only walls can shut a destination off. Always true when no obstacle is placed.
        private bool HasWallFreePath(int fromX, int fromY, int toX, int toY, int maxSteps)
        {
            if (State.Obstacles.Count == 0) return true;
            int span = maxSteps * 2 + 1;
            if (leapReach == null || leapReach.Length < span * span) leapReach = new int[span * span];
            Array.Clear(leapReach, 0, span * span);
            int originX = fromX - maxSteps, originY = fromY - maxSteps;
            leapReach[maxSteps * span + maxSteps] = 1;
            for (int step = 1; step <= maxSteps; step++)
                for (int cellY = 0; cellY < span; cellY++)
                    for (int cellX = 0; cellX < span; cellX++)
                    {
                        if (leapReach[cellY * span + cellX] != step) continue;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nextX = cellX + dx, nextY = cellY + dy;
                                if (nextX < 0 || nextY < 0 || nextX >= span || nextY >= span
                                    || leapReach[nextY * span + nextX] != 0) continue;
                                int boardX = originX + nextX, boardY = originY + nextY;
                                if (!IsOnBoard(boardX, boardY) || State.HasObstacleAt(boardX, boardY))
                                    continue;
                                if (boardX == toX && boardY == toY) return true;
                                leapReach[nextY * span + nextX] = step + 1;
                            }
                    }
            return false;
        }

        // Tiles of an already validated straight run, in travel order.
        private void StraightRoute(List<int> tiles, int fromX, int fromY, int toX, int toY, int steps)
        {
            tiles.Clear();
            int stepX = Math.Sign(toX - fromX), stepY = Math.Sign(toY - fromY);
            for (int step = 1; step <= steps; step++)
                tiles.Add(Packed(fromX + stepX * step, fromY + stepY * step));
        }

        public bool TryMove(PlayerId player, int index, int x, int y)
        {
            if (!CanMove(player, index, x, y)) return false;
            int steps = ResolveMove(player, index, x, y);
            if (steps < 0 || !turns.TryConsumeMove(player, index, steps, false)) return false;
            UnitState mover = State.GetUnit(player, index);
            WalkRoute(mover, route);
            if (!FinishAction(player)) turns.NotifyStateChanged();
            mover.NotifyChanged();
            return true;
        }

        // Sends a unit to the end of its route. The route already stops on the first
        // enemy trap, so the last tile is where the unit really ends up, whether it
        // aimed there or was caught on the way.
        private void WalkRoute(UnitState mover, List<int> tiles)
        {
            if (tiles.Count == 0) return;
            int last = tiles[tiles.Count - 1];
            int tileX = last % State.BoardSize, tileY = last / State.BoardSize;
            TrapEffect trap = State.GetTrapAt(tileX, tileY);
            mover.SetPosition(tileX, tileY, false);
            ResolveTileEntry(mover, triggersTraps: true);
            // A trap holds what it catches: no more walking this turn, though the unit
            // may still fight from where it was stopped.
            if (trap != null && trap.Owner != mover.Owner) mover.MovementStoppedThisTurn = true;
        }

        public bool CanAttack(PlayerId player, int index, PlayerId targetPlayer, int targetIndex)
        {
            if (!turns.CanCombat(player, index) || player == targetPlayer) return false;
            UnitState target = State.GetUnit(targetPlayer, targetIndex);
            UnitState attacker = State.GetUnit(player, index);
            return target != null && target.IsAlive && attacker.AttackDamage > 0
                && Distance(attacker.X, attacker.Y, target.X, target.Y) <= GetAttackRange(player, index);
        }

        public int GetAttackRange(PlayerId player, int index)
        {
            UnitState unit = State.GetUnit(player, index);
            if (unit == null) return 0;
            // Aim depends on this unit's voluntary movement, not team movement or skills.
            bool aiming = unit.Definition.AimRangeBonus > 0 && State.Phase == GamePhase.Battle
                && State.CurrentPlayer == player && !unit.HasMovedThisTurn;
            return unit.AttackRange + (aiming ? unit.Definition.AimRangeBonus : 0);
        }

        // Basic-attack damage after this unit's passive is applied.
        public int GetAttackDamage(PlayerId player, int index, PlayerId targetPlayer, int targetIndex)
        {
            UnitState attacker = State.GetUnit(player, index);
            UnitState target = State.GetUnit(targetPlayer, targetIndex);
            if (attacker == null || target == null) return 0;
            int damage = attacker.AttackDamage;
            switch (attacker.Definition.Passive)
            {
                case UnitPassive.IsolationHunt:
                    if (!HasAllyBeside(target)) damage++;
                    break;
                case UnitPassive.Ember:
                    if (State.HasAreaFrom(target.X, target.Y, attacker)) damage++;
                    break;
                case UnitPassive.Impact:
                    if (attacker.LongestMoveThisTurn >= 2) damage++;
                    break;
                case UnitPassive.MarkedPrey:
                    if (target.IsTrapMarkedBy(attacker)) damage++;
                    break;
            }
            return damage;
        }

        // Isolation Hunt asks whether the target has support of its own beside it.
        private bool HasAllyBeside(UnitState target)
        {
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                UnitState ally = State.GetUnit(target.Owner, index);
                if (ally == null || !ally.IsAlive || ally == target) continue;
                if (Distance(ally.X, ally.Y, target.X, target.Y) <= 1) return true;
            }
            return false;
        }

        public int GetSkillCost(PlayerId player, int index) =>
            State.GetUnit(player, index)?.Definition.GetSkillResourceCost(State.Rules) ?? State.Rules.SkillCost;

        public bool TryAttack(PlayerId player, int index, PlayerId targetPlayer, int targetIndex)
        {
            if (!CanAttack(player, index, targetPlayer, targetIndex)) return false;
            UnitState target = State.GetUnit(targetPlayer, targetIndex);
            int damage = GetAttackDamage(player, index, targetPlayer, targetIndex);
            if (!turns.TryConsumeCombat(player, index, notify: false)) return false;
            target.ApplyDamage(damage, false);
            // The capture point's buffs ride on basic attacks, not skills, and only for
            // the side that currently owns it.
            if (State.CaptureOwner == player)
                State.Rules.Capture.Effect.OnBasicAttack(State, State.GetUnit(player, index), target);
            // Notify observers only once HP, occupancy, action costs and victory agree.
            if (!FinishAction(player)) turns.NotifyStateChanged();
            target.NotifyChanged();
            return true;
        }

        private static int Distance(int x1, int y1, int x2, int y2)
        {
            return Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
        }

        private bool IsOnBoard(int x, int y) => x >= 0 && y >= 0 && x < State.BoardSize && y < State.BoardSize;

        public bool CanUseSkill(PlayerId player, int index)
        {
            UnitState unit = State.GetUnit(player, index);
            if (unit == null || unit.Definition.Skill == UnitSkill.None) return false;
            int apCost = GetSkillAPCost(player, index);
            // Movement skills keep the combat action, so they only need AP and an
            // unspent combat action; combat skills consume it outright.
            // A unit a trap stopped stays put, so its movement skills are off too.
            bool affordable = unit.Definition.IsMovementSkill
                ? State.Phase == GamePhase.Battle && State.CurrentPlayer == player && unit.IsAlive
                    && !unit.HasCombatActedThisTurn && !unit.MovementStoppedThisTurn
                    && State.GetAP(player) >= apCost
                : turns.CanCombat(player, index, apCost);
            return affordable
                && (!State.Rules.AP.UseSkillResource || State.GetCost(player) >= GetSkillCost(player, index));
        }

        public int GetSkillAPCost(PlayerId player, int index) =>
            State.GetUnit(player, index)?.Definition.GetSkillAPCost(State.Rules) ?? State.Rules.AP.DefaultSkillAP;

        // Unit-targeted entry point, kept for the original four skills.
        public bool CanSkill(PlayerId player, int index, PlayerId targetPlayer, int targetIndex)
        {
            UnitState target = State.GetUnit(targetPlayer, targetIndex);
            if (target == null || !target.IsAlive) return false;
            return CanSkillAt(player, index, target.X, target.Y);
        }

        public bool TrySkill(PlayerId player, int index, PlayerId targetPlayer, int targetIndex)
        {
            UnitState target = State.GetUnit(targetPlayer, targetIndex);
            if (target == null || !target.IsAlive) return false;
            return TrySkillAt(player, index, target.X, target.Y);
        }

        // Tile-targeted entry point. Swap needs the second tile; everything else ignores it.
        public bool CanSkillAt(PlayerId player, int index, int x, int y, int x2 = -1, int y2 = -1)
        {
            if (!CanUseSkill(player, index) || !IsOnBoard(x, y)) return false;
            UnitState caster = State.GetUnit(player, index);
            UnitDefinition definition = caster.Definition;
            int distance = Distance(caster.X, caster.Y, x, y);
            UnitState target = State.GetUnitAt(x, y);
            switch (definition.Targeting)
            {
                case SkillTargeting.EnemyUnit:
                    if (target == null || target.Owner == player) return false;
                    break;
                case SkillTargeting.AllyUnit:
                    if (target == null || target.Owner != player || target == caster) return false;
                    break;
                case SkillTargeting.EmptyTile:
                    // An obstacle tile is not empty, it is solid.
                    if (State.IsBlocked(x, y)) return false;
                    break;
                case SkillTargeting.AnyTile:
                    if (State.HasObstacleAt(x, y)) return false;
                    break;
                case SkillTargeting.TwoAllies:
                    return CanSwap(player, caster, x, y, x2, y2);
            }
            if (distance < 1 || distance > definition.SkillRange) return false;
            switch (definition.Skill)
            {
                case UnitSkill.Shield:
                    return target.Shield < definition.GetSkillPower(State.Rules);
                case UnitSkill.Knockback:
                    return GetKnockbackDestination(caster, target, out _, out _);
                case UnitSkill.ShadowLeap:
                    // A leap clears units but not obstacles, so it needs a way around
                    // the walls even though it does not walk it.
                    return HasWallFreePath(caster.X, caster.Y, x, y, definition.SkillRange);
                case UnitSkill.Charge:
                    return GetChargeDistance(caster, x, y) > 0;
                case UnitSkill.SetTrap:
                    return State.GetTrapAt(x, y) == null && !State.HasAreaAt(x, y);
                default:
                    return true;
            }
        }

        // True when this tile is a legal first click for the skill's targeting mode.
        // Two-target skills accept any ally in range before the second pick is known.
        public bool CanSkillFirstTarget(PlayerId player, int index, int x, int y)
        {
            if (!CanUseSkill(player, index) || !IsOnBoard(x, y)) return false;
            UnitState caster = State.GetUnit(player, index);
            if (caster.Definition.Targeting != SkillTargeting.TwoAllies)
                return CanSkillAt(player, index, x, y);
            UnitState target = State.GetUnitAt(x, y);
            return target != null && target.Owner == player
                && Distance(caster.X, caster.Y, x, y) <= caster.Definition.SkillRange;
        }

        private bool CanSwap(PlayerId player, UnitState caster, int x, int y, int x2, int y2)
        {
            if (!IsOnBoard(x2, y2) || (x == x2 && y == y2)) return false;
            UnitState first = State.GetUnitAt(x, y);
            UnitState second = State.GetUnitAt(x2, y2);
            if (first == null || second == null || first.Owner != player || second.Owner != player) return false;
            int range = caster.Definition.SkillRange;
            return Distance(caster.X, caster.Y, x, y) <= range
                && Distance(caster.X, caster.Y, x2, y2) <= range;
        }

        private bool GetKnockbackDestination(UnitState caster, UnitState target, out int x, out int y)
        {
            int power = caster.Definition.GetSkillPower(State.Rules);
            x = target.X + Math.Sign(target.X - caster.X) * power;
            y = target.Y + Math.Sign(target.Y - caster.Y) * power;
            return IsOnBoard(x, y) && !State.IsBlocked(x, y);
        }

        // Straight-line dash. Every tile along the way must be empty, and the
        // direction has to be one the move pattern allows.
        public int GetChargeDistance(UnitState caster, int x, int y)
        {
            int dx = Math.Sign(x - caster.X), dy = Math.Sign(y - caster.Y);
            int stepsX = Math.Abs(x - caster.X), stepsY = Math.Abs(y - caster.Y);
            if (stepsX != 0 && stepsY != 0 && stepsX != stepsY) return -1; // Not a straight line.
            if (!State.Rules.AP.IsMoveStep(dx, dy)) return -1;
            int steps = Math.Max(stepsX, stepsY);
            if (steps < 1 || steps > caster.Definition.SkillRange) return -1;
            for (int step = 1; step <= steps; step++)
            {
                int tileX = caster.X + dx * step, tileY = caster.Y + dy * step;
                if (!IsOnBoard(tileX, tileY) || State.IsBlocked(tileX, tileY)) return -1;
            }
            return steps;
        }

        public bool TrySkillAt(PlayerId player, int index, int x, int y, int x2 = -1, int y2 = -1)
        {
            // A blocked push or invalid target is a failed command: spend nothing.
            if (!CanSkillAt(player, index, x, y, x2, y2)) return false;
            UnitState caster = State.GetUnit(player, index);
            UnitDefinition definition = caster.Definition;
            int apCost = GetSkillAPCost(player, index);
            int moveDistance;
            if (definition.Skill == UnitSkill.Charge)
            {
                // Lay the lane out first: a trap in it shortens the dash, and Impact
                // should reward the distance actually covered, not the one intended.
                StraightRoute(route, caster.X, caster.Y, x, y, GetChargeDistance(caster, x, y));
                moveDistance = StopAtTrap(caster.Owner);
            }
            else moveDistance = Distance(caster.X, caster.Y, x, y);
            bool spent = definition.IsMovementSkill
                ? turns.TryConsumeSkillMove(player, index, apCost, moveDistance, false)
                : turns.TryConsumeCombat(player, index, apCost, false);
            if (!spent) return false;
            int cost = State.Rules.AP.UseSkillResource ? GetSkillCost(player, index) : 0;
            if (player == PlayerId.PlayerOne) State.PlayerOneCost -= cost;
            else State.PlayerTwoCost -= cost;

            UnitState target = State.GetUnitAt(x, y);
            switch (definition.Skill)
            {
                case UnitSkill.StrongStrike:
                case UnitSkill.Snipe:
                    target.ApplyDamage(definition.GetSkillPower(State.Rules), false);
                    break;
                case UnitSkill.Shield:
                    // Lasts through the target side's next turn, or until consumed.
                    target.GrantShield(definition.GetSkillPower(State.Rules),
                        State.GetTurnCount(target.Owner) + 1);
                    break;
                case UnitSkill.Knockback:
                    GetKnockbackDestination(caster, target, out int pushX, out int pushY);
                    target.SetPosition(pushX, pushY, false);
                    // A shove is forced movement, so it never trips the victim's own traps.
                    ResolveTileEntry(target, triggersTraps: false);
                    break;
                case UnitSkill.ShadowLeap:
                    caster.SetPosition(x, y, false);
                    // Teleports skip the "entering" check that arms traps.
                    ResolveTileEntry(caster, triggersTraps: false);
                    break;
                case UnitSkill.Charge:
                    // A dash is still a walk, so a trap in the lane catches and holds it.
                    WalkRoute(caster, route);
                    break;
                case UnitSkill.FireZone:
                    PlaceFireZone(caster, x, y);
                    break;
                case UnitSkill.SetTrap:
                    State.AddTrap(new TrapEffect(player, index, x, y, definition.GetSkillPower(State.Rules)));
                    break;
                case UnitSkill.Swap:
                    ApplySwap(caster, x, y, x2, y2);
                    break;
            }
            if (!FinishAction(player)) turns.NotifyStateChanged();
            NotifyAll();
            return true;
        }

        // Centre tile plus its four orthogonal neighbours, clipped to the board.
        private void PlaceFireZone(UnitState caster, int x, int y)
        {
            var xs = new System.Collections.Generic.List<int>(5);
            var ys = new System.Collections.Generic.List<int>(5);
            int[] offsetX = { 0, 1, -1, 0, 0 };
            int[] offsetY = { 0, 0, 0, 1, -1 };
            for (int i = 0; i < offsetX.Length; i++)
            {
                int tileX = x + offsetX[i], tileY = y + offsetY[i];
                // A wall does not catch fire, so the plus shape is clipped around it.
                if (!IsOnBoard(tileX, tileY) || State.HasObstacleAt(tileX, tileY)) continue;
                xs.Add(tileX);
                ys.Add(tileY);
            }
            // Survives the caster's next two own turn starts.
            State.AddArea(new AreaEffect(caster.Owner, caster.UnitIndex, xs.ToArray(), ys.ToArray(),
                State.GetTurnCount(caster.Owner) + 2, caster.Definition.GetSkillPower(State.Rules)));
            // Anyone already standing in the flames takes the hit immediately.
            for (int side = 0; side < 2; side++)
                for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                {
                    UnitState unit = State.GetUnit((PlayerId)side, index);
                    if (unit != null && unit.IsAlive && unit.Owner != caster.Owner)
                        ResolveTileEntry(unit, triggersTraps: false);
                }
        }

        private void ApplySwap(UnitState caster, int x, int y, int x2, int y2)
        {
            UnitState first = State.GetUnitAt(x, y);
            UnitState second = State.GetUnitAt(x2, y2);
            first.SetPosition(x2, y2, false);
            second.SetPosition(x, y, false);
            if (caster.Definition.Passive == UnitPassive.SpatialEcho)
            {
                first.EchoCharges = Math.Max(first.EchoCharges, 1);
                second.EchoCharges = Math.Max(second.EchoCharges, 1);
            }
            // Forced repositioning, so neither unit's own move/combat state changes.
            ResolveTileEntry(first, triggersTraps: false);
            ResolveTileEntry(second, triggersTraps: false);
        }

        // Fire burns and traps snap when a unit arrives on a tile.
        // Sets off an enemy trap on the tile this unit currently stands on, if any.
        private void SpringTrap(UnitState unit)
        {
            if (!unit.IsAlive) return;
            TrapEffect trap = State.GetTrapAt(unit.X, unit.Y);
            if (trap == null || trap.Owner == unit.Owner) return;
            State.RemoveTrap(trap);
            // The mark lets the trapper hit harder through their next turn.
            unit.MarkTrapped(trap.Owner, trap.OwnerIndex, State.GetTurnCount(trap.Owner) + 1);
            unit.ApplyDamage(trap.Damage, false);
        }

        private void ResolveTileEntry(UnitState unit, bool triggersTraps)
        {
            if (!unit.IsAlive) return;
            if (triggersTraps) SpringTrap(unit);
            if (!unit.IsAlive) return;
            foreach (AreaEffect area in State.Areas)
            {
                if (area.Owner == unit.Owner || !area.Covers(unit.X, unit.Y)) continue;
                if (!area.TryBurn(unit, State.TurnNumber)) continue;
                unit.ApplyDamage(area.Damage, false);
                if (!unit.IsAlive) return;
            }
        }

        // Returns true when the match ended, which already published the change.
        private bool FinishAction(PlayerId actor) => turns.CheckElimination(actor);

        private void NotifyAll()
        {
            for (int side = 0; side < 2; side++)
                for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                    State.GetUnit((PlayerId)side, index)?.NotifyChanged();
        }
    }
}
