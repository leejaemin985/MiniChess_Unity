using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Core.Combat;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;
using MiniChess.Core.Effects;
using MiniChess.Core.Effects.Scheduled;
using MiniChess.Core.Effects.Zones;
using MiniChess.Core.Skills;
using MiniChess.Core.Skills.Conditions;
using MiniChess.Core.Snapshots;
using MiniChess.Core.State;
using MiniChess.Core.Statuses.Library;
using MiniChess.Core.Turns;

namespace MiniChess.Bot.Rules
{
    /// <summary>
    /// 한 번의 결정에 쓰는 판 분석(결정마다 새로 만든다).
    /// 유닛 가치, 피해 가치, 칸별 위협도(다음 상대 턴 예상 피해), 칸 효과의 이득/손해를 계산한다.
    /// </summary>
    internal class BoardAnalysis
    {
        private readonly double[,] _danger;
        private readonly Dictionary<int, double> _threat = new Dictionary<int, double>();

        /// <summary>사거리별 "적을 칠 수 있는 칸까지 이동 칸 수" 지도(필요할 때 만든다).</summary>
        private readonly Dictionary<int, int[,]> _engageSteps = new Dictionary<int, int[,]>();

        public GameController Game { get; }
        public GameState State { get; }
        public BotWeights Weights { get; }
        public Team Team { get; }
        public Team Enemy { get; }
        public List<Unit> Allies { get; }
        public List<Unit> Enemies { get; }

        /// <param name="stalled">교착 상태면 형세와 무관하게 신중함을 CautionStalled 이하로 낮춘다.</param>
        /// <param name="caution">
        /// 정해 둔 신중함. 탐색 봇이 여러 판을 비교할 때 판마다 형세가 달라 배수가 바뀌면 점수를 비교할 수 없으므로,
        /// 탐색을 시작한 판의 값을 넘긴다. null 이면 이 판의 형세로 정한다.
        /// </param>
        public BoardAnalysis(GameController game, Team team, BotWeights weights, bool stalled = false, double? caution = null)
        {
            Game = game;
            State = game.State;
            Weights = weights;
            Team = team;
            Enemy = TurnSystem.GetOpponent(team);
            Allies = State.GetPlayer(team).AliveUnits.Where(unit => unit.IsOnBoard).ToList();
            Enemies = State.GetPlayer(Enemy).AliveUnits.Where(unit => unit.IsOnBoard).ToList();

            foreach (Unit unit in Allies.Concat(Enemies))
                _threat[unit.Id] = ComputeThreat(unit);

            _danger = new double[State.Board.Width, State.Board.Height];
            for (int x = 0; x < State.Board.Width; x++)
            for (int y = 0; y < State.Board.Height; y++)
                _danger[x, y] = double.NaN;
            Caution = caution ?? (stalled ? Math.Min(ComputeCaution(), weights.CautionStalled) : ComputeCaution());
        }

        /// <summary>
        /// 위협도 감점 배수. 형세(턴 제한 판정 기준)가 앞서면 1, 같거나 뒤지면 낮춰 공격적으로 움직인다.
        /// 교착이면 CautionStalled 이하.
        /// </summary>
        public double Caution { get; }

        /// <summary>턴 제한 판정과 같은 기준(생존 수 → 남은 체력 합, 소환물 제외)으로 형세를 본다.</summary>
        private double ComputeCaution()
        {
            List<Unit> mine = Allies.Where(unit => !unit.IsSummon).ToList();
            List<Unit> theirs = Enemies.Where(unit => !unit.IsSummon).ToList();

            int compare = mine.Count.CompareTo(theirs.Count);
            if (compare == 0)
            {
                int hpDiff = mine.Sum(unit => unit.Stats.CurrentHp) - theirs.Sum(unit => unit.Stats.CurrentHp);
                compare = Math.Abs(hpDiff) < Weights.EvenHpMargin ? 0 : Math.Sign(hpDiff);
            }

            if (compare > 0)
                return 1;
            return compare == 0 ? Weights.CautionEven : Weights.CautionBehind;
        }

        #region Units

        public int Ap => State.GetPlayer(Team).Ap.Current;
        public int AttackCost => State.Rules.ActionCost.BasicAttackCost;
        public int MoveCostPerCell => State.Rules.ActionCost.MoveCostPerCell;

        public SkillDefinition FindSkill(string skillId) => State.Skills.Find(skillId);

