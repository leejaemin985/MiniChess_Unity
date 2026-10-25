using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Core.Actions;
using MiniChess.Core.Combat;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;
using MiniChess.Core.Effects;
using MiniChess.Core.Effects.Zones;
using MiniChess.Core.Skills;
using MiniChess.Core.Skills.Effects;
using MiniChess.Core.Snapshots;
using MiniChess.Core.State;
using MiniChess.Core.Statuses;
using MiniChess.Core.Statuses.Library;

namespace MiniChess.Bot.Rules
{
    /// <summary>
    /// 행동 후보 하나의 점수(예상 이득 - AP 비용). 시뮬레이션 없이 판 분석과 스킬 정의(효과 부품)만으로 추정한다.
    /// 점수 단위는 대략 "상대에게 준 HP 피해 1" 이다.
    /// </summary>
    internal class ActionScorer
    {
        private readonly BoardAnalysis _a;
        private readonly BotWeights _w;
        private readonly Dictionary<int, List<SkillInfo>> _usableSkills = new Dictionary<int, List<SkillInfo>>();

        public ActionScorer(BoardAnalysis analysis)
        {
            _a = analysis;
            _w = analysis.Weights;
        }

        public double Score(BotAction action)
        {
            switch (action.Kind)
            {
                case BotActionKind.Move:
                    return ScoreMove(action);
                case BotActionKind.Attack:
                    return ScoreAttack(action);
                case BotActionKind.AttackObstacle:
                    return _w.Obstacle - ApPenalty(_a.AttackCost, _a.Ap);
                case BotActionKind.UseSkill:
                    return ScoreSkill(action);
                default:
                    return 0;
            }
        }

        /// <summary>이동한 자리에서 이번 턴에 바로 공격할 수 있어 고른 이동인지(이동 후 공격으로 이어 가야 하는지).</summary>
        public bool IsOffensiveMove(BotAction action)
        {
            if (action.Kind != BotActionKind.Move)
                return false;

            Unit unit = _a.State.FindUnit(action.UnitId);
            int cost = Math.Max(1, _a.State.Board.GetStraightPath(unit.Position.Value, action.Cell).Count) * _a.MoveCostPerCell;
            return BoardAnalysis.CanStillFight(unit) && BestOffenseFrom(unit, action.Cell, _a.Ap - cost) > 0;
        }

        #region AP

        /// <summary>
        /// AP 를 cost 만큼 쓰는 비용. 다음 턴 회복으로 최대치를 넘겨 버려질 몫은 거의 공짜로 본다.
        /// </summary>
        private double ApPenalty(int cost, int apNow)
        {
            ApRuleData rule = _a.State.Rules.Ap;
            int max = _a.State.GetPlayer(_a.Team).Ap.Max;
            int wasted = rule.CarryOver ? Math.Max(0, apNow + rule.TurnRecoveryAp - max) : apNow;
            int free = Math.Min(cost, wasted);
            return free * _w.WastedApCost + (cost - free) * _w.ApCost;
        }

        #endregion

        #region Position

        /// <summary>
        /// 유닛이 이 칸에 있을 때의 가치: 이번 턴에 바로 할 수 있는 공격 + 적과의 거리 + 위협도 + 칸 보너스(점령, 회복 장판).
        /// </summary>
        private double PositionValue(Unit unit, Position cell, int apAvailable, bool canFight)
        {
            double value = 0;

            if (canFight)
                value += _w.ImmediateOffense * BestOffenseFrom(unit, cell, apAvailable);

            value -= _w.Approach * _a.StepsToEngage(cell, PreferredRange(unit));

            double danger = _a.DangerAt(cell);
            int effectiveHp = BoardAnalysis.EffectiveHp(unit);
            value -= _a.Caution * _w.Danger * Math.Min(danger, effectiveHp);
            if (danger >= effectiveHp)
                value -= _a.Caution * _w.DeathRisk * _a.KillValue(unit);

            if (!unit.IsSummon && _a.CaptureOpen && _a.State.Capture.Tile == cell)
                value += _w.Capture * (1 + _a.State.Capture.GetProgress(_a.Team));

            if (_a.State.Board.GetCell(cell).GetEffect(CellEffectLayer.AreaEffect) is HealField heal && heal.Zone.OwnerTeam == _a.Team)
                value += Math.Min(heal.HealAmount, unit.Stats.MaxHp - unit.Stats.CurrentHp);

            return value;
        }

