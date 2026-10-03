using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text;

namespace MiniChess
{
    public enum BotActionKind { Move, Attack, Skill, SkillAt, EndTurn }

    public readonly struct BotAction
    {
        public BotActionKind Kind { get; }
        public PlayerId Player { get; }
        public int UnitIndex { get; }
        public int X { get; }
        public int Y { get; }
        // Second tile, used only by two-target skills such as Swap.
        public int X2 { get; }
        public int Y2 { get; }
        public PlayerId TargetPlayer { get; }
        public int TargetIndex { get; }

        public BotAction(BotActionKind kind, PlayerId player, int unitIndex = 0,
            int x = 0, int y = 0, PlayerId targetPlayer = PlayerId.PlayerOne, int targetIndex = 0,
            int x2 = -1, int y2 = -1)
        {
            Kind = kind; Player = player; UnitIndex = unitIndex;
            X = x; Y = y; TargetPlayer = targetPlayer; TargetIndex = targetIndex;
            X2 = x2; Y2 = y2;
        }

        public bool Apply(TurnSystem turns)
        {
            var battle = new BattleSystem(turns);
            switch (Kind)
            {
                case BotActionKind.Move: return battle.TryMove(Player, UnitIndex, X, Y);
                case BotActionKind.Attack: return battle.TryAttack(Player, UnitIndex, TargetPlayer, TargetIndex);
                case BotActionKind.Skill: return battle.TrySkill(Player, UnitIndex, TargetPlayer, TargetIndex);
                case BotActionKind.SkillAt: return battle.TrySkillAt(Player, UnitIndex, X, Y, X2, Y2);
                case BotActionKind.EndTurn: return turns.TryEndTurn(Player);
                default: return false;
            }
        }
    }

    public sealed class BotPlan
    {
        public IReadOnlyList<BotAction> Actions { get; }
        public int CompletedDepth { get; }
        public int VisitedNodes { get; }
        internal BotPlan(BotAction[] actions, int depth, int nodes)
        { Actions = Array.AsReadOnly(actions); CompletedDepth = depth; VisitedNodes = nodes; }
    }

    // A search instance is local to one worker task. Never accesses Unity objects.
    public sealed class DfsBot
    {
        private sealed class LimitReached : Exception { }
        private sealed class Candidate
        {
            public GameState State;
            public BotAction[] Actions;
            public int Score;
        }
        private readonly int depthLimit;
        private readonly int nodeLimit;
        private readonly int milliseconds;
        private readonly Stopwatch clock = new Stopwatch();
        private CancellationToken cancellation;
        private PlayerId perspective;
        private int nodes;

        public DfsBot(int depth = 2, int maxNodes = 20000, int timeLimitMs = 300)
        {
            if (depth < 1 || depth > 4) throw new ArgumentOutOfRangeException(nameof(depth));
            if (maxNodes < 1) throw new ArgumentOutOfRangeException(nameof(maxNodes));
            if (timeLimitMs < 0) throw new ArgumentOutOfRangeException(nameof(timeLimitMs));
            depthLimit = depth; nodeLimit = maxNodes; milliseconds = timeLimitMs;
        }

        public BotPlan FindTurn(GameState source, CancellationToken token = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            token.ThrowIfCancellationRequested();
            if (source.Phase != GamePhase.Battle || !source.CurrentPlayer.HasValue)
                return new BotPlan(Array.Empty<BotAction>(), 0, 0);
            perspective = source.CurrentPlayer.Value;
            cancellation = token; nodes = 0; clock.Restart();
            var best = new[] { new BotAction(BotActionKind.EndTurn, perspective) };
            int completedDepth = 0;
            int bestScore = int.MinValue;
            try
            {
                var roots = new List<Candidate>();
                foreach (Candidate candidate in GenerateTurns(source.Clone()))
                {
                    roots.Add(candidate);
                    if (candidate.Score > bestScore)
                    { bestScore = candidate.Score; best = candidate.Actions; }
                    // A terminal win is proven without exploring the opponent's reply.
                    if (candidate.State.Winner == perspective)
                        return new BotPlan(best, 1, nodes);
                }
                roots.Sort((a, b) => b.Score.CompareTo(a.Score));
                TrimCandidates(roots, true);
                completedDepth = 1;
                // Keep the last fully searched depth when the budget interrupts a deeper one.
                for (int depth = 2; depth <= depthLimit; depth++)
                {
                    BotAction[] iterationBest = best;
                    int score = int.MinValue;
                    int alpha = int.MinValue + 1;
                    foreach (Candidate root in roots)
                    {
                        CheckLimit();
                        int value = Minimax(root.State, depth - 1, alpha, int.MaxValue);
                        if (value > score) { score = value; iterationBest = root.Actions; }
                        alpha = Math.Max(alpha, score);
                    }
                    best = iterationBest;
                    completedDepth = depth;
                }
            }
            catch (LimitReached) { }
            finally { clock.Stop(); }
            return new BotPlan(best, completedDepth, nodes);
        }

