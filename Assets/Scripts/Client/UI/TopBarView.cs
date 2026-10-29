using System;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 화면 위 정보 바(프리팹 Resources/UI/TopBar): 양 팀의 이름/AP/점령 진행도, 가운데 라운드와 누구 턴인지.
    /// 모양은 프리팹에서 자유롭게 바꾸고, 아래 참조만 유지하면 된다.
    /// 팀 이름과 AP 숫자는 팀 색으로 칠하고, 지금 턴이 아닌 팀은 흐리게 한다.
    /// </summary>
    public class TopBarView : MonoBehaviour
    {
        public const string ResourcePath = "UI/TopBar";

        [Serializable]
        private class TeamSection
        {
            [Tooltip("턴이 아닐 때 흐리게 할 묶음.")]
            public CanvasGroup group;
            [Tooltip("\"PLAYER 1\" 등. 팀 색으로 칠한다.")]
            public TMP_Text label;
            [Tooltip("\"8/12\". 팀 색으로 칠한다.")]
            public TMP_Text ap;
            [Tooltip("점령 진행도. 점령 칸이 없거나 상대가 점령했으면 숨긴다.")]
            public TMP_Text capture;
        }

        [SerializeField] private TeamSection player1;
        [SerializeField] private TeamSection player2;

        [Header("Center")]
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text turnText;

        [SerializeField, Range(0f, 1f)] private float inactiveTeamAlpha = 0.4f;

        private GameSession _session;
        private Func<Team, Color> _teamColor;

        public void Bind(GameSession session, Func<Team, Color> teamColor)
        {
            _session = session;
            _teamColor = teamColor;

            KoreanFont.ApplyToDefaultTexts(gameObject);
            _session.StateChanged += Refresh;
            if (_session.State != null)
                Refresh();
        }

        private void OnDestroy()
        {
            if (_session != null)
                _session.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            GameState state = _session.State;
            if (state == null)
                return;

            RefreshTeam(player1, state, Team.Player1);
            RefreshTeam(player2, state, Team.Player2);
            RefreshCenter(state);
        }

        private void RefreshTeam(TeamSection section, GameState state, Team team)
        {
            Color color = _teamColor(team);
            section.label.text = _session.IsBot(team) ? $"{TeamName(team)} (BOT)" : TeamName(team);
            section.label.color = color;

            ApPool ap = state.GetPlayer(team).Ap;
            section.ap.text = $"{ap.Current}/{ap.Max}";
            section.ap.color = color;

            CaptureState capture = state.Capture;
            bool showCapture = capture.CapturedBy.HasValue ? capture.CapturedBy == team : capture.IsActive;
            section.capture.gameObject.SetActive(showCapture);
            section.capture.text = capture.CapturedBy.HasValue ? "점령 완료" : $"점령 {capture.GetProgress(team)}/{capture.Required}";

            if (section.group != null)
                section.group.alpha = state.Phase == GamePhase.Battle && team != state.CurrentTeam ? inactiveTeamAlpha : 1f;
        }

        private void RefreshCenter(GameState state)
        {
            if (state.IsGameOver)
            {
                roundText.text = $"ROUND {Round(state)}";
                turnText.text = "경기 종료";
                return;
            }

            if (state.Phase == GamePhase.Setup)
            {
                // 온라인은 각자 동시에 배치하므로, 내 배치를 마치면 상대를 기다린다.
                Team? placing = _session.GetPlacingTeam(state);
                roundText.text = "SETUP";
                turnText.text = (placing.HasValue ? $"{WhoseName(placing.Value)} 배치" : _session.IsOnline ? "상대 배치 대기" : "배치") + FormatTimeLeft();
                return;
            }

            roundText.text = $"ROUND {Round(state)}";
            turnText.text = $"{WhoseName(state.CurrentTeam)} 턴{FormatTimeLeft()}";
        }

        private int _shownSeconds = -1;

        /// <summary>온라인 턴(배치) 제한 시간이 1초 줄 때마다 턴 표시를 다시 쓴다.</summary>
        private void Update()
        {
            float? left = _session?.TurnTimeLeft;
            int seconds = left.HasValue ? Mathf.CeilToInt(left.Value) : -1;
            if (seconds == _shownSeconds)
                return;

            _shownSeconds = seconds;
            if (_session.State != null)
                RefreshCenter(_session.State);
        }

        /// <summary>턴(배치) 남은 시간(온라인, 예: "  1:42"). 제한이 없으면 빈 문자열.</summary>
        private string FormatTimeLeft()
        {
            float? left = _session.TurnTimeLeft;
            if (!left.HasValue)
                return string.Empty;

            int seconds = Mathf.CeilToInt(left.Value);
            return $"  {seconds / 60}:{seconds % 60:00}";
        }

        /// <summary>한 라운드 = 양 팀이 한 턴씩. 코어 TurnNumber 는 팀 턴마다 1씩 오른다(1부터).</summary>
        private static int Round(GameState state) => Math.Max(1, (state.TurnNumber + 1) / 2);

        /// <summary>이 화면에서 한 팀만 조작하면(봇 상대, 온라인) "내"/"상대", 아니면(한 화면 교대, 봇끼리) 팀 이름.</summary>
        private string WhoseName(Team team)
        {
            Team other = team == Team.Player1 ? Team.Player2 : Team.Player1;
            bool oneSide = _session.IsLocallyControlled(team) != _session.IsLocallyControlled(other);
            if (!oneSide)
                return TeamName(team);

            return _session.IsLocallyControlled(team) ? "내" : "상대";
        }

        private static string TeamName(Team team) => team == Team.Player1 ? "PLAYER 1" : "PLAYER 2";
    }
}