        /// <summary>유닛이 한 턴에 줄 수 있는 피해(기본 공격과 피해 스킬을 한 번씩, 최대 AP 안에서). 유닛 자체의 위험성이라 지금 남은 AP 와는 무관하다.</summary>
        public double Threat(Unit unit)
        {
            return _threat.TryGetValue(unit.Id, out double threat) ? threat : ComputeThreat(unit);
        }

        /// <summary>처치했을 때의 가치.</summary>
        public double KillValue(Unit unit)
        {
            return unit.IsSummon ? Weights.SummonKillBonus : Weights.KillBonus + Weights.KillThreatFactor * Threat(unit);
        }

        public static int EffectiveHp(Unit unit) => unit.Stats.CurrentHp + unit.Stats.Shield;

        /// <summary>팀의 소환물 아닌 살아 있는 유닛의 체력 + 보호막 합. 교착 판정에 쓴다.</summary>
        public static int TeamEffectiveHp(GameState state, Team team)
        {
            return state.GetPlayer(team).AliveUnits.Where(unit => !unit.IsSummon).Sum(EffectiveHp);
        }

        public static bool IsRooted(Unit unit) => unit.FindStatus(StatusLibrary.RootId) != null;
        public static bool IsInstalled(Unit unit) => unit.FindStatus(StatusLibrary.InstalledId) != null;
        public static bool IsStunned(Unit unit) => unit.FindStatus(StatusLibrary.StunnedId) != null;

        /// <summary>이번 턴에 아직 쓰지 않은 기본 공격이나 스킬이 남아 있는지.</summary>
        public static bool CanStillFight(Unit unit) => unit.HasCombatActionLeft;

        /// <summary>used 를 쓴 뒤에도 이번 턴에 기본 공격이나 다른 스킬이 남는지(행동 종료 스킬이면 false).</summary>
        public static bool CanFightAfter(Unit unit, SkillDefinition used)
        {
            if (used.EndsCasterActions || unit.TurnState.ActionsEnded)
                return false;

            return !unit.TurnState.BasicAttackUsed
                || unit.Stats.Base.SkillIds.Any(id => id != used.Id && !unit.TurnState.HasUsedSkill(id));
        }

        public IEnumerable<SkillDefinition> SkillsOf(Unit unit) => SkillFactsOf(unit).Select(facts => facts.Skill);

        /// <summary>싸우기 좋은 적과의 거리: 기본 공격 사거리와 피해 스킬 사거리 중 큰 값.</summary>
        public int PreferredRange(Unit unit)
        {
            int skillRange = 0;
            foreach (SkillFacts facts in SkillFactsOf(unit))
            {
                if (facts.EnemyDamage > 0)
                    skillRange = Math.Max(skillRange, facts.Reach);
            }

            return Math.Max(1, Math.Max(unit.Stats.AttackRange, skillRange - 1));
        }

        /// <summary>
        /// 위협도 캐시(스레드마다). 위협도는 규칙, 스킬, 기본 능력치, 공격력, 설치 여부, 최대 AP 만으로 정해진다.
        /// 판 평가마다 모든 유닛의 위협도를 다시 셈하던 비용을 없앤다.
        /// </summary>
        [ThreadStatic] private static Dictionary<(object Rules, SkillCatalog Catalog, IReadOnlyUnitBaseStats Base, int Attack, bool Installed, int MaxAp), double> _threatCache;

        private const int ThreatCacheLimit = 4096;

        private double ComputeThreat(Unit unit)
        {
            var key = ((object)State.Rules, State.Skills, unit.Stats.Base, unit.Stats.Attack, IsInstalled(unit), State.GetPlayer(unit.Team).Ap.Max);
            var cache = _threatCache ?? (_threatCache = new Dictionary<(object, SkillCatalog, IReadOnlyUnitBaseStats, int, bool, int), double>());
            if (cache.TryGetValue(key, out double threat))
                return threat;

            threat = ComputeThreatUncached(unit);
            if (cache.Count >= ThreatCacheLimit)
                cache.Clear();
            cache[key] = threat;
            return threat;
        }

        private double ComputeThreatUncached(Unit unit)
        {
            var hits = new List<(double Damage, int Cost)>();
            if (!IsInstalled(unit))
                hits.Add((unit.Stats.Attack, AttackCost));
            foreach (SkillFacts facts in SkillFactsOf(unit))
            {
                if (facts.EnemyDamage > 0)
                    hits.Add((facts.EnemyDamage, facts.Skill.ApCost ?? 0));
            }

            return BestWithinBudget(hits, State.GetPlayer(unit.Team).Ap.Max);
        }

