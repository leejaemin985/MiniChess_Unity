using System;
using MiniChess.Bot.Rules;
using MiniChess.Core.State;

namespace MiniChess.Bot.Eval.Terms
{
    /// <summary>
    /// 남겨 둬서 내 턴에 회복분보다 더 쓸 수 있는 AP 의 가치(최대치에 막혀 버려질 몫은 0).
    /// 상대 차례면 다음 내 턴의 회복 후 AP 로, 내 차례(상대 응수까지 본 판)면 이미 회복된 지금 AP 로 본다.
    /// </summary>
    internal sealed class CarriedApTerm : IEvalTerm
    {
        public string Name => "CarriedAp";

        public double Evaluate(BoardAnalysis a)
        {
            GameState state = a.State;
            if (!state.Rules.Ap.CarryOver)
                return 0;

            PlayerState player = state.GetPlayer(a.Team);
            int recovery = Math.Min(player.Ap.Max, state.Rules.Ap.TurnRecoveryAp);
            int nextTurnAp = state.CurrentTeam == a.Team ? player.Ap.Current : Math.Min(player.Ap.Max, player.Ap.Current + recovery);
            return Math.Max(0, nextTurnAp - recovery) * a.Weights.ApCost;
        }
    }
}
