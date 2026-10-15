using System.Collections.Generic;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.State;

namespace MiniChess.Bot
{
    /// <summary>
    /// 봇 플레이어의 공통 진입점. 클라/헤드리스 실행 모두 이 클래스만 쓴다.
    ///   - 배치: DecidePlacement 결과로 PlaceUnits 를 요청한다.
    ///   - 전투: 자기 턴 동안 DecideNext 를 반복해 행동을 하나씩 받아 요청하고, EndTurn 이 나오면 턴이 끝난다.
    /// 봇은 상태를 바꾸지 않는다. 요청은 호출하는 쪽이 한다(클라는 뷰 동기화 경로로, 헤드리스는 BotAction.Execute 로).
    /// </summary>
    public abstract class BotPlayer
    {
        /// <summary>한 턴에 고를 수 있는 행동 수 상한. 넘으면 턴을 끝낸다(무한 반복 방지).</summary>
        public int MaxActionsPerTurn { get; set; } = 40;

        private int _trackedTurn = -1;
        private int _actionsThisTurn;

        /// <summary>팀의 배치(캐릭터와 시작 칸). 배치 규칙(인원, 중복 금지, 자기 시작 칸)을 지킨다.</summary>
        public abstract IReadOnlyList<UnitPlacement> DecidePlacement(GameController game, Team team);

        /// <summary>
        /// 자기 턴에 다음으로 할 행동. 할 것이 없거나 상한에 닿으면 EndTurn.
        /// 자기 턴이 아니면 null.
        /// </summary>
        public BotAction DecideNext(GameController game, Team team)
        {
            GameState state = game.State;
            if (state.Phase != GamePhase.Battle || state.CurrentTeam != team)
                return null;

            if (_trackedTurn != state.TurnNumber)
            {
                _trackedTurn = state.TurnNumber;
                _actionsThisTurn = 0;
            }

            if (_actionsThisTurn >= MaxActionsPerTurn)
                return BotAction.EndTurn(team);

            _actionsThisTurn++;
            return Choose(game, team) ?? BotAction.EndTurn(team);
        }

        /// <summary>다음 행동을 고른다. 턴을 끝내려면 null 또는 EndTurn.</summary>
        protected abstract BotAction Choose(GameController game, Team team);
    }
}
