using MiniChess.Bot.Rules;
using MiniChess.Core.State;

namespace MiniChess.Bot.Eval.Terms
{
    /// <summary>점령: 쌓인 진행도 차이와 점령 칸 위의 유닛(아군이면 +, 적이면 -).</summary>
    internal sealed class CaptureTerm : IEvalTerm
    {
        public string Name => "Capture";

        public double Evaluate(BoardAnalysis a)
        {
            if (!a.CaptureOpen)
                return 0;

            CaptureState capture = a.State.Capture;
            double value = a.Weights.Capture * (capture.GetProgress(a.Team) - capture.GetProgress(a.Enemy));
            Unit holder = a.State.Board.GetLivingUnit(capture.Tile.Value);
            if (holder != null && !holder.IsSummon)
                value += holder.Team == a.Team ? a.Weights.Capture : -a.Weights.Capture;
            return value;
        }
    }
}