        /// <summary>싸우기 좋은 적과의 거리: 기본 공격 사거리와 피해 스킬 사거리 중 큰 값.</summary>
        private int PreferredRange(Unit unit)
        {
            int skillRange = _a.SkillsOf(unit)
                .Where(skill => SkillEstimates.EnemyDamage(skill) > 0)
                .Select(SkillEstimates.Reach)
                .DefaultIfEmpty(0)
                .Max();
            return Math.Max(1, Math.Max(unit.Stats.AttackRange, skillRange - 1));
        }

        /// <summary>이 칸에서 이번 턴에 바로 할 수 있는 가장 좋은 공격(기본 공격 또는 피해 스킬)의 점수.</summary>
        private double BestOffenseFrom(Unit unit, Position cell, int ap)
        {
            double best = 0;

            if (!BoardAnalysis.IsInstalled(unit) && ap >= _a.AttackCost)
            {
                foreach (Unit enemy in _a.Enemies)
                {
                    if (RangeCalculator.GetDistance(cell, enemy.Position.Value) > unit.Stats.AttackRange)
                        continue;

                    double value = _a.DamageValue(unit, enemy, BoardAnalysis.BasicAttackDamage(unit), DamageType.Direct)
                        + OnHitStatusValue(unit, enemy)
                        - ApPenalty(_a.AttackCost, ap);
                    best = Math.Max(best, value);
                }
            }

            foreach (SkillInfo info in UsableSkills(unit))
            {
                SkillDefinition skill = info.Definition;
                int cost = skill.ApCost ?? 0;
                double damage = SkillEstimates.EnemyDamage(skill);
                if (damage <= 0 || ap < cost)
                    continue;

                int reach = SkillEstimates.Reach(skill);
                foreach (Unit enemy in _a.Enemies)
                {
                    if (RangeCalculator.GetDistance(cell, enemy.Position.Value) > reach)
                        continue;

                    best = Math.Max(best, _a.DamageValue(unit, enemy, damage, DamageType.Direct) - ApPenalty(cost, ap));
                }
            }

            return best;
        }

        private List<SkillInfo> UsableSkills(Unit unit)
        {
            if (!_usableSkills.TryGetValue(unit.Id, out List<SkillInfo> skills))
            {
                skills = _a.Game.GetSkills(unit.Id)
                    .Where(info => info.Usable == SkillFailReason.None && info.Definition != null)
                    .ToList();
                _usableSkills[unit.Id] = skills;
            }

            return skills;
        }

        /// <summary>칸으로 들어갈 때 밟는 적 덫의 손해(경로 포함). 봇에게 숨겨진 덫(상대가 설치한 덫, GameVisibility)은 모르는 것으로 본다.</summary>
        private double PathHazard(IEnumerable<Position> path)
        {
            double value = 0;
            foreach (Position position in path)
            {
                if (_a.State.Board.GetCell(position).GetEffect(CellEffectLayer.Trap) is RootTrap trap && trap.Zone.OwnerTeam != _a.Team
                    && !GameVisibility.IsHidden(trap, _a.Team))
                    value -= trap.Damage + _w.Root;
            }

            return value;
        }

        /// <summary>유닛을 from → to 로 옮겼을 때의 위치 가치 변화.</summary>
        private double Relocation(Unit unit, Position to, int apAfter, bool canFightAfter)
        {
            Position from = unit.Position.Value;
            return PositionValue(unit, to, apAfter, canFightAfter)
                - PositionValue(unit, from, _a.Ap, BoardAnalysis.CanStillFight(unit));
        }

        #endregion

        #region Move / Attack

