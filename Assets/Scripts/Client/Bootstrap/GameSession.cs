using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Client.Board;
using MiniChess.Client.Common;
using MiniChess.Client.Interaction;
using MiniChess.Client.UI;
using MiniChess.Client.Units;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Data;
using MiniChess.Core.Presets;
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
        [Header("Lineup (비우면 프리셋 기본 구성)")]
        [SerializeField] private List<CharacterChoice> player1Lineup = new List<CharacterChoice>();
        [SerializeField] private List<CharacterChoice> player2Lineup = new List<CharacterChoice>();

        [Header("View")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private BoardView boardView;
        [SerializeField] private UnitsView unitsView;
        [SerializeField] private BoardCameraRig cameraRig;
        [SerializeField] private BoardInput boardInput;
        [SerializeField] private SelectionController selection;
        [SerializeField] private HudView hud;

        public GameState State { get; private set; }
        public BoardCoordinates Coordinates { get; private set; }
        public SelectionController Selection => selection;

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

        /// <summary>Inspector 의 팀 구성으로 새 경기를 시작한다(재시작 시 바뀐 구성도 반영).</summary>
        public void StartNewGame()
        {
            GameState state;
            try
            {
                state = CreateGame();
            }
            catch (GameConfigException e)
            {
                Debug.LogError($"[GameSession] 경기를 만들 수 없음: {e.Message}");
                return;
            }

            // 이전 경기의 유닛을 계속 붙잡고 있지 않도록 먼저 해제한다.
            selection.Deselect();

            State = state;
            Coordinates = new BoardCoordinates(State.Board.Width, State.Board.Height, cellSize);

            boardView.Build(State.Board, Coordinates);
            boardView.SyncEffects(State.Board);
            unitsView.Build(State, Coordinates);
            cameraRig.Frame(Coordinates);
            boardInput.Initialize(cameraRig.Camera, Coordinates);

            StateChanged?.Invoke();
        }

        private GameState CreateGame()
        {
            string[] defaultLineup = PrototypeTestPreset.CreateStartingLineup();

            return QuickBattleFactory.Create(
                PrototypeTestPreset.CreateRules(),
                PrototypeTestPreset.CreateTestMap(),
                PrototypeTestPreset.CreateCharacters(),
                ToLineup(player1Lineup, defaultLineup),
                ToLineup(player2Lineup, defaultLineup),
                PrototypeTestPreset.CreateSkills());
        }

        private static IReadOnlyList<string> ToLineup(List<CharacterChoice> choices, string[] fallback)
        {
            return choices == null || choices.Count == 0
                ? fallback
                : choices.Select(choice => choice.ToString()).ToList();
        }

        /// <summary>행동과 무관한 안내(예: 스킬 지정 불가)를 거부 메시지와 같은 경로로 띄운다.</summary>
        public void ReportFailure(string message)
        {
            ActionFailed?.Invoke(message);
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

        /// <summary>기본 공격으로 장애물을 친다.</summary>
        public bool TryAttackObstacle(Unit attacker, Position target)
        {
            var action = new AttackObstacleAction(attacker, target);
            AttackFailReason reason = action.Validate(State);
            if (reason != AttackFailReason.None)
                return Fail(FailReasonText.Describe(reason));

            action.Execute(State);
            return Succeed();
        }

        public bool TryUseSkill(Unit caster, string skillId, Position target)
        {
            return TryUseSkill(caster, skillId, new[] { target });
        }

        /// <summary>여러 칸을 지정하는 스킬(지정 순).</summary>
        public bool TryUseSkill(Unit caster, string skillId, IReadOnlyList<Position> targets)
        {
            var action = new UseSkillAction(caster, skillId, targets);
            SkillFailReason reason = action.Validate(State);
            if (reason != SkillFailReason.None)
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
            boardView.SyncEffects(State.Board);
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
