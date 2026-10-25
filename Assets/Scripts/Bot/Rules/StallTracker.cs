using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Turns;

namespace MiniChess.Bot.Rules
{
    /// <summary>
    /// 교착 판정: 내 턴이 StallTurns 번 지나도록 적 체력(보호막 포함) 합이 줄지 않았는지.
    /// 교착이면 판 분석의 신중함을 낮춰(BoardAnalysis stalled) 들어가게 한다.
    /// </summary>
    internal class StallTracker
    {
        private int _lastEnemyHp = int.MaxValue;
        private int _turnsWithoutProgress;
        private int _checkedTurn = -1;

        public bool IsStalled(BotWeights weights) => _turnsWithoutProgress >= weights.StallTurns;

        /// <summary>내 턴이 바뀔 때마다(같은 턴에 여러 번 불러도 한 번만) 적 체력 합을 비교해 카운트를 갱신한다.</summary>
        public void Update(GameController game, Team team)
        {
            if (_checkedTurn == game.State.TurnNumber)
                return;
            _checkedTurn = game.State.TurnNumber;

            int enemyHp = BoardAnalysis.TeamEffectiveHp(game.State, TurnSystem.GetOpponent(team));
            _turnsWithoutProgress = enemyHp < _lastEnemyHp ? 0 : _turnsWithoutProgress + 1;
            _lastEnemyHp = enemyHp;
        }
    }
}