        private double ScoreMove(BotAction action)
        {
            Unit unit = _a.State.FindUnit(action.UnitId);
            Position from = unit.Position.Value;
            IReadOnlyList<Position> path = _a.State.Board.GetStraightPath(from, action.Cell);
            int cost = Math.Max(1, path.Count) * _a.MoveCostPerCell;

            return Relocation(unit, action.Cell, _a.Ap - cost, BoardAnalysis.CanStillFight(unit))
                + PathHazard(path)
                - ApPenalty(cost, _a.Ap);
        }

        private double ScoreAttack(BotAction action)
        {
            Unit attacker = _a.State.FindUnit(action.UnitId);
            Unit target = _a.State.FindUnit(action.TargetUnitId);

            return _a.DamageValue(attacker, target, BoardAnalysis.BasicAttackDamage(attacker), DamageType.Direct)
                + OnHitStatusValue(attacker, target)
                - ApPenalty(_a.AttackCost, _a.Ap);
        }

        /// <summary>기본 공격 적중 시 거는 상태(분신 저주 표식, 점령 보상 화상 등)의 가치.</summary>
        private double OnHitStatusValue(Unit attacker, Unit target)
        {
            return attacker.BasicAttackStatuses
                .Concat(_a.State.GetPlayer(attacker.Team).BasicAttackStatuses)
                .Sum(status => StatusValue(status, attacker, target));
        }

        #endregion

        #region Skill

        private double ScoreSkill(BotAction action)
        {
            Unit caster = _a.State.FindUnit(action.UnitId);
            SkillDefinition skill = _a.FindSkill(action.SkillId);
            if (caster == null || skill == null)
                return 0;

            int cost = skill.ApCost ?? 0;
            var context = new SkillGuess(caster, skill, action, _a.Game.GetSkillArea(caster.Id, skill.Id, action.Targets[0], action.Rotation), _a.Ap - cost);

            // 예약 포격이 적을 하나도 노리지 않으면 부수 효과(설치 보호막 등)만 보고 쓰지 않는다.
            if (skill.Effects.OfType<ScheduleStrikeEffect>().Any() && UnitsIn(context.Area, TargetFilter.Enemy, caster).Count == 0)
                return 0;

            double value = skill.Effects.Sum(effect => EffectValue(effect, context));
            return value - ApPenalty(cost, _a.Ap);
        }

        /// <summary>스킬 점수 계산에 쓰는 묶음.</summary>
        private class SkillGuess
        {
            public Unit Caster { get; }
            public SkillDefinition Skill { get; }
            public BotAction Action { get; }
            public IReadOnlyList<Position> Area { get; }
            public int ApAfter { get; }

            public SkillGuess(Unit caster, SkillDefinition skill, BotAction action, IReadOnlyList<Position> area, int apAfter)
            {
                Caster = caster;
                Skill = skill;
                Action = action;
                Area = area;
                ApAfter = apAfter;
            }
        }

        private List<Unit> UnitsIn(IEnumerable<Position> cells, TargetFilter filter, Unit caster)
        {
            return cells
                .Where(_a.State.Board.IsInBounds)
                .Select(_a.State.Board.GetLivingUnit)
                .Where(unit => unit != null && filter.Matches(caster, unit))
                .Distinct()
                .ToList();
        }