        private int Minimax(GameState state, int depth, int alpha, int beta)
        {
            CheckLimit();
            if (depth == 0 || state.Phase == GamePhase.Finished) return Evaluate(state);
            bool maximize = state.CurrentPlayer == perspective;
            var candidates = new List<Candidate>(GenerateTurns(state));
            candidates.Sort((a, b) => maximize ? b.Score.CompareTo(a.Score) : a.Score.CompareTo(b.Score));
            TrimCandidates(candidates, maximize);
            int best = maximize ? int.MinValue : int.MaxValue;
            foreach (Candidate candidate in candidates)
            {
                int value = Minimax(candidate.State, depth - 1, alpha, beta);
                if (maximize) { best = Math.Max(best, value); alpha = Math.Max(alpha, best); }
                else { best = Math.Min(best, value); beta = Math.Min(beta, best); }
                if (beta <= alpha) break;
            }
            return best;
        }

        // AP expands the tree substantially: share a per-turn generation budget across
        // first actions, then DFS each branch. Always retain a legal early End Turn.
        private IEnumerable<Candidate> GenerateTurns(GameState state)
        {
            var seen = new HashSet<string>();
            var path = new List<BotAction>();
            foreach (Candidate candidate in CompleteTurns(state, path, seen, nodes + 1)) yield return candidate;
            var firstActions = new List<BotAction>(LegalActions(state));
            int branchBudget = Math.Max(16, 1200 / Math.Max(1, firstActions.Count));
            foreach (BotAction action in firstActions)
            {
                GameState next = CopyAndApply(state, action);
                path.Add(action);
                foreach (Candidate candidate in CompleteTurns(next, path, seen, nodes + branchBudget)) yield return candidate;
                path.Clear();
            }
        }

        private IEnumerable<Candidate> CompleteTurns(GameState state, List<BotAction> path,
            HashSet<string> seen, int branchLimit)
        {
            CheckLimit();
            if (!seen.Add(StateKey(state))) yield break;
            if (state.Phase == GamePhase.Finished)
            {
                yield return new Candidate { State = state, Actions = path.ToArray(), Score = Evaluate(state) };
                yield break;
            }
            var end = new BotAction(BotActionKind.EndTurn, state.CurrentPlayer.Value);
            GameState ended = CopyAndApply(state, end);
            path.Add(end);
            yield return new Candidate { State = ended, Actions = path.ToArray(), Score = Evaluate(ended) };
            path.RemoveAt(path.Count - 1);
            foreach (BotAction action in LegalActions(state))
            {
                if (nodes >= branchLimit) yield break;
                GameState next = CopyAndApply(state, action);
                path.Add(action);
                foreach (Candidate candidate in CompleteTurns(next, path, seen, branchLimit)) yield return candidate;
                path.RemoveAt(path.Count - 1);
            }
        }

        private static void TrimCandidates(List<Candidate> candidates, bool maximize)
        {
            candidates.Sort((a, b) => maximize ? b.Score.CompareTo(a.Score) : a.Score.CompareTo(b.Score));
            if (candidates.Count <= 48) return;
            Candidate pass = candidates.Find(c => c.Actions.Length == 1 && c.Actions[0].Kind == BotActionKind.EndTurn);
            candidates.RemoveRange(48, candidates.Count - 48);
            if (pass != null && !candidates.Contains(pass)) candidates[47] = pass;
        }

