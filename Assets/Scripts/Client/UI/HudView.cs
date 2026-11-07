using System;
using System.Collections;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Common;
using MiniChess.Client.Interaction;
using MiniChess.Client.Visuals;
using MiniChess.Core.Capture;
using MiniChess.Core.Common;
using MiniChess.Core.Events;
using MiniChess.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 턴 종료 버튼, 상태이상 설명 창, 실패 이유/점령 알림 토스트, 경기 종료 패널. 위 정보 바(TopBar)와 아래 유닛 패널(UnitInfoPanel), 시작 화면 접속 패널(ConnectPanel)은 프리팹으로 띄운다.
    /// GameSession 이벤트를 받아 상태를 다시 그리고, 버튼 입력은 GameSession 에 요청한다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        /// <summary>위 정보 바(TopBar 프리팹의 Bar)와 하단 HUD(UnitInfoPanel 프리팹의 Bottom) 높이. 보드 카메라가 그 사이에 보드를 맞춘다.</summary>
        private const float TopBarHeight = 170f;
        private const float BottomHudHeight = 470f;

        /// <summary>턴 종료 버튼(과 전투 정보 창)의 위쪽 끝. 위 정보 바 바로 아래.</summary>
        private const float EndTurnTop = TopBarHeight + 16f;

        [SerializeField] private float toastDuration = 1.5f;

        private GameSession _session;
        private Func<Team, Color> _teamColor;

        private Canvas _canvas;
        private BoardCameraRig _cameraRig;
        private Button _endTurnButton;
        private CanvasGroup _toastGroup;
        private TextMeshProUGUI _toastText;
        private GameObject _gameOverPanel;
        private TextMeshProUGUI _gameOverText;
        private Coroutine _toastRoutine;
        /// <summary>점령을 알린 경기(GameSession.MatchId). 온라인은 상태마다 GameState 객체가 새로 만들어지므로 번호로 비교한다.</summary>
        private int _captureAnnouncedFor = -1;

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

        /// <summary>화면 크기가 바뀌어도(창 크기, 회전) 보드가 HUD 사이에 들어오도록 카메라에 가림 비율을 알린다.</summary>
        private void LateUpdate()
        {
            if (_cameraRig == null)
                _cameraRig = FindObjectOfType<BoardCameraRig>();
            if (_cameraRig == null || _canvas == null || Screen.height <= 0)
                return;

            float scale = _canvas.scaleFactor / Screen.height;
            _cameraRig.SetScreenInsets(TopBarHeight * scale, BottomHudHeight * scale);
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
            if (state == null)
            {
                // 온라인 상대를 기다리는 중.
                _endTurnButton.interactable = false;
                _gameOverPanel.SetActive(false);
                return;
            }

            AnnounceCapture(state);

            // 이 화면에서 조작하지 않는 팀(봇, 온라인 상대)의 턴이거나 서버 결과 대기/행동 연출 중에는 턴을 끝낼 수 없다.
            _endTurnButton.interactable = state.Phase == GamePhase.Battle && _session.IsLocallyControlled(state.CurrentTeam) && !_session.IsBusy;

            _gameOverPanel.SetActive(state.IsGameOver);
            if (state.IsGameOver)
                _gameOverText.text = state.Winner.HasValue ? $"{TeamLabel(state.Winner.Value)} WINS"
                    : state.EndReason == GameEndReason.Aborted ? "NO CONTEST" : "DRAW";
        }

        /// <summary>점령이 완료되면 한 번 토스트로 알린다(진행도는 TopBar 가 보여준다).</summary>
        private void AnnounceCapture(GameState state)
        {
            CaptureState capture = state.Capture;
            if (!capture.CapturedBy.HasValue || _captureAnnouncedFor == _session.MatchId)
                return;

            _captureAnnouncedFor = _session.MatchId;
            ShowToast($"{TeamLabel(capture.CapturedBy.Value)} CAPTURED  -  {DescribeReward(capture.Reward)}");
        }

        private static string DescribeReward(CaptureReward reward)
        {
            switch (reward)
            {
                case null: return "NO REWARD";
                case ShieldReward shield: return shield.Regenerates
                    ? $"SHIELD {shield.Amount} (ALL UNITS, REFILL EVERY TURN)"
                    : $"SHIELD +{shield.Amount} (ALL UNITS)";
                case BasicAttackStatusReward status: return $"BASIC ATTACK + {status.Status.Id}";
                default: return reward.Id;
            }
        }

        /// <summary>"PLAYER 1", 봇 팀이면 "PLAYER 2 (BOT)".</summary>
        private string TeamLabel(Team team)
        {
            string label = TeamNames.Name(team);
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
            _canvas = canvas;
            Transform root = canvas.transform;

            // 하단은 유닛 패널과 AP 게이지가 쓰므로 턴 종료는 오른쪽 위(정보 바 아래)에 둔다.
            _endTurnButton = CreateFramedButton(root, "EndTurn", "턴 종료", 40f, OnEndTurnClicked);
            UiFactory.Place((RectTransform)_endTurnButton.transform, new Vector2(1f, 1f), new Vector2(-24f, -EndTurnTop), new Vector2(300f, 96f));
            // 전투 정보 창은 정보 바의 버튼으로 여닫고, 열리면 턴 종료 버튼 위를 덮는다.
            BattleInfoView battleInfo = BattleInfoView.Create(root, _session, _teamColor,
                Resources.Load<StatusVisualCatalog>(StatusVisualCatalog.ResourcePath), new Vector2(-24f, -EndTurnTop));

            TopBarView topBar = CreatePrefabPanel<TopBarView>(TopBarView.ResourcePath);
            if (topBar != null)
            {
                topBar.Bind(_session);
                topBar.InfoClicked += battleInfo.Toggle;
            }
            CreatePrefabPanel<UnitInfoPanel>(UnitInfoPanel.ResourcePath)?.Bind(_session, _teamColor);
            PlacementPanelView.Create(root, _session, _session.Placement);
            BuildToast(root);
            BuildGameOverPanel(root);
            CreatePrefabPanel<ConnectPanelView>(ConnectPanelView.ResourcePath)?.Bind(_session);
        }

        /// <summary>어두운 바탕 + 옅은 둥근 테두리 버튼(위 정보 바의 버튼과 같은 모양).</summary>
        private static Button CreateFramedButton(Transform parent, string name, string label, float fontSize, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(parent, name, label, HudPalette.ButtonFill, onClick);
            button.targetGraphic.gameObject.AddComponent<RoundedFrame>().Thickness = 0f;
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = fontSize;

            Image border = UiFactory.CreatePanel(button.transform, "Border", HudPalette.ButtonBorder);
            border.raycastTarget = false;
            UiFactory.Stretch(border.rectTransform);
            border.gameObject.AddComponent<RoundedFrame>().Thickness = 2f;
            border.transform.SetAsFirstSibling();
            return button;
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
            UiFactory.Place(_gameOverText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1000f, 140f));
            _gameOverText.enableAutoSizing = true;
            _gameOverText.fontSizeMax = 96f;
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
