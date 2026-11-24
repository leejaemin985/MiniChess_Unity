using System;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Common;
using MiniChess.Client.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 시작 화면의 접속 패널(프리팹 Resources/UI/ConnectPanel): 서버 IP 입력칸 + 연결 버튼 + 로컬 플레이 버튼.
    /// 경기 방식을 고르기 전, 그리고 온라인 연결이 끊겨 진행 중인 경기가 없을 때 보인다.
    /// 모양은 프리팹에서 자유롭게 바꾸고, 아래 참조만 유지하면 된다.
    /// </summary>
    public class ConnectPanelView : MonoBehaviour
    {
        public const string ResourcePath = "UI/ConnectPanel";

        [Tooltip("보이고 숨길 묶음(캔버스 아래 패널).")]
        [SerializeField] private GameObject panel;
        [Tooltip("서버 주소 \"IP:포트\"(예: 192.168.100.23:5055) 또는 전체 주소(wss://.../ws). 입력칸에서 Enter 로도 연결한다.")]
        [SerializeField] private TMP_InputField ipInput;
        [SerializeField] private Button connectButton;
        [Tooltip("비우면 로컬 플레이 버튼 없이 쓴다.")]
        [SerializeField] private Button localButton;

        private GameSession _session;

        /// <param name="openLocalSettings">로컬 경기 설정 창을 여는 동작. 주면 로컬 플레이 버튼 옆에 설정 버튼을 만든다.</param>
        public void Bind(GameSession session, Action openLocalSettings = null)
        {
            _session = session;

            KoreanFont.ApplyToDefaultTexts(gameObject);

            // 웹 빌드: 페이지 주소의 ?server=wss://... 를 미리 채운다(브라우저에서는 입력칸 붙여넣기가 불편하다).
            string server = ServerConnection.GetPageQueryParam("server");
            if (!string.IsNullOrEmpty(server))
                ipInput.text = server;

            connectButton.onClick.AddListener(OnConnectClicked);
            ipInput.onSubmit.AddListener(_ => OnConnectClicked());
            if (localButton != null)
            {
                localButton.onClick.AddListener(OnLocalClicked);
                if (openLocalSettings != null)
                    CreateSettingsButton(openLocalSettings);
            }

            _session.StateChanged += Refresh;
            Refresh();
        }

        /// <summary>
        /// 로컬 플레이 버튼을 복제해 오른쪽에 설정 버튼을 둔다(같은 모양). 두 버튼이 원래 로컬 플레이 버튼 자리를 나눠 쓴다.
        /// 프리팹은 그대로 두고 코드로만 만든다.
        /// </summary>
        private void CreateSettingsButton(Action openLocalSettings)
        {
            const float gap = 20f;
            const float settingsWidth = 200f;

            var localRect = (RectTransform)localButton.transform;
            Vector2 size = localRect.sizeDelta;
            Vector2 center = localRect.anchoredPosition;
            float total = size.x + gap + settingsWidth;
            float left = center.x - total * 0.5f;

            Button settings = Instantiate(localButton, localRect.parent);
            settings.name = "LocalSettings";
            settings.onClick = new Button.ButtonClickedEvent();
            settings.onClick.AddListener(() => openLocalSettings());
            TextMeshProUGUI label = settings.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = "설정";

            localRect.anchoredPosition = new Vector2(left + size.x * 0.5f, center.y);
            var settingsRect = (RectTransform)settings.transform;
            settingsRect.sizeDelta = new Vector2(settingsWidth, size.y);
            settingsRect.anchoredPosition = new Vector2(left + size.x + gap + settingsWidth * 0.5f, center.y);
        }

        private void OnDestroy()
        {
            if (_session != null)
                _session.StateChanged -= Refresh;
        }

        /// <summary>방식을 고르기 전, 또는 온라인 연결이 없고 진행 중인(끝나지 않은) 경기가 있거나 경기가 없을 때 보인다.</summary>
        private void Refresh()
        {
            bool disconnected = _session.IsOnline && !_session.HasConnection && (_session.State == null || !_session.State.IsGameOver);
            panel.SetActive(!_session.IsModeChosen || disconnected);
        }

        private void OnConnectClicked()
        {
            string address = ipInput.text.Trim();
            if (address.Length == 0)
            {
                _session.ReportFailure("서버 주소를 입력하세요 (예: 192.168.100.23:5055)");
                return;
            }

            _session.StartOnline(address);
        }

        private void OnLocalClicked()
        {
            _session.StartLocal();
        }
    }
}
