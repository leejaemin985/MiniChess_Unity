using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Bot;
using MiniChess.Client.Board;
using MiniChess.Client.Common;
using MiniChess.Client.Interaction;
using MiniChess.Client.Network;
using MiniChess.Client.Players;
using MiniChess.Client.UI;
using MiniChess.Client.Units;
using MiniChess.Client.Visuals;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;
using MiniChess.Core.Events;
using MiniChess.Core.Presets;
using MiniChess.Core.Setup;
using MiniChess.Core.Skills.Areas;
using MiniChess.Core.Snapshots;
using MiniChess.Core.State;
using MiniChess.Protocol;
using UnityEngine;

namespace MiniChess.Client.Bootstrap
{
    /// <summary>
    /// 한 경기의 코어 GameController 를 소유하는 진입점.
    /// 클라이언트는 Game.State 를 읽어 그리기만 하고, 변경은 이 클래스의 Try* 를 거쳐 명령 메시지(Protocol)로만 한다.
    /// 표시용 조회는 Game 의 Get* 를 직접 쓴다. 뷰 참조가 비어 있으면 자식 오브젝트로 직접 만든다.
    ///   로컬: 명령을 바로 실행한다. 요청 팀은 현재 턴의 팀(한 화면 교대 플레이) 또는 봇 팀이다.
    ///   온라인: 명령을 서버에 보내기만 한다. 서버가 상태가 바뀔 때마다 보내는 내 팀 시점의 스냅샷으로 경기를 새로 만들어(GameController.Restore)
    ///          화면과 조회에 쓴다. 상대에게 숨겨진 정보(상대 덫, 포격 조준 칸)는 스냅샷에 없으므로 이 클라이언트도 알 수 없다.
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

        [Header("Online (켜면 서버에 접속해 상대와 대전. 봇/Quick Start 는 쓰지 않는다)")]
        [SerializeField] private bool online;
        [SerializeField] private string serverUrl = "ws://localhost:5055/ws";
        [Tooltip("서버와 주고받는 메시지를 콘솔에 남긴다.")]
        [SerializeField] private bool logNetwork = true;

        [Header("Quick Start (켜면 배치 단계를 건너뛰고 아래 라인업을 자동 배치)")]
        [SerializeField] private bool quickStart;

        [Header("Quick Start Lineup (비우면 프리셋 기본 구성)")]
        [SerializeField] private List<CharacterChoice> player1Lineup = new List<CharacterChoice>();
        [SerializeField] private List<CharacterChoice> player2Lineup = new List<CharacterChoice>();

        [Header("Visuals (비우면 Resources/CharacterVisualCatalog 를 불러옴)")]
        [SerializeField] private CharacterVisualCatalog characterVisuals;
        [Tooltip("기본 공격/스킬 시전 아이콘을 띄우는 시간(초).")]
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

        private ServerConnection _connection;
        private int _lastRequestId;

        /// <summary>온라인 경기의 설정(서버와 같은 프리셋). 서버 스냅샷으로 경기를 다시 만들 때 쓴다.</summary>
        private GameConfig _onlineConfig;

        /// <summary>서버 결과를 기다리는 내 명령. 없으면 null.</summary>
        private CommandMessage _pendingCommand;

        /// <summary>코어 단일 진입점(명령 + 조회). 경기를 만들지 못했거나 온라인 상대를 기다리는 중이면 null.</summary>
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

        /// <summary>서버에 접속해 대전하는 중인지.</summary>
        public bool IsOnline => online;

        /// <summary>온라인에서 이 클라이언트의 팀. 로컬이거나 아직 경기가 정해지지 않았으면 null.</summary>
        public Team? LocalTeam { get; private set; }

        /// <summary>보낸 명령의 서버 결과를 기다리는 중인지. 기다리는 동안은 새 입력을 받지 않는다.</summary>
        public bool IsAwaitingServer => _pendingCommand != null;

        /// <summary>팀을 봇이 조작하는지. 온라인에서는 봇을 쓰지 않는다.</summary>
        public bool IsBot(Team team)
        {
            return !online && (team == Team.Player1 ? player1Control : player2Control) == PlayerControl.Bot;
        }

