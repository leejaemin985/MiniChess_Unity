using MiniChess.Bot.Rules;
using MiniChess.Core.State;
using MiniChess.Core.Statuses;
using MiniChess.Core.Statuses.Library;

namespace MiniChess.Bot.Eval.Terms
{
    /// <summary>적에게 남은 지속 피해와 저주 표식의 가치(아직 들어가지 않은 피해).</summary>
    internal sealed class AfflictionsTerm : IEvalTerm
    {
        public string Name => "Afflictions";

        public double Evaluate(BoardAnalysis a)
        {
            double value = 0;
            foreach (Unit enemy in a.Enemies)
            {
                foreach (StatusEffect status in enemy.Statuses)
                {
                    switch (status.Definition.Behavior)
                    {
                        case DamageOverTimeBehavior dot:
                            value += dot.DamagePerTrigger * status.Remaining * a.Weights.DamageOverTime;
                            break;
                        case CurseMarkBehavior mark:
                            value += mark.BonusDamage * a.Weights.DamageOverTime;
                            break;
                    }
                }
            }

            return value;
        }
    }
}
