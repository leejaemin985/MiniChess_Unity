using System.Linq;
using MiniChess.Core.Skills;
using MiniChess.Core.Skills.Effects;
using MiniChess.Core.Skills.Targeting;
using MiniChess.Core.State;
using MiniChess.Core.Statuses;
using MiniChess.Core.Statuses.Library;

namespace MiniChess.Bot.Rules
{
    /// <summary>스킬 정의(부품)만 보고 내는 대략적인 추정치. 상대 위협도 계산에 쓴다.</summary>
    internal static class SkillEstimates
    {
        /// <summary>적 한 명에게 줄 수 있는 피해 추정(즉시 피해 + 전방 공격 + 예약 포격 절반 + 화상 합).</summary>
        public static double EnemyDamage(SkillDefinition skill)
        {
            double damage = 0;
            foreach (SkillEffect effect in skill.Effects)
            {
                switch (effect)
                {
                    case DamageEffect hit when hit.Filter == TargetFilter.Enemy:
                        damage += hit.Amount ?? 0;
                        break;
                    case FrontStrikeEffect strike:
                        damage += strike.Damage ?? 0;
                        break;
                    case ScheduleStrikeEffect scheduled:
                        damage += (scheduled.Damage ?? 0) * 0.5;
                        break;
                    case ApplyStatusEffect status when status.Filter == TargetFilter.Enemy:
                        damage += StatusDamage(status.Status);
                        break;
                }
            }

            return damage;
        }

        /// <summary>시전자 위치에서 효과가 닿는 대략적인 거리(지정 사거리 + 범위 반경 1).</summary>
        public static int Reach(SkillDefinition skill)
        {
            switch (skill.Targeting)
            {
                case UnitTargeting unit:
                    return unit.Range ?? 0;
                case CellTargeting cell:
                    return (cell.Range ?? 0) + 1;
                case StraightMoveTargeting straight:
                    return (straight.Range ?? 0) + 1;
                default:
                    return 0;
            }
        }

        /// <summary>시전자를 다른 칸으로 옮기는 스킬(도약 등)의 이동 거리. 아니면 0.</summary>
        public static int SelfMoveReach(SkillDefinition skill)
        {
            if (!skill.Effects.Any(effect => effect is TeleportEffect || effect is DashEffect))
                return 0;

            return Reach(skill) - (skill.Targeting is CellTargeting || skill.Targeting is StraightMoveTargeting ? 1 : 0);
        }

        /// <summary>지속 피해 상태(화상)의 총 피해. 피해 상태가 아니면 0.</summary>
        public static double StatusDamage(StatusDefinition status)
        {
            return status.Behavior is BurnBehavior burn ? burn.DamagePerTrigger * status.Duration : 0;
        }

        /// <summary>유닛에 걸린 상태의 행동(Behavior). 없으면 null.</summary>
        public static T FindBehavior<T>(Unit unit, string statusId) where T : StatusBehavior
        {
            return unit.FindStatus(statusId)?.Definition.Behavior as T;
        }
    }
}