        /// <summary>스킬 하나에서 판과 무관하게 정해지는 추정값(스킬 정의는 경기 중 바뀌지 않는다).</summary>
        private sealed class SkillFacts
        {
            public SkillDefinition Skill;
            public double EnemyDamage;
            public int Reach;
            public int SelfMoveReach;
            public CanMoveCondition[] MoveConditions;
        }

        /// <summary>
        /// 유닛 기본 능력치(복제한 판끼리 같은 객체)별 스킬 추정값 목록(스레드마다 캐시). SkillIds 순서이고 카탈로그에 없는 스킬은 뺀다.
        /// 판 평가마다 스킬을 찾고 효과를 훑던 비용을 없앤다.
        /// </summary>
        [ThreadStatic] private static Dictionary<(SkillCatalog Catalog, IReadOnlyUnitBaseStats Base), SkillFacts[]> _skillFacts;

        private const int SkillFactsCacheLimit = 1024;

        private SkillFacts[] SkillFactsOf(Unit unit)
        {
            Dictionary<(SkillCatalog, IReadOnlyUnitBaseStats), SkillFacts[]> cache =
                _skillFacts ?? (_skillFacts = new Dictionary<(SkillCatalog, IReadOnlyUnitBaseStats), SkillFacts[]>());
            var key = (State.Skills, unit.Stats.Base);
            if (cache.TryGetValue(key, out SkillFacts[] facts))
                return facts;

            facts = unit.Stats.Base.SkillIds.Select(FindSkill).Where(skill => skill != null).Select(skill => new SkillFacts
            {
                Skill = skill,
                EnemyDamage = SkillEstimates.EnemyDamage(skill),
                Reach = SkillEstimates.Reach(skill),
                SelfMoveReach = SkillEstimates.SelfMoveReach(skill),
                MoveConditions = skill.Conditions.OfType<CanMoveCondition>().ToArray(),
            }).ToArray();

            if (cache.Count >= SkillFactsCacheLimit)
                cache.Clear();
            cache[key] = facts;
            return facts;
        }

        /// <summary>
        /// 팀이 이번(지금 그 팀 차례면) 또는 다음 자기 턴에 쓸 수 있는 AP. 차례가 아니면 지금 AP + 회복(최대치까지).
        /// 탐색 봇은 내 턴을 끝낸 뒤(상대 차례, 상대 AP 회복 후)의 판도 평가하므로 차례를 본다.
        /// </summary>
        public int ApNextTurn(Team team)
        {
            PlayerState player = State.GetPlayer(team);
            if (State.CurrentTeam == team)
                return player.Ap.Current;
            return Math.Min(player.Ap.Max, player.Ap.Current + State.Rules.Ap.TurnRecoveryAp);
        }

        /// <summary>각 공격을 한 번씩만 쓸 때 AP budget 안에서 낼 수 있는 가장 큰 피해 합(공격 수가 적어 모든 조합을 본다).</summary>
        private static double BestWithinBudget(IReadOnlyList<(double Damage, int Cost)> hits, int budget)
        {
            double best = 0;
            for (int mask = 1; mask < 1 << hits.Count; mask++)
            {
                double damage = 0;
                int cost = 0;
                for (int i = 0; i < hits.Count; i++)
                {
                    if ((mask & (1 << i)) == 0)
                        continue;
                    damage += hits[i].Damage;
                    cost += hits[i].Cost;
                }

                if (cost <= budget)
                    best = Math.Max(best, damage);
            }

            return best;
        }

        #endregion

        #region Damage

        /// <summary>
        /// target 에게 amount 피해를 주는 가치. 호위(직접 피해 대신 받기), 저주 표식 추가 피해, 보호막, 처치를 반영한다.
        /// 아군이 맞으면 음수. dealtBefore 는 같은 턴에 이 대상에게 먼저 줄 피해로, 그 뒤에 더하는 몫의 가치만 센다(처치 보너스는 한 번).
        /// </summary>
        public double DamageValue(Unit attacker, Unit target, double amount, DamageType type, double dealtBefore)
        {
            return dealtBefore <= 0
                ? DamageValue(attacker, target, amount, type)
                : DamageValue(attacker, target, dealtBefore + amount, type) - DamageValue(attacker, target, dealtBefore, type);
        }