        /// <summary>이 화면의 사람이 조작하는 팀인지(로컬: 봇이 아닌 팀, 온라인: 내 팀).</summary>
        public bool IsLocallyControlled(Team team)
        {
            return online ? LocalTeam == team : !IsBot(team);
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

        private void Update()
        {
            if (_connection != null)
                PollServer();
        }

        private void OnDestroy()
        {
            _connection?.Dispose();
        }

        /// <summary>
        /// 새 경기를 시작한다. 온라인이면 서버에 접속해 상대를 기다린다.
        /// 로컬은 배치 단계부터 시작하고, Quick Start 면 Inspector 라인업을 자동 배치해 바로 Battle 로 시작한다.
        /// </summary>
        public void StartNewGame()
        {
            if (online)
            {
                Connect();
                return;
            }

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

            BeginGame(game);
        }

        private void BeginGame(GameController game)
        {
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

        /// <summary>
        /// 지금 배치할 팀. 로컬은 선공 팀부터 한 팀씩, 온라인은 내 팀(아직 배치하지 않았을 때)만. 배치할 팀이 없으면 null.
        /// 봇 팀일 수 있으므로 사람 입력에 쓸 때는 IsLocallyControlled 로 거른다.
        /// </summary>
        public Team? GetPlacingTeam(GameState state)
        {
            if (state == null || state.Phase != GamePhase.Setup)
                return null;

            if (online)
                return LocalTeam.HasValue && !state.GetPlayer(LocalTeam.Value).HasPlaced ? LocalTeam : null;

            return PlacementController.GetNextPlacingTeam(state);
        }

        #region Actions

        /// <summary>한 팀의 배치를 확정한다. 두 팀이 모두 확정하면 코어가 Battle 을 시작한다.</summary>
        public bool TryPlaceUnits(Team team, IReadOnlyList<UnitPlacement> placements)
        {
            return Submit(team, new PlaceUnitsMessage
            {
                Placements = placements.Select(p => new PlacementEntry { CharacterId = p.CharacterId, Position = p.Position }).ToList(),
            });
        }

        public bool TryMove(Unit unit, Position destination)
        {
            return Submit(ActingTeam, new MoveMessage { UnitId = unit.Id, Destination = destination });
        }

        public bool TryAttack(Unit attacker, Unit target)
        {
            return Submit(ActingTeam, new AttackMessage { UnitId = attacker.Id, TargetUnitId = target.Id });
        }

        /// <summary>기본 공격으로 장애물을 친다.</summary>
        public bool TryAttackObstacle(Unit attacker, Position target)
        {
            return Submit(ActingTeam, new AttackObstacleMessage { UnitId = attacker.Id, Target = target });
        }

        /// <summary>스킬을 사용한다. 지정 칸은 지정 순, rotation 은 회전하는 범위의 스킬에만 의미가 있다.</summary>
        public bool TryUseSkill(Unit caster, string skillId, IReadOnlyList<Position> targets, AreaRotation rotation = AreaRotation.R0)
        {
            return Submit(ActingTeam, new UseSkillMessage { UnitId = caster.Id, SkillId = skillId, Targets = targets.ToList(), Rotation = rotation });
        }

        public bool TryEndTurn()
        {
            return Submit(ActingTeam, new EndTurnMessage());
        }

        /// <summary>봇이 고른 행동을 사람 입력과 같은 경로(뷰 동기화 포함)로 요청한다. 요청 팀은 행동의 팀이다.</summary>
        public bool TryBotAction(BotAction action)
        {
            switch (action.Kind)
            {
                case BotActionKind.Move:
                    return Submit(action.Team, new MoveMessage { UnitId = action.UnitId, Destination = action.Cell });
                case BotActionKind.Attack:
                    return Submit(action.Team, new AttackMessage { UnitId = action.UnitId, TargetUnitId = action.TargetUnitId });
                case BotActionKind.AttackObstacle:
                    return Submit(action.Team, new AttackObstacleMessage { UnitId = action.UnitId, Target = action.Cell });
                case BotActionKind.UseSkill:
                    return Submit(action.Team, new UseSkillMessage
                    {
                        UnitId = action.UnitId, SkillId = action.SkillId, Targets = action.Targets.ToList(), Rotation = action.Rotation,
                    });
                case BotActionKind.EndTurn:
                    return Submit(action.Team, new EndTurnMessage());
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, null);
            }
        }

        /// <summary>사람 입력의 요청 팀. 로컬은 현재 턴의 팀, 온라인은 내 팀(서버가 연결 기준으로 다시 정한다).</summary>
        private Team ActingTeam => online && LocalTeam.HasValue ? LocalTeam.Value : State.CurrentTeam;

        /// <summary>
        /// 명령을 보낸다. 로컬은 바로 실행해 성공 여부를 돌려준다.
        /// 온라인은 서버로 보내고 true 를 돌려준다(결과는 나중에 온다: 실패는 토스트, 성공은 CommandApplied 로 반영).
        /// </summary>
        private bool Submit(Team team, CommandMessage command)
        {
            if (Game == null)
                return false;

            command.TurnNumber = State.TurnNumber;
            if (!online)
                return Apply(team, command);

            if (_connection == null || _connection.IsClosed)
                return Fail("서버에 연결되어 있지 않습니다");
            if (_pendingCommand != null)
                return Fail("이전 요청을 처리하는 중입니다");

            command.RequestId = ++_lastRequestId;
            _pendingCommand = command;
            LogNetwork($"=> {MessageTypes.NameOf(command.GetType())} #{command.RequestId} ({team}, turn {command.TurnNumber})");
            _ = _connection.SendAsync(command);
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>명령을 이 클라이언트의 경기에 실행하고 화면에 반영한다(로컬 전용).</summary>
        private bool Apply(Team team, CommandMessage command)
        {
            CommandOutcome outcome = CommandExecutor.Execute(Game, team, command);
            if (!outcome.Success)
                return Fail(FailReasonText.Describe(command, outcome.Reason));

            PlayEvents(outcome.Events);
            return Succeed();
        }

        /// <summary>
        /// 일어난 일을 연출한다(로컬/온라인 공용).
        ///   - 기본 공격: 공격자 위에 기본 공격 아이콘. 스킬: 시전자 위에 스킬 시전 아이콘.
        ///   - 덫 발동, 포격 착탄: 그 칸을 잠깐 표시한다(온라인에서 숨겨져 있던 상대 덫/조준 칸이 이때 드러난다).
        /// </summary>
        private void PlayEvents(IEnumerable<GameEvent> events)
        {
            foreach (GameEvent gameEvent in events)
            {
                switch (gameEvent)
                {
                    case UnitAttackedEvent attacked:
                        ShowActionIcon(attacked.AttackerId, characterVisuals?.BasicAttackIcon);
                        break;
                    case ObstacleHitEvent hit:
                        ShowActionIcon(hit.AttackerId, characterVisuals?.BasicAttackIcon);
                        break;
                    case SkillUsedEvent used:
                        ShowActionIcon(used.CasterId, characterVisuals?.SkillCastIcon);
                        break;
                    case CellEffectTriggeredEvent triggered when triggered.OwnerTeam.HasValue:
                        boardView.RevealTrap(triggered.Position, triggered.OwnerTeam.Value);
                        break;
                    case StrikeLandedEvent landed:
                        boardView.RevealStrike(landed.Strike.Cells, landed.Strike.SourceTeam);
                        break;
                }
            }
        }

        private void ShowActionIcon(int unitId, Sprite icon)
        {
            if (icon != null)
                unitsView.ShowActionIcon(State.FindUnit(unitId), icon, actionIconDuration);
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

        #region Online

        private async void Connect()
        {
            _connection?.Dispose();
            _pendingCommand = null;
            _lastRequestId = 0;
            LocalTeam = null;
            selection.Deselect();
            Game = null;
            StateChanged?.Invoke();

            var connection = new ServerConnection();
            _connection = connection;
            ReportFailure("서버에 접속하는 중...");
            LogNetwork($"connecting to {serverUrl}");

            await connection.ConnectAsync(new Uri(serverUrl));
            if (connection != _connection || connection.IsClosed)
                return;

            await connection.SendAsync(new JoinMatchMessage { ProtocolVersion = ProtocolInfo.Version });
            ReportFailure("상대를 기다리는 중...");
        }

        /// <summary>받은 서버 메시지를 메인 스레드에서 처리한다.</summary>
        private void PollServer()
        {
            ServerConnection connection = _connection;
            while (connection == _connection && connection.TryReceive(out ServerMessage message))
            {
                LogNetwork($"<= {Describe(message)}");
                switch (message)
                {
                    case MatchStartedMessage started:
                        OnMatchStarted(started);
                        break;
                    case CommandResultMessage result:
                        OnCommandResult(result);
                        break;
                    case StateUpdateMessage update:
                        OnStateUpdate(update);
                        break;
                    case ErrorMessage error:
                        ReportFailure(error.Message);
                        break;
                    case null:
                        Debug.LogError("[GameSession] 해석할 수 없는 서버 메시지");
                        break;
                }
            }

            if (connection == _connection && connection.IsClosed)
            {
                LogNetwork($"connection closed: {connection.CloseReason ?? "normal"}");
                _connection = null;
                _pendingCommand = null;
                if (State == null || !State.IsGameOver)
                    ReportFailure(connection.CloseReason != null ? $"서버 연결 실패: {connection.CloseReason}" : "서버 연결이 끊겼습니다");
                StateChanged?.Invoke();
            }
        }

        private void LogNetwork(string text)
        {
            if (logNetwork)
                Debug.Log($"[Net] {text}");
        }

        /// <summary>로그용 한 줄 요약.</summary>
        private static string Describe(ServerMessage message)
        {
            switch (message)
            {
                case null: return "(해석할 수 없는 메시지)";
                case MatchStartedMessage started: return $"MatchStarted: {started.YourTeam}";
                case CommandResultMessage result: return $"CommandResult #{result.RequestId}: {(result.Success ? "ok" : result.Reason)}";
                case StateUpdateMessage update:
                    return $"StateUpdate (turn {update.Snapshot?.TurnNumber}, last event {update.Snapshot?.LastEventSequence}): "
                        + string.Join(", ", (update.Events ?? new List<EventEnvelope>()).Select(e => e.Type));
                case ErrorMessage error: return $"Error: {error.Message}";
                default: return MessageTypes.NameOf(message.GetType());
            }
        }

        private void OnMatchStarted(MatchStartedMessage started)
        {
            GameController game = RestoreOnline(started.Snapshot);
            if (game == null)
                return;

            LocalTeam = started.YourTeam;
            BeginGame(game);
            ReportFailure($"경기 시작: {(started.YourTeam == Team.Player1 ? "PLAYER 1" : "PLAYER 2")}");
        }

        private void OnCommandResult(CommandResultMessage result)
        {
            if (_pendingCommand == null || result.RequestId != _pendingCommand.RequestId)
                return;

            CommandMessage command = _pendingCommand;
            _pendingCommand = null;
            if (!result.Success)
                Fail(FailReasonText.Describe(command, result.Reason));
            StateChanged?.Invoke();
        }

        /// <summary>서버의 상태 변화: 받은 스냅샷으로 경기를 새로 만들어 화면을 맞추고, 이벤트로 연출한다.</summary>
        private void OnStateUpdate(StateUpdateMessage update)
        {
            if (Game == null)
                return;

            GameController game = RestoreOnline(update.Snapshot);
            if (game == null)
                return;

            List<GameEvent> events = (update.Events ?? new List<EventEnvelope>())
                .Select(ClientProtocolJson.ReadEvent)
                .Where(gameEvent => gameEvent != null)
                .ToList();

            Game = game;
            unitsView.Rebind(State);
            PlayEvents(events);
            Succeed();

            if (events.OfType<GameEndedEvent>().Any(ended => ended.Reason == GameEndReason.Disconnect && ended.Winner == LocalTeam))
                ReportFailure("상대의 연결이 끊겼습니다");
        }

        /// <summary>서버 스냅샷으로 경기를 만든다(표시/조회 전용). 만들 수 없으면 오류를 남기고 null.</summary>
        private GameController RestoreOnline(GameSnapshot snapshot)
        {
            try
            {
                _onlineConfig = _onlineConfig ?? OnlineMatch.CreateConfig();
                return OnlineMatch.Restore(_onlineConfig, snapshot);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError($"[GameSession] 서버 상태를 복원할 수 없음: {e.Message}");
                return null;
            }
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
