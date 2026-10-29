using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MiniChess.Bot;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Interaction;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Players
{
    /// <summary>
    /// 봇 팀을 대신 조작한다. 배치 단계에서는 봇 팀의 배치를 요청하고, 봇 팀의 턴에는 행동을 하나씩
    /// (BotActionDelay 간격으로) 받아 GameSession 을 통해 요청한다. 사람 입력과 같은 경로라 뷰도 그대로 갱신된다.
    /// 판단은 로컬 실제 경기(GameSession.Authority)로 한다.
    /// 봇의 결정(무엇을 할지)은 MiniChess.Bot(GameSession 의 팀별 난이도로 BotFactory 가 만든 봇)이 하고, 여기서는 진행 타이밍만 맡는다.
    /// </summary>
    public class BotDriver : MonoBehaviour
    {
        private GameSession _session;
        private GameController _game;
        private int _matchId = -1;
        private readonly Dictionary<Team, BotPlayer> _bots = new Dictionary<Team, BotPlayer>();
        private bool _running;

        /// <summary>진행 중인 봇 판단(별도 스레드)의 취소. 경기가 바뀌거나 이 컴포넌트가 사라지면 취소한다.</summary>
        private CancellationTokenSource _thinking;

        public void Initialize(GameSession session)
        {
            if (_session != null)
                _session.StateChanged -= OnStateChanged;

            _session = session;
            _session.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            CancelThinking();
            if (_session != null)
                _session.StateChanged -= OnStateChanged;
        }

        private void CancelThinking()
        {
            _thinking?.Cancel();
            _thinking = null;
        }

        private void OnStateChanged()
        {
            // 새 경기면(객체가 아니라 경기 번호로 판단) 진행 중이던 봇 처리를 멈추고 봇을 새로 만든다.
            // 봇은 로컬 경기에서만 돌며, 실제 경기(Authority)를 본다. 화면용 Game 은 사람 팀 시점으로 매번 새로 만들어지지만
            // Authority 는 경기 동안 바뀌지 않는다(진행 중 확인은 game == _game 으로 한다).
            // 봇이 상대에게 숨겨진 정보(사람 팀의 덫, 포격 조준 칸)를 쓰지 않는 것은 봇 규칙이 GameVisibility 로 거른다.
            if (_session.MatchId != _matchId)
            {
                StopAllCoroutines();
                CancelThinking();
                _running = false;
                _matchId = _session.MatchId;
                _bots.Clear();
            }

            _game = _session.Authority;

            // 봇이 요청한 행동도 StateChanged 를 일으키므로, 진행 중에는 여기서 새로 시작하지 않는다.
            if (!_running)
                TryStart();
        }

        /// <summary>지금 봇이 할 일이 있으면(봇 팀 배치 또는 봇 팀 턴) 진행을 시작한다.</summary>
        private void TryStart()
        {
            GameState state = _game?.State;
            if (state == null || state.IsGameOver)
                return;

            if (state.Phase == GamePhase.Setup)
            {
                Team? placing = PlacementController.GetNextPlacingTeam(state);
                if (placing.HasValue && _session.IsBot(placing.Value))
                    StartCoroutine(PlaceRoutine(placing.Value));
                return;
            }

            if (state.Phase == GamePhase.Battle && _session.IsBot(state.CurrentTeam))
                StartCoroutine(TurnRoutine(state.CurrentTeam));
        }

        private IEnumerator PlaceRoutine(Team team)
        {
            _running = true;
            GameController game = _game;

            yield return new WaitForSeconds(_session.BotActionDelay);

            if (game == _game)
            {
                IReadOnlyList<UnitPlacement> placements = GetBot(team).DecidePlacement(game, team);
                if (!_session.TryPlaceUnits(team, placements))
                    Debug.LogError($"[BotDriver] {team} 봇 배치가 거부됨");
            }

            Finish(game);
        }

        private IEnumerator TurnRoutine(Team team)
        {
            _running = true;
            GameController game = _game;
            BotPlayer bot = GetBot(team);

            while (game == _game && !game.State.IsGameOver && game.State.CurrentTeam == team)
            {
                yield return new WaitForSeconds(_session.BotActionDelay);
                if (game != _game)
                    break;

                // 수 읽기(깊이 2~3)는 오래 걸릴 수 있어 별도 스레드에서 한다. 봇은 실제 경기를 읽고 복사본만 바꾸며,
                // 봇 차례에는 봇의 요청 말고는 실제 경기가 바뀌지 않으므로 읽기가 겹쳐도 안전하다.
                // 경기가 바뀌면(새 경기, 다시 시작) 코루틴은 멈추고 이 판단은 취소되어 일찍 끝난다(결과는 버린다).
                _thinking = new CancellationTokenSource();
                CancellationToken cancellation = _thinking.Token;
                Task<BotAction> decision = Task.Run(() => bot.DecideNext(game, team, cancellation));
                while (!decision.IsCompleted)
                    yield return null;
                _thinking = null;
                if (game != _game)
                    break;

                if (decision.IsFaulted)
                {
                    Debug.LogError($"[BotDriver] 봇 판단 중 오류: {decision.Exception?.GetBaseException()}. 턴을 종료합니다.");
                    _session.TryBotAction(BotAction.EndTurn(team));
                    break;
                }

                BotAction action = decision.Result;
                if (action == null)
                    break;

                if (!_session.TryBotAction(action))
                {
                    // 봇이 고른 행동이 거부되면 봇 버그다. 경기가 멈추지 않도록 턴을 넘긴다.
                    Debug.LogError($"[BotDriver] 봇 행동 거부: {action}. 턴을 종료합니다.");
                    _session.TryBotAction(BotAction.EndTurn(team));
                    break;
                }

                if (action.Kind == BotActionKind.EndTurn)
                    break;
            }

            Finish(game);
        }

        /// <summary>진행을 끝내고, 같은 경기에서 이어서 봇 차례가 있으면(봇끼리 대전 등) 다시 시작한다.</summary>
        private void Finish(GameController game)
        {
            _running = false;
            if (game == _game)
                TryStart();
        }

        private BotPlayer GetBot(Team team)
        {
            if (!_bots.TryGetValue(team, out BotPlayer bot))
            {
                int seed = _session.BotSeed != 0 ? _session.BotSeed + (int)team : Random.Range(int.MinValue, int.MaxValue);
                bot = BotFactory.Create(_session.GetBotDifficulty(team), seed, _session.GetBotSearch(team).ToSettings());
                _bots[team] = bot;
            }

            return bot;
        }
    }
}