        private double EffectValue(SkillEffect effect, SkillGuess g)
        {
            Unit caster = g.Caster;
            Position target = g.Action.Targets[0];

            switch (effect)
            {
                case DamageEffect hit:
                    return UnitsIn(g.Area, hit.Filter, caster).Sum(unit => _a.DamageValue(caster, unit, hit.Amount ?? 0, hit.Type));

                case FrontStrikeEffect strike:
                {
                    (int dx, int dy) = caster.Position.Value.DirectionTo(target);
                    Position front = target.Offset(dx, dy);
                    if (!_a.State.Board.IsInBounds(front))
                        return 0;
                    Unit victim = _a.State.Board.GetLivingUnit(front);
                    return victim != null && victim.Team != caster.Team
                        ? _a.DamageValue(caster, victim, strike.Damage ?? 0, DamageType.Direct)
                        : 0;
                }

                case DashEffect _:
                    return Relocation(caster, target, g.ApAfter, BoardAnalysis.CanFightAfter(caster, g.Skill))
                        + PathHazard(_a.State.Board.GetStraightPath(caster.Position.Value, target));

                case TeleportEffect _:
                    return Relocation(caster, target, g.ApAfter, BoardAnalysis.CanFightAfter(caster, g.Skill))
                        + PathHazard(new[] { target });

                case ScheduleStrikeEffect scheduled:
                    return UnitsIn(g.Area, TargetFilter.Enemy, caster).Sum(enemy =>
                        _a.DamageValue(caster, enemy, scheduled.Damage ?? 0, DamageType.Area) * StrikeHitChance(enemy));

                case ApplyStatusEffect status:
                    return UnitsIn(g.Area, status.Filter, caster).Sum(unit => StatusValue(status.Status, caster, unit));

                case ApplySelfStatusEffect self:
                {
                    if (caster.FindStatus(self.Status.Id) != null)
                        return SelfStatusValue(self.Status, caster);
                    var selfGuess = new SkillGuess(caster, g.Skill, g.Action, new[] { caster.Position.Value }, g.ApAfter);
                    return SelfStatusValue(self.Status, caster) + self.OnApplied.Sum(nested => EffectValue(nested, selfGuess));
                }

                case RemoveSelfStatusEffect remove:
                {
                    if (caster.FindStatus(remove.StatusId) == null)
                        return 0;
                    var selfGuess = new SkillGuess(caster, g.Skill, g.Action, new[] { caster.Position.Value }, g.ApAfter);
                    return RemoveSelfStatusValue(remove.StatusId, caster) + remove.OnRemoved.Sum(nested => EffectValue(nested, selfGuess));
                }

                case ApplyShieldEffect shield:
                    return UnitsIn(g.Area, shield.Filter, caster).Sum(unit => ShieldValue(unit, shield.ShieldId, shield.Amount ?? 0));

                case RemoveShieldEffect removeShield:
                    return -UnitsIn(g.Area, removeShield.Filter, caster)
                        .Sum(unit => (unit.Stats.FindShield(removeShield.ShieldId)?.Amount ?? 0) * (_w.ShieldIdle + _w.Protection));

                // 밀린 뒤 이어지는 효과(기절 등)는 지금 칸 기준으로 어림한다.
                case KnockbackEffect knockback:
                    return UnitsIn(g.Area, knockback.Filter, caster).Count(unit => unit.Team != caster.Team) * _w.Knockback
                        + knockback.AfterPush.Sum(nested => EffectValue(nested, g));

                case CleanseEffect cleanse:
                    return UnitsIn(g.Area, cleanse.Filter, caster).Sum(unit => unit.Statuses
                        .Where(status => CleanseEffect.IsCleansable(unit, status))
                        .Sum(status => DebuffValue(status.Definition, unit, already: false)));

                case SummonEffect _:
                    return _w.Summon;

                case WarpUnitEffect _:
                    return WarpUnitValue(g, g.Action.Targets[0], g.Action.Targets[1]);

                case SwapWithCasterEffect _:
                    return SwapValue(g, caster.Position.Value, target);

                case PlaceZoneEffect zone:
                    return ZoneValue(zone, g);

                default:
                    return 0;
            }
        }

        private double StrikeHitChance(Unit enemy)
        {
            bool immobile = BoardAnalysis.IsRooted(enemy) || BoardAnalysis.IsInstalled(enemy) || BoardAnalysis.IsStunned(enemy);
            return immobile ? _w.StrikeHitChanceImmobile : _w.StrikeHitChance;
        }

        /// <summary>from 칸의 유닛을 destination 으로 옮겼을 때(실 관문)의 위치 가치 변화 + 도착 칸 위험.</summary>
        private double WarpUnitValue(SkillGuess g, Position from, Position destination)
        {
            Unit unit = _a.State.Board.GetLivingUnit(from);
            if (unit == null)
                return 0;

            bool fights = unit == g.Caster
                ? BoardAnalysis.CanFightAfter(g.Caster, g.Skill)
                : BoardAnalysis.CanStillFight(unit);
            return Relocation(unit, destination, g.ApAfter, fights) + PathHazard(new[] { destination });
        }