        private static string StateKey(GameState state)
        {
            var key = new StringBuilder();
            key.Append((int)state.Phase).Append('/').Append(state.CurrentPlayer).Append('/').Append(state.Winner);
            // Capture progress is position, not decoration: the same board one round
            // further into a hold is a different node. The tile itself is fixed for the
            // whole match, so it needs no place in the key.
            if (state.HasCapturePoint)
                key.Append('/').Append(state.CaptureHolder).Append(state.CaptureHeldRounds)
                    .Append(',').Append(state.CaptureOwner);
            for (int side = 0; side < 2; side++)
            {
                PlayerId player = (PlayerId)side;
                key.Append('|').Append(state.GetAP(player)).Append(',').Append(state.GetCost(player))
                    .Append(',').Append(state.GetTurnCount(player));
                for (int i = 0; i < GameState.UnitsPerPlayer; i++)
                {
                    UnitState unit = state.GetUnit(player, i);
                    if (unit == null) { key.Append(";null"); continue; }
                    key.Append(';').Append(unit.X).Append(',').Append(unit.Y).Append(',').Append(unit.HP)
                        .Append(',').Append(unit.Shield).Append(',').Append(unit.ShieldExpiresAfterOwnerTurn)
                        .Append(',').Append(unit.HasMovedThisTurn).Append(',').Append(unit.HasCombatActedThisTurn)
                        .Append(',').Append(unit.MoveTilesThisTurn).Append(',').Append(unit.LongestMoveThisTurn)
                        .Append(',').Append(unit.MovementStoppedThisTurn)
                        .Append(',').Append(unit.EchoCharges).Append(',').Append(unit.TrapMarkOwnerIndex)
                        // Pending scorch is future HP, so it separates nodes too.
                        .Append(',').Append(unit.AttackBurnDamage).Append(',').Append(unit.AttackBurnAtOwnTurn);
                }
            }
            // Board effects are part of the position: two otherwise identical states
            // with different fire or traps must not collapse into one node.
            foreach (AreaEffect area in state.Areas)
            {
                key.Append("|f").Append((int)area.Owner).Append(',').Append(area.CasterIndex)
                    .Append(',').Append(area.ExpiresAtOwnerTurn);
                for (int i = 0; i < area.TileCount; i++)
                    key.Append(':').Append(area.GetTileX(i)).Append('.').Append(area.GetTileY(i));
            }
            foreach (TrapEffect trap in state.Traps)
                key.Append("|t").Append((int)trap.Owner).Append(',').Append(trap.X).Append(',').Append(trap.Y);
            return key.ToString();
        }
        private GameState CopyAndApply(GameState state, BotAction action)
        {
            CheckLimit();
            nodes++;
            GameState copy = state.Clone();
            if (!action.Apply(new TurnSystem(copy))) throw new InvalidOperationException("Generated an illegal bot action.");
            return copy;
        }

