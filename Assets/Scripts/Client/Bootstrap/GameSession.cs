using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Bot;
using MiniChess.Client.Board;
using MiniChess.Client.Common;
using MiniChess.Client.Interaction;
using MiniChess.Client.Players;
using MiniChess.Client.UI;
using MiniChess.Client.Units;
using MiniChess.Client.Visuals;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;
using MiniChess.Core.Presets;
using MiniChess.Core.Setup;
using MiniChess.Core.Skills.Areas;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Bootstrap
{
    /// <summary>
    /// 한 경기의 코어 GameController 를 소유하는 진입점.
    /// 클라이언트는 Game.State 를 읽어 그리기만 하고, 변경은 이 클래스의 Try* 를 거쳐 컨트롤러 요청으로만 한다.
    /// 요청 팀은 현재 턴의 팀이다(한 화면 교대 플레이). 표시용 조회는 Game 의 Get* 를 직접 쓴다.
    /// 뷰 참조가 비어 있으면 자식 오브젝트로 직접 만든다.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        [Header("Players")]
        [SerializeField] private PlayerControl player1Control = PlayerControl.Human;
        [SerializeField] private PlayerControl player2Control = PlayerControl.Bot;
        [Tooltip("봇 행동 사이 간격(초). 행동을 눈으로 따라갈 수 있게 한다.")]
        [SerializeField, Min(0f)] private float botActionDelay = 0.5f;
        [Tooltip("봇 무작위 시드. 0 이면 매 경기 다르게.")]
        [SerializeField] private int botSeed;

        [Header("Quick Start (켜면 배치 단계를 건너뛰고 아래 라인업을 자동 배치)")]
        [SerializeField] private bool quickStart;

        [Header("Quick Start Lineup (비우면 프리셋 기본 구성)")]
        [SerializeField] private List<CharacterChoice> player1Lineup = new List<CharacterChoice>();
        [SerializeField] private List<CharacterChoice> player2Lineup = new List<CharacterChoice>();

        [Header("Visuals (비우면 Resources/CharacterVisualCatalog 를 불러옴)")]
        [SerializeField] private CharacterVisualCatalog characterVisuals;
        [Tooltip("기본 공격 아이콘을 띄우는 시간(초).")]
        [SerializeField, Min(0f)] private float actionIconDuration = 0.7f;

        [Header("View")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private BoardView boardView;
        [SerializeField] private UnitsView unitsView;
        [SerializeField] private BoardCameraRig cameraRig;
        [SerializeField] private BoardInput boardInput;
        [SerializeField] private SelectionController selection;
        [SerializeField] private PlacementController placement;
        [SerializeField] private PlacementPreviewView placementPreview;
        [SerializeField] private HudView hud;
        [SerializeField] private BotDriver botDriver;

        /// <summary>코어 단일 진입점(명령 + 조회). 경기를 만들지 못했으면 null.</summary>
        public GameController Game { get; private set; }

        /// <summary>읽기 전용 경기 상태(Game.State).</summary>
        public GameState State => Game?.State;
        public BoardCoordinates Coordinates { get; private set; }
        public SelectionController Selection => selection;
        public PlacementController Placement => placement;
        /// <summary>캐릭터 얼굴 등 시각 데이터. 없으면 null.</summary>
        public CharacterVisualCatalog CharacterVisuals => characterVisuals;

        public float BotActionDelay => botActionDelay;
        public int BotSeed => botSeed;

        /// <summary>팀을 봇이 조작하는지.</summary>
        public bool IsBot(Team team)
        {
            return (team == Team.Player1 ? player1Control : player2Control) == PlayerControl.Bot;
        }

        /// <summary>새 경기 시작 또는 행동 성공으로 코어 상태가 바뀐 뒤(뷰 동기화 후) 호출된다.</summary>
        public event Action StateChanged;

        /// <summary>행동이 거부되었을 때 화면에 띄울 이유와 함께 호출된다.</summary>
        public event Action<string> ActionFailed;

        private void Awake()
        {
            if (characterVisuals == null) characterVisuals = Resources.Load<CharacterVisualCatalog>(CharacterVisualCatalog.ResourcePath);
            if (boardView == null) boardView = CreateChild<BoardView>("BoardView");
            if (unitsView == null) unitsView = CreateChild<UnitsView>("UnitsView");
            if (cameraRig == null) cameraRig = CreateChild<BoardCameraRig>("CameraRig");
            if (boardInput == null) boardInput = CreateChild<BoardInput>("BoardInput");
            if (selection == null) selection = CreateChild<SelectionController>("Selection");
            if (placement == null) placement = CreateChild<PlacementController>("Placement");
            if (placementPreview == null) placementPreview = CreateChild<PlacementPreviewView>("PlacementPreview");
            if (hud == null) hud = CreateChild<HudView>("Hud");
            if (botDriver == null) botDriver = CreateChild<BotDriver>("BotDriver");
        }

        private void Start()
        {
            // 둘 다 보드 하이라이트를 쓰므로, 상태 변경 시 선택(지우기) → 배치(다시 그리기) 순으로 처리되도록 이 순서로 초기화한다.
            selection.Initialize(this, boardInput, boardView);
            placement.Initialize(this, boardInput, boardView);
            placementPreview.Initialize(placement, () => Coordinates, unitsView.GetTeamColor, id => Game?.State.Characters.Find(id)?.Name ?? id);
            hud.Initialize(this, unitsView.GetTeamColor);
            botDriver.Initialize(this);

            StartNewGame();
        }

        /// <summary>
        /// 새 경기를 시작한다. 기본은 배치 단계부터 시작하고, Quick Start 면 Inspector 라인업을 자동 배치해 바로 Battle 로 시작한다.
        /// </summary>
        public void StartNewGame()
        {
            GameController game;
            try
            {
                game = CreateGame();
            }
            catch (Exception e) when (e is GameConfigException || e is InvalidOperationException)
            {
                // 설정 누락(GameConfigException) 또는 배치 규칙 위반(같은 캐릭터 중복 등, InvalidOperationException)
                Debug.LogError($"[GameSession] 경기를 만들 수 없음: {e.Message}");
                return;
            }

            // 이전 경기의 유닛을 계속 붙잡고 있지 않도록 먼저 해제한다.
            selection.Deselect();

            Game = game;
            Coordinates = new BoardCoordinates(State.Board.Width, State.Board.Height, cellSize);

            boardView.Build(State.Board, Coordinates);
            boardView.SyncEffects(State.Board);
            boardView.SyncStrikes(State);
            unitsView.Build(State, Coordinates, characterVisuals);
            cameraRig.Frame(Coordinates);
            boardInput.Initialize(cameraRig.Camera, Coordinates);

            StateChanged?.Invoke();
        }

        private GameController CreateGame()
        {
            if (!quickStart)
            {
                return GameController.Create(new GameConfig
                {
                    Rules = PrototypeTestPreset.CreateRules(),
                    Map = PrototypeTestPreset.CreateTestMap(),
                    Characters = PrototypeTestPreset.CreateCharacters(),
                    Skills = PrototypeTestPreset.CreateSkills(),
                });
            }

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

        /// <summary>한 팀의 배치를 확정한다. 두 팀이 모두 확정하면 코어가 Battle 을 시작한다.</summary>
        public bool TryPlaceUnits(Team team, IReadOnlyList<UnitPlacement> placements)
        {
            return Handle(Game.PlaceUnits(new PlaceUnitsRequest(team, placements)), FailReasonText.Describe);
        }

        public bool TryMove(Unit unit, Position destination)
        {
            return Handle(Game.Move(new MoveRequest(State.CurrentTeam, unit.Id, destination)), FailReasonText.Describe);
        }

        public bool TryAttack(Unit attacker, Unit target)
        {
            return RequestAttack(new AttackRequest(State.CurrentTeam, attacker.Id, target.Id));
        }

        /// <summary>기본 공격으로 장애물을 친다.</summary>
        public bool TryAttackObstacle(Unit attacker, Position target)
        {
            return RequestAttackObstacle(new AttackObstacleRequest(State.CurrentTeam, attacker.Id, target));
        }

        /// <summary>스킬을 사용한다. 지정 칸은 지정 순, rotation 은 회전하는 범위의 스킬에만 의미가 있다.</summary>
        public bool TryUseSkill(Unit caster, string skillId, IReadOnlyList<Position> targets, AreaRotation rotation = AreaRotation.R0)
        {
            var request = new UseSkillRequest(State.CurrentTeam, caster.Id, skillId, targets, rotation);
            return Handle(Game.UseSkill(request), FailReasonText.Describe);
        }

        public bool TryEndTurn()
        {
            return Handle(Game.EndTurn(new EndTurnRequest(State.CurrentTeam)), FailReasonText.Describe);
        }

        /// <summary>봇이 고른 행동을 사람 입력과 같은 경로(뷰 동기화 포함)로 요청한다. 요청 팀은 행동의 팀이다.</summary>
        public bool TryBotAction(BotAction action)
        {
            switch (action.Kind)
            {
                case BotActionKind.Move:
                    return Handle(Game.Move(new MoveRequest(action.Team, action.UnitId, action.Cell)), FailReasonText.Describe);
                case BotActionKind.Attack:
                    return RequestAttack(new AttackRequest(action.Team, action.UnitId, action.TargetUnitId));
                case BotActionKind.AttackObstacle:
                    return RequestAttackObstacle(new AttackObstacleRequest(action.Team, action.UnitId, action.Cell));
                case BotActionKind.UseSkill:
                    var request = new UseSkillRequest(action.Team, action.UnitId, action.SkillId, action.Targets, action.Rotation);
                    return Handle(Game.UseSkill(request), FailReasonText.Describe);
                case BotActionKind.EndTurn:
                    return Handle(Game.EndTurn(new EndTurnRequest(action.Team)), FailReasonText.Describe);
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, null);
            }
        }

        /// <summary>기본 공격 요청(사람/봇 공용). 성공하면 공격자 위에 기본 공격 아이콘을 띄운다.</summary>
        private bool RequestAttack(AttackRequest request)
        {
            var response = Game.Attack(request);
            if (!Handle(response, FailReasonText.Describe))
                return false;

            ShowBasicAttackIcon(response.Result.Attacker);
            return true;
        }

        /// <summary>장애물 기본 공격 요청(사람/봇 공용). 성공하면 공격자 위에 기본 공격 아이콘을 띄운다.</summary>
        private bool RequestAttackObstacle(AttackObstacleRequest request)
        {
            var response = Game.AttackObstacle(request);
            if (!Handle(response, FailReasonText.Describe))
                return false;

            ShowBasicAttackIcon(response.Result.Attacker);
            return true;
        }

        private void ShowBasicAttackIcon(Unit attacker)
        {
            if (characterVisuals != null)
                unitsView.ShowActionIcon(attacker, characterVisuals.BasicAttackIcon, actionIconDuration);
        }

        /// <summary>성공이면 뷰를 동기화하고, 실패면 사유를 알린다. 실패한 요청은 코어 상태를 바꾸지 않는다.</summary>
        private bool Handle<TReason, TResult>(GameResponse<TReason, TResult> response, Func<TReason, string> describe)
            where TReason : struct, Enum
            where TResult : class
        {
            return response.Success ? Succeed() : Fail(describe(response.Reason));
        }

        private bool Succeed()
        {
            unitsView.Sync();
            boardView.SyncEffects(State.Board);
            boardView.SyncStrikes(State);
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