        /// <summary>두 칸의 유닛 자리를 맞바꿨을 때 두 유닛의 위치 가치 변화 합.</summary>
        private double SwapValue(SkillGuess g, Position first, Position second)
        {
            Unit a = _a.State.Board.GetLivingUnit(first);
            Unit b = _a.State.Board.GetLivingUnit(second);
            if (a == null || b == null)
                return 0;

            bool casterFights = BoardAnalysis.CanFightAfter(g.Caster, g.Skill);
            bool aFights = a == g.Caster ? casterFights : BoardAnalysis.CanStillFight(a);
            bool bFights = b == g.Caster ? casterFights : BoardAnalysis.CanStillFight(b);

            return Relocation(a, second, g.ApAfter, aFights) + Relocation(b, first, g.ApAfter, bFights);
        }

        /// <summary>상태 부여 가치. 적에게는 제약/피해, 아군에게는 보호(호위), 자신에게는 SelfStatusValue.</summary>
        private double StatusValue(StatusDefinition status, Unit caster, Unit target)
        {
            if (status.Behavior is SealedBehavior)
                return SealValue(target);

            if (target.Team != _a.Team)
                return DebuffValue(status, target, already: target.FindStatus(status.Id) != null);

            if (status.Behavior is GuardBehavior)
            {
                if (target == caster)
                    return 0;
                double danger = _a.DangerAt(target.Position.Value);
                return Math.Min(danger, BoardAnalysis.EffectiveHp(target)) * _a.Caution * _w.Protection;
            }

            // 자기 지정(TargetFilter.Self) 상태도 ApplyStatusEffect 로 들어온다(암살자 차징 등).
            if (target == caster)
                return SelfStatusValue(status, caster);

            return _w.OtherStatus;
        }

        /// <summary>
        /// 나쁜 상태 하나가 target 에게 걸려 있는 가치(적에게 걸 때의 이득, 아군에게서 정화로 지울 때의 이득).
        /// already 면 이미 같은 상태가 있어 새로 거는 이득이 적다.
        /// 기절은 다음 자기 턴의 행동을 통째로 막으므로 봉인과 같은 기준(위협)으로 본다.
        /// </summary>
        private double DebuffValue(StatusDefinition status, Unit target, bool already)
        {
            switch (status.Behavior)
            {
                case RootBehavior _:
                    return already ? _w.OtherStatus : _w.Root;
                case DistanceLimitBehavior _:
                    return already ? 0 : _w.DistanceLimit;
                case DamageOverTimeBehavior _:
                    return SkillEstimates.StatusDamage(status) * _w.DamageOverTime;
                case CurseMarkBehavior mark:
                    return mark.BonusDamage * _w.DamageOverTime;
                case StunnedBehavior _:
                    return already ? 0 : _a.Threat(target) * _a.Caution * _w.Protection;
                default:
                    return _w.OtherStatus;
            }
        }

        /// <summary>
        /// 봉인 가치. 봉인된 유닛은 다음 자기 턴 끝까지 행동할 수 없고 피해도 받지 않는다.
        ///   적: 다음 상대 턴의 위협을 막는 만큼. 대신 이번 턴에 더는 칠 수 없으므로 처치보다는 낮게 둔다.
        ///   아군(자신 포함): 다음 상대 턴에 죽을 상황이면 살리는 가치, 아니면 자기 턴을 잃는 손해.
        /// </summary>
        private double SealValue(Unit target)
        {
            if (target.FindStatus(StatusLibrary.SealedId) != null)
                return 0;

            if (target.Team != _a.Team)
                return _a.Threat(target) * _a.Caution * _w.Protection;

            double lostTurn = _a.Threat(target);
            bool wouldDie = _a.DangerAt(target.Position.Value) >= BoardAnalysis.EffectiveHp(target);
            return wouldDie ? _a.KillValue(target) * _a.Caution - lostTurn : -lostTurn;
        }

