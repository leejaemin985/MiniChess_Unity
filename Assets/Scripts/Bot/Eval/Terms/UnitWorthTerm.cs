using MiniChess.Bot.Rules;
using MiniChess.Core.State;

namespace MiniChess.Bot.Eval.Terms
{
    /// <summary>살아 있는 유닛의 가치(남은 HP + 보호막(비율) + 처치했을 때의 가치), 아군 - 적.</summary>
    internal sealed class UnitWorthTerm : IEvalTerm
    {
        public string Name => "UnitWorth";

        public double Evaluate(BoardAnalysis a)
        {
            double value = 0;
            foreach (Unit unit in a.Allies)
                value += Worth(a, unit);
            foreach (Unit unit in a.Enemies)
                value -= Worth(a, unit);
            return value;
        }

        public static double Worth(BoardAnalysis a, Unit unit)
        {
            return unit.Stats.CurrentHp + unit.Stats.Shield * a.Weights.ShieldDamageRatio + a.KillValue(unit);
        }
    }
}
