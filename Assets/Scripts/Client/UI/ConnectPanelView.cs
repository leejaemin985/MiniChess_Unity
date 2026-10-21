using MiniChess.Client.Bootstrap;
using MiniChess.Client.Common;
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
        [Tooltip("서버 주소 \"IP:포트\"(예: 192.168.100.23:5055). 입력칸에서 Enter 로도 연결한다.")]
        [SerializeField] private TMP_InputField ipInput;
        [SerializeField] private Button connectButton;
        [Tooltip("비우면 로컬 플레이 버튼 없이 쓴다.")]
        [SerializeField] private Button localButton;

        private GameSession _session;

        public void Bind(GameSession session)
        {
            _session = session;

            KoreanFont.ApplyToDefaultTexts(gameObject);
            connectButton.onClick.AddListener(OnConnectClicked);
            ipInput.onSubmit.AddListener(_ => OnConnectClicked());
            if (localButton != null)
                localButton.onClick.AddListener(OnLocalClicked);

            _session.StateChanged += Refresh;
            Refresh();
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