        /// <summary>자신에게 거는 상태(차징, 설치)의 가치.</summary>
        private double SelfStatusValue(StatusDefinition status, Unit caster)
        {
            switch (status.Behavior)
            {
                case ChargedBehavior charged:
                    return caster.FindStatus(status.Id) != null ? 0 : charged.BonusDamage * _w.DamageOverTime;
                case InstalledBehavior _:
                    return 0;
                default:
                    return _w.OtherStatus;
            }
        }

        /// <summary>
        /// 자신의 상태를 푸는 가치. 설치 해체는 착탄 대기 중인 포격이 없고, 지금 포격으로 맞힐 수 있는 적이 없을 때만 이득.
        /// </summary>
        private double RemoveSelfStatusValue(string statusId, Unit caster)
        {
            if (statusId != StatusLibrary.InstalledId)
                return 0;

            if (_a.State.ScheduledStrikes.Any(strike => strike.Source == caster))
                return -_w.Uninstall;

            // 지금 AP 가 모자라 포격을 못 쓰는 것과 맞힐 적이 없는 것은 다르다. 사용 가능 여부와 무관하게 지정 후보 칸으로 본다.
            foreach (SkillDefinition skill in _a.SkillsOf(caster).Where(skill => skill.IsConfigured && skill.Effects.Any(effect => effect is ScheduleStrikeEffect)))
            {
                foreach (Position target in skill.Targeting.GetCandidates(_a.State, caster).Distinct())
                {
                    if (UnitsIn(_a.Game.GetSkillArea(caster.Id, skill.Id, target), TargetFilter.Enemy, caster).Count > 0)
                        return -_w.Uninstall;
                }
            }

            return _w.Uninstall;
        }

        /// <summary>보호막 가치: 위협받는 만큼은 크게, 나머지는 조금. 같은 Id 보호막이 이미 그 이상이면 0.</summary>
        private double ShieldValue(Unit unit, string shieldId, int amount)
        {
            int existing = unit.Stats.FindShield(shieldId)?.Amount ?? 0;
            int gained = Math.Max(0, amount - existing);
            if (gained == 0)
                return 0;

            double uncovered = Math.Max(0, _a.DangerAt(unit.Position.Value) - unit.Stats.Shield);
            return Math.Min(gained, uncovered) * _a.Caution * _w.Protection + gained * _w.ShieldIdle;
        }

        /// <summary>
        /// 장판/덫/장애물 설치 가치. 칸 효과 종류는 팩토리로 시험 생성해 확인한다(상태는 바뀌지 않는다).
        /// </summary>
        private double ZoneValue(PlaceZoneEffect zone, SkillGuess g)
        {
            int lifetime = zone.Lifetime ?? 1;
            int turns = Math.Min(lifetime, 2);
            var probe = new FieldZone(g.Skill.Id, g.Caster, lifetime, _a.State.TurnNumber);
            double value = 0;

            foreach (Position cell in g.Area.Where(_a.State.Board.IsInBounds))
            {
                Unit occupant = _a.State.Board.GetLivingUnit(cell);
                switch (zone.Factory(probe, cell))
                {
                    case DamageField damage:
                        if (occupant != null && occupant.Team != _a.Team)
                            value += _a.DamageValue(g.Caster, occupant, damage.DamageAmount * turns, DamageType.Area) * _w.DamageOverTime;
                        break;

                    case HealField heal:
                        if (occupant != null && occupant.Team == _a.Team)
                            value += Math.Min(heal.HealAmount * turns, occupant.Stats.MaxHp - occupant.Stats.CurrentHp);
                        break;

                    case RootTrap _:
                        if (_a.Enemies.Any(enemy => !enemy.IsSummon && RangeCalculator.GetDistance(enemy.Position.Value, cell) <= 2))
                            value += _w.Trap;
                        break;

                    case Obstacle _:
                        value += _w.Obstacle;
                        break;
                }
            }

            return value;
        }

        #endregion
    }
}
