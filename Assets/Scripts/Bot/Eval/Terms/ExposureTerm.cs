using System;
using MiniChess.Bot.Rules;
using MiniChess.Core.State;

namespace MiniChess.Bot.Eval.Terms
{
    /// <summary>다음 상대 턴에 아군이 받을 것으로 예상되는 손해(규칙 봇의 위치 판단과 같은 식). 신중함 배수를 곱한다.</summary>
    internal sealed class ExposureTerm : IEvalTerm
    {
        public string Name => "Exposure";

        public double Evaluate(BoardAnalysis a)
        {
            double value = 0;
            foreach (Unit unit in a.Allies)
            {
                double danger = a.DangerAt(unit.Position.Value);
                int effectiveHp = BoardAnalysis.EffectiveHp(unit);
                double loss = a.Caution * a.Weights.Danger * Math.Min(danger, effectiveHp);
                if (danger >= effectiveHp)
                    loss += a.Caution * a.Weights.DeathRisk * a.KillValue(unit);
                value -= loss;
            }

            return value;
        }
    }
}