        public double DamageValue(Unit attacker, Unit target, double amount, DamageType type)
        {
            if (amount <= 0 || !target.IsOnBoard)
                return 0;

            if (type == DamageType.Direct)
            {
                CurseMarkBehavior mark = SkillEstimates.FindBehavior<CurseMarkBehavior>(target, StatusLibrary.CurseMarkId);
                Unit markSource = target.FindStatus(StatusLibrary.CurseMarkId)?.Source;
                if (mark != null && markSource != null && markSource.SummonOwner == attacker)
                    amount += mark.BonusDamage;

                target = GuardianOf(target) ?? target;
            }

            int shield = target.Stats.Shield;
            int hp = target.Stats.CurrentHp;
            double toShield = Math.Min(shield, amount);
            double toHp = Math.Min(hp, amount - toShield);

            double value = toHp + toShield * Weights.ShieldDamageRatio;
            if (toHp >= hp)
                value += KillValue(target);

            if (State.Capture.Tile.HasValue && target.Position == State.Capture.Tile && State.Capture.GetProgress(target.Team) > 0)
                value += Weights.CaptureDenial;

            return target.Team == Team ? -value : value;
        }

        /// <summary>대상의 직접 피해를 지금 대신 받을 호위자. 없거나 거리가 멀면 null.</summary>
        public static Unit GuardianOf(Unit target)
        {
            var guard = target.FindStatus(StatusLibrary.GuardId);
            if (guard == null || !(guard.Definition.Behavior is GuardBehavior behavior))
                return null;

            Unit guardian = guard.Source;
            if (guardian == null || guardian == target || !guardian.IsOnBoard)
                return null;

            return RangeCalculator.GetDistance(guardian.Position.Value, target.Position.Value) <= behavior.MaxDistance ? guardian : null;
        }

        /// <summary>기본 공격 피해(차징 추가 피해 포함).</summary>
        public static int BasicAttackDamage(Unit attacker)
        {
            ChargedBehavior charged = SkillEstimates.FindBehavior<ChargedBehavior>(attacker, StatusLibrary.ChargedId);
            return attacker.Stats.Attack + (charged?.BonusDamage ?? 0);
        }

        #endregion

        #region Cells

        /// <summary>내 유닛이 이 칸에서 상대 턴을 맞으면 받을 것으로 예상되는 피해. 처음 물을 때 계산해 둔다(판 평가는 아군 칸만 묻는다).</summary>
        public double DangerAt(Position position)
        {
            double cached = _danger[position.X, position.Y];
            if (double.IsNaN(cached))
            {
                cached = ComputeDanger(position);
                _danger[position.X, position.Y] = cached;
            }

            return cached;
        }

        /// <summary>가장 가까운 적(소환물이 아닌 적 우선)까지의 거리. 적이 없으면 0.</summary>
        public int NearestEnemyDistance(Position position)
        {
            return EngageTargets().Select(enemy => RangeCalculator.GetDistance(position, enemy.Position.Value)).DefaultIfEmpty(0).Min();
        }

        /// <summary>
        /// 이 칸에서 사거리 range 로 적을 칠 수 있는 칸까지 상하좌우 이동 칸 수(벽/장애물은 돌아감, 유닛은 무시).
        /// 이미 칠 수 있으면 0, 갈 수 없으면 보드 칸 수.
        /// </summary>
        public int StepsToEngage(Position position, int range)
        {
            if (!_engageSteps.TryGetValue(range, out int[,] steps))
            {
                steps = EngageStepsCached(range);
                _engageSteps[range] = steps;
            }

            return steps[position.X, position.Y];
        }

        /// <summary>
        /// 접근 지도 캐시(스레드마다). 지도는 사거리, 칠 대상 위치, 걸을 수 있는 칸만으로 정해지므로, 판이 달라도 이 셋이 같으면
        /// 다시 쓴다(탐색 중 대부분의 판은 내 유닛만 움직여 적 위치와 보드가 그대로다). 키를 그대로 비교하므로 결과는 캐시가 없을 때와 같다.
        /// 지도 배열은 여러 분석이 함께 읽으므로 고치면 안 된다.
        /// </summary>
        [ThreadStatic] private static Dictionary<EngageKey, int[,]> _engageCache;

        private const int EngageCacheLimit = 4096;

        private ulong[] _walkable;

