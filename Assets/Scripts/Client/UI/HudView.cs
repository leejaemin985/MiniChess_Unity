using System;
using System.Collections;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Interaction;
using MiniChess.Core.Capture;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 턴 종료 버튼, 실패 이유/점령 알림 토스트, 경기 종료 패널. 위 정보 바(TopBar)와 아래 유닛 패널(UnitInfoPanel)은 프리팹으로 띄운다.
    /// GameSession 이벤트를 받아 상태를 다시 그리고, 버튼 입력은 GameSession 에 요청한다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private float toastDuration = 1.5f;

        private GameSession _session;
        private Func<Team, Color> _teamColor;

        private Button _endTurnButton;
        private CanvasGroup _toastGroup;
        private TextMeshProUGUI _toastText;
        private GameObject _gameOverPanel;
        private TextMeshProUGUI _gameOverText;
        private Coroutine _toastRoutine;
        private GameState _captureAnnouncedFor;

        public void Initialize(GameSession session, Func<Team, Color> teamColor)
        {
            Unbind();

            _session = session;
            _teamColor = teamColor;

            if (_endTurnButton == null)
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

            AnnounceCapture(state);

            // 봇 팀의 턴에는 사람이 턴을 끝낼 수 없다.
            _endTurnButton.interactable = state.Phase == GamePhase.Battle && !_session.IsBot(state.CurrentTeam);

            _gameOverPanel.SetActive(state.IsGameOver);
            if (state.IsGameOver)
                _gameOverText.text = state.Winner.HasValue ? $"{TeamLabel(state.Winner.Value)} WINS" : "DRAW";
        }

        /// <summary>점령이 완료되면 한 번 토스트로 알린다(진행도는 TopBar 가 보여준다).</summary>
        private void AnnounceCapture(GameState state)
        {
            CaptureState capture = state.Capture;
            if (!capture.CapturedBy.HasValue || _captureAnnouncedFor == state)
                return;

            _captureAnnouncedFor = state;
            ShowToast($"{TeamLabel(capture.CapturedBy.Value)} CAPTURED  -  {DescribeReward(capture.Reward)}");
        }

        private static string DescribeReward(CaptureReward reward)
        {
            switch (reward)
            {
                case null: return "NO REWARD";
                case ShieldReward shield: return $"SHIELD +{shield.Amount} (ALL UNITS)";
                case BasicAttackStatusReward status: return $"BASIC ATTACK + {status.Status.Id}";
                default: return reward.Id;
            }
        }

        /// <summary>"PLAYER 1", 봇 팀이면 "PLAYER 2 (BOT)".</summary>
        private string TeamLabel(Team team)
        {
            string label = team == Team.Player1 ? "PLAYER 1" : "PLAYER 2";
            return _session.IsBot(team) ? $"{label} (BOT)" : label;
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

            _endTurnButton = UiFactory.CreateButton(root, "EndTurn", "END TURN", new Color(0.15f, 0.15f, 0.18f, 0.9f), OnEndTurnClicked);
            // 하단은 유닛 패널이 쓰므로 턴 종료는 오른쪽 위(정보 바 아래)에 둔다.
            UiFactory.Place((RectTransform)_endTurnButton.transform, new Vector2(1f, 1f), new Vector2(-40f, -160f), new Vector2(280f, 80f));

            CreatePrefabPanel<TopBarView>(TopBarView.ResourcePath)?.Bind(_session, _teamColor);
            CreatePrefabPanel<UnitInfoPanel>(UnitInfoPanel.ResourcePath)?.Bind(_session, _teamColor);
            PlacementPanelView.Create(root, _session, _session.Placement);
            BuildToast(root);
            BuildGameOverPanel(root);
        }

        /// <summary>Resources 의 패널 프리팹을 띄운다. 프리팹은 자체 캔버스라 HUD 캔버스 밖(Hud 오브젝트 아래)에 둔다. 없으면 null.</summary>
        private T CreatePrefabPanel<T>(string resourcePath) where T : Component
        {
            var prefab = Resources.Load<T>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[HudView] 패널 프리팹(Resources/{resourcePath})이 없음");
                return null;
            }

            return Instantiate(prefab, transform);
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
