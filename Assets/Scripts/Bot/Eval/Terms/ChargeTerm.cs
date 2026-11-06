using MiniChess.Bot.Rules;
using MiniChess.Core.State;
using MiniChess.Core.Statuses.Library;

namespace MiniChess.Bot.Eval.Terms
{
    /// <summary>아군이 쌓아 둔 차징(다음 공격의 추가 피해).</summary>
    internal sealed class ChargeTerm : IEvalTerm
    {
        public string Name => "Charge";

        public double Evaluate(BoardAnalysis a)
        {
            double value = 0;
            foreach (Unit unit in a.Allies)
            {
                if (SkillEstimates.FindBehavior<ChargedBehavior>(unit, StatusLibrary.ChargedId) is ChargedBehavior charged)
                    value += charged.BonusDamage * a.Weights.DamageOverTime;
            }

            return value;
        }
    }
}