        private int[,] EngageStepsCached(int range)
        {
            Board board = State.Board;
            if (_walkable == null)
            {
                _walkable = new ulong[(board.Width * board.Height + 63) / 64];
                for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    int bit = x * board.Height + y;
                    if (Walkable(board.GetCell(new Position(x, y))))
                        _walkable[bit >> 6] |= 1UL << (bit & 63);
                }
            }

            List<Unit> targets = EngageTargets().ToList();
            var cells = new int[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                cells[i] = targets[i].Position.Value.X * board.Height + targets[i].Position.Value.Y;
            Array.Sort(cells);

            var key = new EngageKey(range, board.Width, board.Height, _walkable, cells);
            Dictionary<EngageKey, int[,]> cache = _engageCache ?? (_engageCache = new Dictionary<EngageKey, int[,]>());
            if (cache.TryGetValue(key, out int[,] steps))
                return steps;

            if (cache.Count >= EngageCacheLimit)
                cache.Clear();
            steps = ComputeEngageSteps(range);
            cache[key] = steps;
            return steps;
        }

        /// <summary>접근 지도 캐시 키. 배열 내용까지 비교한다(해시 충돌로 다른 지도를 쓰는 일이 없게).</summary>
        private sealed class EngageKey : IEquatable<EngageKey>
        {
            private readonly int _range, _width, _height, _hash;
            private readonly ulong[] _walkable;
            private readonly int[] _targets;

            public EngageKey(int range, int width, int height, ulong[] walkable, int[] targets)
            {
                _range = range;
                _width = width;
                _height = height;
                _walkable = walkable;
                _targets = targets;

                unchecked
                {
                    int hash = (range * 397 ^ width) * 397 ^ height;
                    foreach (ulong bits in walkable)
                        hash = hash * 31 + bits.GetHashCode();
                    foreach (int cell in targets)
                        hash = hash * 31 + cell;
                    _hash = hash;
                }
            }

            public bool Equals(EngageKey other)
            {
                if (other == null || other._hash != _hash || other._range != _range || other._width != _width || other._height != _height
                    || other._walkable.Length != _walkable.Length || other._targets.Length != _targets.Length)
                    return false;
                for (int i = 0; i < _walkable.Length; i++)
                {
                    if (other._walkable[i] != _walkable[i])
                        return false;
                }

                for (int i = 0; i < _targets.Length; i++)
                {
                    if (other._targets[i] != _targets[i])
                        return false;
                }

                return true;
            }

            public override bool Equals(object obj) => Equals(obj as EngageKey);
            public override int GetHashCode() => _hash;
        }

        private IEnumerable<Unit> EngageTargets()
        {
            return Enemies.Any(enemy => !enemy.IsSummon) ? Enemies.Where(enemy => !enemy.IsSummon) : Enemies;
        }

        /// <summary>적을 칠 수 있는 칸들에서 시작하는 너비 우선 탐색.</summary>
        private int[,] ComputeEngageSteps(int range)
        {
            Board board = State.Board;
            int unreachable = board.Width * board.Height;
            var steps = new int[board.Width, board.Height];
            var queue = new Queue<Position>();
            List<Unit> targets = EngageTargets().ToList();

            foreach (BoardCell cell in board.AllCells())
            {
                Position position = cell.Position;
                bool engaging = Walkable(cell) && targets.Any(enemy => RangeCalculator.GetDistance(position, enemy.Position.Value) <= range);
                steps[position.X, position.Y] = engaging ? 0 : unreachable;
                if (engaging)
                    queue.Enqueue(position);
            }

            var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (queue.Count > 0)
            {
                Position current = queue.Dequeue();
                int next = steps[current.X, current.Y] + 1;
                foreach ((int dx, int dy) in directions)
                {
                    Position neighbor = current.Offset(dx, dy);
                    if (!board.IsInBounds(neighbor) || !Walkable(board.GetCell(neighbor)) || steps[neighbor.X, neighbor.Y] <= next)
                        continue;
                    steps[neighbor.X, neighbor.Y] = next;
                    queue.Enqueue(neighbor);
                }
            }

            return steps;
        }

        private static bool Walkable(BoardCell cell) => cell.Terrain != TerrainType.Wall && !cell.HasObstacle;

        /// <summary>점령 칸이 아직 유효한지.</summary>
        public bool CaptureOpen => State.Capture.IsActive && State.Capture.Tile.HasValue && !State.Capture.CapturedBy.HasValue;

