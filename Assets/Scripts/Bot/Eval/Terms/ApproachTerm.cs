using MiniChess.Bot.Rules;
using MiniChess.Core.State;

namespace MiniChess.Bot.Eval.Terms
{
    /// <summary>아군이 선호 사거리로 적을 칠 수 있는 칸까지 남은 이동 칸 수만큼 감점(멀리서 머뭇거리지 않게).</summary>
    internal sealed class ApproachTerm : IEvalTerm
    {
        public string Name => "Approach";

        public double Evaluate(BoardAnalysis a)
        {
            double value = 0;
            foreach (Unit unit in a.Allies)
                value -= a.Weights.Approach * a.StepsToEngage(unit.Position.Value, a.PreferredRange(unit));
            return value;
        }
    }
}
