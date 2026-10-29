using System.Collections.Generic;
using System.Threading;
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

        /// <summary>
        /// DecideNext 와 같되, cancellation 이 취소되면 오래 걸리는 판단(수 읽기)을 그때까지 본 것으로 바로 끝낸다.
        /// 경기를 나가거나 다시 시작해 결과가 필요 없어졌을 때 계산을 멈추는 용도다.
        /// </summary>
        public BotAction DecideNext(GameController game, Team team, CancellationToken cancellation)
        {
            Cancellation = cancellation;
            try
            {
                return DecideNext(game, team);
            }
            finally
            {
                Cancellation = CancellationToken.None;
            }
        }

        /// <summary>지금 판단을 멈춰야 하는지. 오래 걸리는 봇은 이 값을 보고 판단을 일찍 끝낸다.</summary>
        protected CancellationToken Cancellation { get; private set; }

        /// <summary>다음 행동을 고른다. 턴을 끝내려면 null 또는 EndTurn.</summary>
        protected abstract BotAction Choose(GameController game, Team team);
    }
}