        private IEnumerable<BotAction> LegalActions(GameState state)
        {
            var turns = new TurnSystem(state);
            var battle = new BattleSystem(turns);
            PlayerId player = state.CurrentPlayer.Value;
            // Combat first makes immediately useful candidates available under tight budgets.
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                CheckLimit();
                UnitState actor = state.GetUnit(player, index);
                if (actor == null || !actor.IsAlive) continue;
                for (int targetPlayer = 0; targetPlayer < 2; targetPlayer++)
                    for (int target = 0; target < GameState.UnitsPerPlayer; target++)
                    {
                        if (battle.CanAttack(player, index, (PlayerId)targetPlayer, target))
                            yield return new BotAction(BotActionKind.Attack, player, index,
                                targetPlayer: (PlayerId)targetPlayer, targetIndex: target);
                    }
                foreach (BotAction skill in SkillActions(state, battle, player, index, actor))
                    yield return skill;
            }
            // The scan window follows the configured move limit, so raising it in the
            // AP rules asset keeps generated candidates and legal moves in agreement.
            int radius = state.Rules.AP.MaxMoveDistance;
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                UnitState unit = state.GetUnit(player, index);
                if (unit == null || !turns.CanMove(player, index)) continue;
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                        if (battle.CanMove(player, index, unit.X + dx, unit.Y + dy))
                            yield return new BotAction(BotActionKind.Move, player, index, unit.X + dx, unit.Y + dy);
            }
        }

        // Skill candidates depend on what the skill points at: a unit, a tile inside
        // its range, or a pair of allies. The scan is bounded by the skill's range.
        private IEnumerable<BotAction> SkillActions(GameState state, BattleSystem battle,
            PlayerId player, int index, UnitState actor)
        {
            UnitDefinition definition = actor.Definition;
            if (definition.Skill == UnitSkill.None || !battle.CanUseSkill(player, index)) yield break;
            SkillTargeting targeting = definition.Targeting;
            if (targeting == SkillTargeting.EnemyUnit || targeting == SkillTargeting.AllyUnit)
            {
                for (int targetPlayer = 0; targetPlayer < 2; targetPlayer++)
                    for (int target = 0; target < GameState.UnitsPerPlayer; target++)
                        if (battle.CanSkill(player, index, (PlayerId)targetPlayer, target))
                            yield return new BotAction(BotActionKind.Skill, player, index,
                                targetPlayer: (PlayerId)targetPlayer, targetIndex: target);
                yield break;
            }
            if (targeting == SkillTargeting.TwoAllies)
            {
                for (int first = 0; first < GameState.UnitsPerPlayer; first++)
                    for (int second = first + 1; second < GameState.UnitsPerPlayer; second++)
                    {
                        UnitState a = state.GetUnit(player, first), b = state.GetUnit(player, second);
                        if (a == null || b == null || !a.IsAlive || !b.IsAlive) continue;
                        if (battle.CanSkillAt(player, index, a.X, a.Y, b.X, b.Y))
                            yield return new BotAction(BotActionKind.SkillAt, player, index,
                                a.X, a.Y, x2: b.X, y2: b.Y);
                    }
                yield break;
            }
            int range = definition.SkillRange;
            for (int dy = -range; dy <= range; dy++)
                for (int dx = -range; dx <= range; dx++)
                {
                    CheckLimit();
                    int tileX = actor.X + dx, tileY = actor.Y + dy;
                    if (battle.CanSkillAt(player, index, tileX, tileY))
                        yield return new BotAction(BotActionKind.SkillAt, player, index, tileX, tileY);
                }
        }

        private void CheckLimit()
        {
            cancellation.ThrowIfCancellationRequested();
            if (nodes >= nodeLimit || (milliseconds > 0 && clock.ElapsedMilliseconds >= milliseconds))
                throw new LimitReached();
        }

        private int Evaluate(GameState state)
        {
            if (state.Phase == GamePhase.Finished) return state.Winner == perspective ? 1000000 : -1000000;
            int score = 0;
            for (int side = 0; side < 2; side++)
            {
                PlayerId player = (PlayerId)side;
                PlayerId opponent = side == 0 ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
                int subtotal = (state.Rules.AP.UseSkillResource ? state.GetCost(player) * 3 : 0)
                    + Math.Min(state.GetAP(player), Math.Max(0, state.Rules.AP.MaxAP - state.Rules.AP.RecoveryAP)) * 6;
                for (int i = 0; i < GameState.UnitsPerPlayer; i++)
                {
                    UnitState unit = state.GetUnit(player, i);
                    if (unit == null || !unit.IsAlive) continue;
                    // Every unit is a win condition now, so a surviving body is worth
                    // far more than the leader bonus used to be worth on one of them.
                    subtotal += 140 + unit.HP * 30 + unit.Shield * 12 + unit.EchoCharges * 4;
                    // Scorch already inflicted is HP this unit is going to lose.
                    subtotal -= unit.AttackBurnDamage * 22;
                    // Standing in enemy fire is a liability the search should feel.
                    if (state.HasAreaAt(unit.X, unit.Y) && !state.HasAreaAt(unit.X, unit.Y, player))
                        subtotal -= 20;
                    int closest = state.BoardSize;
                    for (int j = 0; j < GameState.UnitsPerPlayer; j++)
                    {
                        UnitState enemy = state.GetUnit(opponent, j);
                        if (enemy == null || !enemy.IsAlive) continue;
                        int distance = Math.Max(Math.Abs(unit.X - enemy.X), Math.Abs(unit.Y - enemy.Y));
                        closest = Math.Min(closest, distance);
                        // Positional estimate; exact legal replies are explored by Minimax.
                        if (distance <= unit.AttackRange) subtotal += enemy.HP <= 2 ? 18 : 5;
                    }
                    subtotal += (state.BoardSize - closest) * 3;
                }
                // Board control the side currently owns, valued modestly.
                foreach (AreaEffect area in state.Areas)
                    if (area.Owner == player) subtotal += area.TileCount * 3;
                foreach (TrapEffect trap in state.Traps)
                    if (trap.Owner == player) subtotal += 8;
                // The capture point, valued as roughly half a body. Deliberately modest:
                // what the buffs actually do already shows up in the terms above — the
                // standing shield in unit.Shield, the scorch in the burn penalty — so a
                // large number here would count the same advantage twice. Progress
                // toward a capture is worth less than holding one.
                if (state.HasCapturePoint)
                {
                    if (state.CaptureOwner == player) subtotal += 70;
                    else if (state.CaptureHolder == player) subtotal += state.CaptureHeldRounds * 16;
                    // Standing on a contested point is worth a step of its own, or the
                    // search never starts the hold that the rounds above pay for.
                    if (state.GetUnitAt(state.CaptureX, state.CaptureY)?.Owner == player) subtotal += 24;
                }
                score += player == perspective ? subtotal : -subtotal;
            }
            return score;
        }
    }
}