        /// <summary>
        /// 이 칸의 다음 상대 턴 예상 피해. 적마다 (이동 가정 + 사거리) 안에 닿는 기본 공격과 피해 스킬을 모아(각각 한 번씩),
        /// 상대의 다음 턴 AP 로 할 수 있는 만큼 큰 것부터 더한다. 상대 예약 포격과 상대 피해 장판도 더한다. 벽은 0.
        /// </summary>
        private double ComputeDanger(Position position)
        {
            BoardCell cell = State.Board.GetCell(position);
            if (cell.Terrain == TerrainType.Wall)
                return 0;

            _enemyHits = _enemyHits ?? ComputeEnemyHits();
            _enemyApNext = _enemyApNext ?? ApNextTurn(Enemy);

            // 닿는 공격을 큰 피해부터(_enemyHits 는 피해 순으로 정렬돼 있다) 상대 AP 안에서 더한다.
            double total = 0;
            int ap = _enemyApNext.Value;
            int moveCost = MoveCostPerCell;
            foreach (EnemyHit hit in _enemyHits)
            {
                int distance = RangeCalculator.GetDistance(hit.From, position);
                if (distance > hit.Mobility + hit.Reach)
                    continue;

                int cost = hit.Cost + (distance > hit.Reach ? moveCost : 0);
                if (cost > ap)
                    continue;
                total += hit.Damage;
                ap -= cost;
            }

            // 상대 포격의 조준 칸은 봇에게 숨겨진 정보라(GameVisibility) 위험에 넣지 않는다.
            foreach (ScheduledStrike strike in State.ScheduledStrikes)
            {
                if (strike.SourceTeam != Team && !GameVisibility.IsHidden(strike, Team) && strike.Cells.Contains(position))
                    total += strike.Damage;
            }

            if (cell.GetEffect(CellEffectLayer.AreaEffect) is DamageField field && field.Zone.OwnerTeam != Team)
                total += field.DamageAmount;

            return total;
        }

        /// <summary>적 하나의 공격 하나(기본 공격, 피해 스킬 각각): 어디서, 얼마나 움직여, 얼마 사거리로, 얼마 피해를, 몇 AP 에.</summary>
        private struct EnemyHit
        {
            public Position From;
            public int Mobility;
            public int Reach;
            public double Damage;
            public int Cost;
        }

        private List<EnemyHit> _enemyHits;
        private int? _enemyApNext;

        /// <summary>
        /// 적들이 다음 턴에 쓸 수 있는 공격(칸과 무관한 부분). 적 순서, 기본 공격 → 스킬 순서.
        /// 다가가야 닿으면 이동 1칸 비용을 더한다(한 번 다가가면 나머지 공격도 그 자리에서 한다고 본다).
        /// </summary>
        private List<EnemyHit> ComputeEnemyHits()
        {
            var hits = new List<EnemyHit>();
            foreach (Unit enemy in Enemies)
            {
                // 기절한 적은 스스로 아무것도 못 한다.
                if (IsStunned(enemy))
                    continue;

                // 걷기는 속박/설치면 못 한다. 스킬 이동은 그 스킬의 이동 조건으로 본다(돌진은 속박을 무시하고, 도약은 막힌다).
                SkillFacts[] skills = SkillFactsOf(enemy);
                int mobility = IsRooted(enemy) || IsInstalled(enemy) ? 0 : Weights.EnemyMobility;
                foreach (SkillFacts facts in skills)
                {
                    if (facts.SelfMoveReach > mobility && facts.MoveConditions.All(condition => condition.IsMet(State, enemy)))
                        mobility = facts.SelfMoveReach;
                }

                Position from = enemy.Position.Value;
                if (!IsInstalled(enemy))
                    hits.Add(new EnemyHit { From = from, Mobility = mobility, Reach = enemy.Stats.AttackRange, Damage = BasicAttackDamage(enemy), Cost = AttackCost });

                foreach (SkillFacts facts in skills)
                {
                    if (facts.EnemyDamage > 0)
                        hits.Add(new EnemyHit { From = from, Mobility = mobility, Reach = facts.Reach, Damage = facts.EnemyDamage, Cost = facts.Skill.ApCost ?? 0 });
                }
            }

            // 칸마다 큰 피해부터 더하므로 미리 정렬해 둔다(안정 정렬이라 같은 피해는 원래 순서, 칸마다 정렬하던 것과 같다).
            return hits.OrderByDescending(hit => hit.Damage).ToList();
        }

        #endregion
    }
}
