using System;
using MiniChess.Client.Board;
using MiniChess.Client.Common;
using MiniChess.Client.Interaction;
using MiniChess.Client.UI;
using MiniChess.Client.Units;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Setup;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Bootstrap
{
    /// <summary>
    /// 한 경기의 코어 GameState 를 소유하는 진입점.
    /// 클라이언트는 이 상태를 읽어 그리기만 하고, 변경은 이 클래스의 Try* 를 거쳐 코어 Action 으로만 한다.
    /// 뷰 참조가 비어 있으면 자식 오브젝트로 직접 만든다.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private BoardView boardView;
        [SerializeField] private UnitsView unitsView;
        [SerializeField] private BoardCameraRig cameraRig;
        [SerializeField] private BoardInput boardInput;
        [SerializeField] private SelectionController selection;
        [SerializeField] private HudView hud;

        public GameState State { get; private set; }
        public BoardCoordinates Coordinates { get; private set; }

        /// <summary>새 경기 시작 또는 행동 성공으로 코어 상태가 바뀐 뒤(뷰 동기화 후) 호출된다.</summary>
        public event Action StateChanged;

        /// <summary>행동이 거부되었을 때 화면에 띄울 이유와 함께 호출된다.</summary>
        public event Action<string> ActionFailed;

        private void Awake()
        {
            if (boardView == null) boardView = CreateChild<BoardView>("BoardView");
            if (unitsView == null) unitsView = CreateChild<UnitsView>("UnitsView");
            if (cameraRig == null) cameraRig = CreateChild<BoardCameraRig>("CameraRig");
            if (boardInput == null) boardInput = CreateChild<BoardInput>("BoardInput");
            if (selection == null) selection = CreateChild<SelectionController>("Selection");
            if (hud == null) hud = CreateChild<HudView>("Hud");
        }

        private void Start()
        {
            selection.Initialize(this, boardInput, boardView);
            hud.Initialize(this, unitsView.GetTeamColor);

            StartNewGame();
        }

        public void StartNewGame()
        {
            // 이전 경기의 유닛을 계속 붙잡고 있지 않도록 먼저 해제한다.
            selection.Deselect();

            State = QuickBattleFactory.CreateDefault();
            Coordinates = new BoardCoordinates(State.Board.Width, State.Board.Height, cellSize);

            boardView.Build(State.Board, Coordinates);
            unitsView.Build(State, Coordinates);
            cameraRig.Frame(Coordinates);
            boardInput.Initialize(cameraRig.Camera, Coordinates);

            StateChanged?.Invoke();
        }

        #region Actions

        public bool TryMove(Unit unit, Position destination)
        {
            var action = new MoveAction(unit, destination);
            MoveFailReason reason = action.Validate(State);
            if (reason != MoveFailReason.None)
                return Fail(FailReasonText.Describe(reason));

            action.Execute(State);
            return Succeed();
        }

        public bool TryAttack(Unit attacker, Unit target)
        {
            var action = new AttackAction(attacker, target);
            AttackFailReason reason = action.Validate(State);
            if (reason != AttackFailReason.None)
                return Fail(FailReasonText.Describe(reason));

            action.Execute(State);
            return Succeed();
        }

        public bool TryEndTurn()
        {
            var action = new EndTurnAction(State.CurrentTeam);
            EndTurnFailReason reason = action.Validate(State);
            if (reason != EndTurnFailReason.None)
                return Fail(FailReasonText.Describe(reason));

            action.Execute(State);
            return Succeed();
        }

        private bool Succeed()
        {
            unitsView.Sync();
            StateChanged?.Invoke();
            return true;
        }

        private bool Fail(string message)
        {
            ActionFailed?.Invoke(message);
            return false;
        }

        #endregion

        private T CreateChild<T>(string childName) where T : Component
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<T>();
        }
    }
}
