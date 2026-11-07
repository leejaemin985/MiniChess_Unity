using System;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 화면 위 정보 바(프리팹 Resources/UI/TopBar): 가운데 라운드와 누구 턴인지, 그 아래 남은 턴 시간 게이지, 오른쪽 전투 정보 버튼.
    /// 모양은 프리팹에서 자유롭게 바꾸고, 아래 참조만 유지하면 된다.
    /// 팀 AP 는 여기 두지 않는다(내 팀 AP 는 하단 AP 게이지, 상대 AP 는 전투 정보 창).
    /// 시간 게이지는 제한 시간이 있을 때(온라인)만 보이고, 초 숫자는 마지막 lastSeconds 초에만 보인다.
    /// 서버는 남은 시간만 보내므로 턴이 바뀐 뒤 처음 받은 남은 시간을 그 턴의 전체 시간으로 삼는다(턴 도중 재접속하면 그때부터 가득 찬 게이지).
    /// </summary>
    public class TopBarView : MonoBehaviour
    {
        public const string ResourcePath = "UI/TopBar";

        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text turnText;

        [Header("Turn Timer")]
        [Tooltip("시간 게이지 묶음(배경 포함). 제한 시간이 없으면 숨긴다.")]
        [SerializeField] private GameObject timeGauge;
        [Tooltip("남은 시간 비율만큼 가로로 채워지는 막대(앵커로 늘린다).")]
        [SerializeField] private Image timeFill;
        [Tooltip("남은 초. 마지막 lastSeconds 초에만 보인다.")]
        [SerializeField] private TMP_Text timeText;
        [SerializeField, Min(0)] private int lastSeconds = 5;
        [SerializeField] private Color timeColor = new Color(0.92f, 0.8f, 0.49f);
        [Tooltip("마지막 lastSeconds 초의 게이지/숫자 색.")]
        [SerializeField] private Color urgentColor = new Color(1f, 0.42f, 0.36f);

        [Tooltip("전투 정보 창(상대 AP, 점령, 상태이상 설명)을 여닫는 버튼.")]
        [SerializeField] private Button infoButton;

        private GameSession _session;
        /// <summary>시간 게이지의 기준(지금 턴의 전체 시간, 초)과 그 턴. 턴이 바뀌면 다시 잡는다.</summary>
        private float _turnTotal;
        private (GamePhase Phase, int Turn, Team Team)? _timedTurn;
        private int _shownSeconds = -1;

        /// <summary>전투 정보 버튼을 눌렀다.</summary>
        public event Action InfoClicked;

        public void Bind(GameSession session)
        {
            _session = session;

            KoreanFont.ApplyToDefaultTexts(gameObject);
            infoButton.onClick.AddListener(() => InfoClicked?.Invoke());
            _session.StateChanged += Refresh;
            if (_session.State != null)
                Refresh();
            else
                RefreshTimer();
        }

        private void OnDestroy()
        {
            if (_session != null)
                _session.StateChanged -= Refresh;
        }

        private void Update()
        {
            RefreshTimer();
        }

        private void Refresh()
        {
            GameState state = _session.State;
            if (state == null)
                return;

            if (state.IsGameOver)
            {
                roundText.text = $"{Round(state)} 라운드";
                turnText.text = "경기 종료";
                return;
            }

            if (state.Phase == GamePhase.Setup)
            {
                // 온라인은 각자 동시에 배치하므로, 내 배치를 마치면 상대를 기다린다.
                Team? placing = _session.GetPlacingTeam(state);
                roundText.text = "배치";
                turnText.text = placing.HasValue ? $"{WhoseName(placing.Value)} 배치" : _session.IsOnline ? "상대 배치 대기" : "배치";
                return;
            }

            roundText.text = $"{Round(state)} 라운드";
            turnText.text = $"{WhoseName(state.CurrentTeam)} 턴";
        }

        /// <summary>게이지는 매 프레임, 숫자는 1초 줄 때마다 다시 쓴다.</summary>
        private void RefreshTimer()
        {
            GameState state = _session?.State;
            float? left = _session?.TurnTimeLeft;
            bool visible = left.HasValue && state != null && !state.IsGameOver;
            timeGauge.SetActive(visible);
            if (!visible)
            {
                _timedTurn = null;
                SetSeconds(-1);
                return;
            }

            var turn = (state.Phase, state.TurnNumber, state.CurrentTeam);
            if (_timedTurn != turn)
            {
                _timedTurn = turn;
                _turnTotal = left.Value;
            }
            _turnTotal = Mathf.Max(_turnTotal, left.Value);

            int seconds = Mathf.CeilToInt(left.Value);
            bool urgent = seconds <= lastSeconds;
            RectTransform fill = timeFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(_turnTotal > 0f ? Mathf.Clamp01(left.Value / _turnTotal) : 0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            timeFill.color = urgent ? urgentColor : timeColor;
            SetSeconds(urgent ? seconds : -1);
        }

        private void SetSeconds(int seconds)
        {
            if (seconds == _shownSeconds)
                return;

            _shownSeconds = seconds;
            timeText.gameObject.SetActive(seconds >= 0);
            timeText.text = seconds.ToString();
            timeText.color = urgentColor;
        }

        /// <summary>한 라운드 = 양 팀이 한 턴씩. 코어 TurnNumber 는 팀 턴마다 1씩 오른다(1부터).</summary>
        public static int Round(GameState state) => Math.Max(1, (state.TurnNumber + 1) / 2);

        private string WhoseName(Team team) => TeamNames.Whose(_session, team);
    }
}
