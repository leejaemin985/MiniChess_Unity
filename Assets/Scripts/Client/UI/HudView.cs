using System;
using System.Collections;
using MiniChess.Client.Bootstrap;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 턴/AP 표시, 턴 종료 버튼, 실패 이유 토스트, 경기 종료 패널.
    /// GameSession 이벤트를 받아 상태를 다시 그리고, 버튼 입력은 GameSession 에 요청한다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private float toastDuration = 1.5f;
        [SerializeField, Range(0f, 1f)] private float inactiveTeamAlpha = 0.4f;

        private GameSession _session;
        private Func<Team, Color> _teamColor;

        private TextMeshProUGUI _turnText;
        private TextMeshProUGUI _player1ApText;
        private TextMeshProUGUI _player2ApText;
        private Button _endTurnButton;
        private CanvasGroup _toastGroup;
        private TextMeshProUGUI _toastText;
        private GameObject _gameOverPanel;
        private TextMeshProUGUI _gameOverText;
        private Coroutine _toastRoutine;

        public void Initialize(GameSession session, Func<Team, Color> teamColor)
        {
            Unbind();

            _session = session;
            _teamColor = teamColor;

            if (_turnText == null)
                Build();

            _session.StateChanged += Refresh;
            _session.ActionFailed += ShowToast;

            if (_session.State != null)
                Refresh();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (_session == null)
                return;

            _session.StateChanged -= Refresh;
            _session.ActionFailed -= ShowToast;
        }

        #region Refresh

        private void Refresh()
        {
            GameState state = _session.State;

            _turnText.text = $"TURN {state.TurnNumber}  -  {TeamLabel(state.CurrentTeam)}";
            _turnText.color = _teamColor(state.CurrentTeam);

            RefreshAp(_player1ApText, state, Team.Player1);
            RefreshAp(_player2ApText, state, Team.Player2);

            _endTurnButton.interactable = state.Phase == GamePhase.Battle;

            _gameOverPanel.SetActive(state.IsGameOver);
            if (state.IsGameOver)
                _gameOverText.text = state.Winner.HasValue ? $"{TeamLabel(state.Winner.Value)} WINS" : "DRAW";
        }

        private void RefreshAp(TextMeshProUGUI text, GameState state, Team team)
        {
            ApPool ap = state.GetPlayer(team).Ap;
            text.text = $"{TeamLabel(team)}\nAP {ap.Current}/{ap.Max}";

            Color color = _teamColor(team);
            color.a = team == state.CurrentTeam ? 1f : inactiveTeamAlpha;
            text.color = color;
        }

        private static string TeamLabel(Team team)
        {
            return team == Team.Player1 ? "PLAYER 1" : "PLAYER 2";
        }

        #endregion

        #region Toast

        private void ShowToast(string message)
        {
            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);

            _toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            _toastText.text = message;
            _toastGroup.alpha = 1f;

            yield return new WaitForSeconds(toastDuration);

            const float fadeTime = 0.3f;
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                _toastGroup.alpha = 1f - t / fadeTime;
                yield return null;
            }

            _toastGroup.alpha = 0f;
            _toastRoutine = null;
        }

        #endregion

        #region Build

        private void Build()
        {
            UiFactory.EnsureEventSystem();
            Canvas canvas = UiFactory.CreateCanvas(transform, "HudCanvas");
            Transform root = canvas.transform;

            _turnText = UiFactory.CreateText(root, "Turn", 48f, TextAlignmentOptions.Center);
            UiFactory.Place(_turnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 70f));
            _turnText.fontStyle = FontStyles.Bold;

            _player1ApText = UiFactory.CreateText(root, "Player1 AP", 40f, TextAlignmentOptions.TopLeft);
            UiFactory.Place(_player1ApText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(400f, 120f));

            _player2ApText = UiFactory.CreateText(root, "Player2 AP", 40f, TextAlignmentOptions.TopRight);
            UiFactory.Place(_player2ApText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(400f, 120f));

            _endTurnButton = UiFactory.CreateButton(root, "EndTurn", "END TURN", new Color(0.15f, 0.15f, 0.18f, 0.9f), OnEndTurnClicked);
            UiFactory.Place((RectTransform)_endTurnButton.transform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(280f, 90f));

            SkillPanelView.Create(root, _session);
            BuildToast(root);
            BuildGameOverPanel(root);
        }

        private void BuildToast(Transform root)
        {
            Image background = UiFactory.CreatePanel(root, "Toast", new Color(0f, 0f, 0f, 0.7f));
            background.raycastTarget = false;
            UiFactory.Place(background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 240f), new Vector2(700f, 70f));

            _toastGroup = background.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;

            _toastText = UiFactory.CreateText(background.transform, "Text", 34f, TextAlignmentOptions.Center);
            UiFactory.Stretch(_toastText.rectTransform);
        }

        private void BuildGameOverPanel(Transform root)
        {
            Image overlay = UiFactory.CreatePanel(root, "GameOver", new Color(0f, 0f, 0f, 0.6f));
            UiFactory.Stretch(overlay.rectTransform);
            _gameOverPanel = overlay.gameObject;

            _gameOverText = UiFactory.CreateText(overlay.transform, "Result", 96f, TextAlignmentOptions.Center);
            UiFactory.Place(_gameOverText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1200f, 140f));
            _gameOverText.fontStyle = FontStyles.Bold;

            Button restart = UiFactory.CreateButton(overlay.transform, "Restart", "RESTART", new Color(0.2f, 0.5f, 0.3f, 1f), OnRestartClicked);
            UiFactory.Place((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(320f, 100f));

            _gameOverPanel.SetActive(false);
        }

        #endregion

        private void OnEndTurnClicked()
        {
            _session.TryEndTurn();
        }

        private void OnRestartClicked()
        {
            _session.StartNewGame();
        }
    }
}
