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
        public BoardAnalysis(GameController game, Team team, BotWeights weights, bool stalled = false)
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

            _danger = ComputeDanger();
            Caution = stalled ? Math.Min(ComputeCaution(), weights.CautionStalled) : ComputeCaution();
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

        /// <summary>유닛이 한 번의 전투 행동으로 줄 수 있는 가장 큰 피해(기본 공격 또는 스킬).</summary>
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

        /// <summary>이번 턴에 아직 전투 행동(기본 공격/전투 스킬)을 할 수 있는지.</summary>
        public static bool CanStillFight(Unit unit)
        {
            return !unit.TurnState.CombatActionUsed && !unit.TurnState.ActionsEnded;
        }

        public IEnumerable<SkillDefinition> SkillsOf(Unit unit)
        {
            return unit.Stats.Base.SkillIds.Select(FindSkill).Where(skill => skill != null);
        }

        private double ComputeThreat(Unit unit)
        {
            double basic = IsInstalled(unit) ? 0 : unit.Stats.Attack;
            double skill = SkillsOf(unit).Select(SkillEstimates.EnemyDamage).DefaultIfEmpty(0).Max();
            return Math.Max(basic, skill);
        }

        #endregion

        #region Damage

        /// <summary>
        /// target 에게 amount 피해를 주는 가치. 호위(직접 피해 대신 받기), 저주 표식 추가 피해, 보호막, 처치를 반영한다.
        /// 아군이 맞으면 음수.
        /// </summary>
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

        /// <summary>내 유닛이 이 칸에서 상대 턴을 맞으면 받을 것으로 예상되는 피해.</summary>
        public double DangerAt(Position position) => _danger[position.X, position.Y];

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
                steps = ComputeEngageSteps(range);
                _engageSteps[range] = steps;
            }

            return steps[position.X, position.Y];
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
        /// 다음 상대 턴 예상 피해. 적마다 (이동 가정 + 사거리) 안이면 가장 큰 한 방을 더하되,
        /// 상대 AP 로 할 수 있는 공격 수만큼 큰 것부터 더한다. 상대 예약 포격과 상대 피해 장판도 더한다.
        /// </summary>
        private double[,] ComputeDanger()
        {
            var danger = new double[State.Board.Width, State.Board.Height];
            PlayerState enemyPlayer = State.GetPlayer(Enemy);
            int enemyApNext = Math.Min(enemyPlayer.Ap.Max, enemyPlayer.Ap.Current + State.Rules.Ap.TurnRecoveryAp);
            int attacks = Math.Max(1, enemyApNext / Math.Max(1, AttackCost + 1));

            foreach (BoardCell cell in State.Board.AllCells())
            {
                if (cell.Terrain == TerrainType.Wall)
                    continue;

                Position position = cell.Position;
                List<double> hits = Enemies
                    .Select(enemy => EnemyHitAt(enemy, position))
                    .Where(hit => hit > 0)
                    .OrderByDescending(hit => hit)
                    .Take(attacks)
                    .ToList();
                double total = hits.Sum();

                // 상대 포격의 조준 칸은 봇에게 숨겨진 정보라(GameVisibility) 위험에 넣지 않는다.
                foreach (ScheduledStrike strike in State.ScheduledStrikes)
                {
                    if (strike.Source.Team != Team && !GameVisibility.IsHidden(strike, Team) && strike.Cells.Contains(position))
                        total += strike.Damage;
                }

                if (cell.GetEffect(CellEffectLayer.AreaEffect) is DamageField field && field.Zone.OwnerTeam != Team)
                    total += field.DamageAmount;

                danger[position.X, position.Y] = total;
            }

            return danger;
        }

        /// <summary>적 하나가 다음 턴에 이 칸을 칠 수 있으면 그 피해, 아니면 0.</summary>
        private double EnemyHitAt(Unit enemy, Position position)
        {
            // 기절한 적은 스스로 아무것도 못 한다.
            if (IsStunned(enemy))
                return 0;

            // 걷기는 속박/설치면 못 한다. 스킬 이동은 그 스킬의 이동 조건으로 본다(돌진은 속박을 무시하고, 도약은 막힌다).
            int mobility = IsRooted(enemy) || IsInstalled(enemy) ? 0 : Weights.EnemyMobility;
            mobility = Math.Max(mobility, SkillsOf(enemy)
                .Where(skill => skill.Conditions.OfType<CanMoveCondition>().All(condition => condition.IsMet(State, enemy)))
                .Select(SkillEstimates.SelfMoveReach)
                .DefaultIfEmpty(0)
                .Max());

            int distance = RangeCalculator.GetDistance(enemy.Position.Value, position);
            double best = 0;

            if (!IsInstalled(enemy) && distance <= mobility + enemy.Stats.AttackRange)
                best = BasicAttackDamage(enemy);

            foreach (SkillDefinition skill in SkillsOf(enemy))
            {
                double damage = SkillEstimates.EnemyDamage(skill);
                if (damage > best && distance <= mobility + SkillEstimates.Reach(skill))
                    best = damage;
            }

            return best;
        }

        #endregion
    }
}
