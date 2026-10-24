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
using MiniChess.Client.Visuals.Effects;
using MiniChess.Core.Actions;
using MiniChess.Core.Combat;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;
using MiniChess.Core.Effects;
using MiniChess.Core.Events;
using MiniChess.Core.Movement;
using MiniChess.Core.Presets;
using MiniChess.Core.Setup;
using MiniChess.Core.Skills.Areas;
using MiniChess.Core.Snapshots;
using MiniChess.Core.State;
using MiniChess.Core.Statuses.Library;
using MiniChess.Protocol;
using UnityEngine;

namespace MiniChess.Client.Bootstrap
{
    /// <summary>
    /// 한 경기의 코어 GameController 를 소유하는 진입점.
    /// 클라이언트는 Game.State 를 읽어 그리기만 하고, 변경은 이 클래스의 Try* 를 거쳐 명령 메시지(Protocol)로만 한다.
    /// 표시용 조회는 Game 의 Get* 를 직접 쓴다. 뷰 참조가 비어 있으면 자식 오브젝트로 직접 만든다.
    ///   로컬: 명령을 실제 경기(Authority, 서버 역할)에 바로 실행한다. 요청 팀은 현재 턴의 팀(한 화면 교대 플레이) 또는 봇 팀이다.
    ///         사람 한 팀 vs 봇이면 온라인과 같은 경로로 사람 팀 시점 스냅샷과 걸러진 이벤트만 화면에 쓴다(숨김 규칙은 GameProjection 한 곳).
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
        [Tooltip("시작 화면의 접속 패널에서 고른 방식으로 바뀐다(연결 = 켬, 로컬 플레이 = 끔).")]
        [SerializeField] private bool online;
        [Tooltip("접속 패널에 입력한 \"IP:포트\" 로 이 주소의 호스트/포트를 바꿔 쓴다(경로 /ws 유지). 포트를 생략하면 이 주소의 포트.")]
        [SerializeField] private string serverUrl = "ws://localhost:5055/ws";
        [Tooltip("서버와 주고받는 메시지를 콘솔에 남긴다.")]
        [SerializeField] private bool logNetwork = true;

        [Header("Quick Start (켜면 배치 단계를 건너뛰고 아래 라인업을 자동 배치)")]
        [SerializeField] private bool quickStart;

        [Header("Quick Start Lineup (비우면 프리셋 기본 구성)")]
        [Tooltip("코어 캐릭터 Id. 목록은 코어 PieceModules 에서 읽고, 검증(없는 Id, 빈 수치)도 코어 QuickBattleFactory 가 한다.")]
        [SerializeField, CoreCharacterId] private List<string> player1Lineup = new List<string>();
        [SerializeField, CoreCharacterId] private List<string> player2Lineup = new List<string>();

        [Header("Visuals (비우면 Resources/CharacterVisualCatalog 를 불러옴)")]
        [SerializeField] private CharacterVisualCatalog characterVisuals;
        [Tooltip("기본 공격/스킬 시전 아이콘을 띄우는 시간(초).")]
        [SerializeField, Min(0f)] private float actionIconDuration = 0.7f;
        [Tooltip("장판 종류별 이펙트 프리팹. 비우면 Resources/FieldEffectCatalog 를 불러온다.")]
        [SerializeField] private FieldEffectCatalog fieldEffects;
        [Tooltip("스킬별 범위 이펙트(일자 베기 등) 프리팹. 비우면 Resources/SkillEffectCatalog 를 불러온다.")]
        [SerializeField] private SkillEffectCatalog skillEffects;

        [Header("Options (비우면 Resources/GameOptions 를 불러오고, 그것도 없으면 코드 기본값)")]
        [SerializeField] private GameOptions gameOptions;

        [Header("View")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private BoardView boardView;
        [SerializeField] private FieldEffectsView fieldEffectsView;
        [SerializeField] private SkillEffectsView skillEffectsView;
        [SerializeField] private StatusEffectsView statusEffectsView;
        [SerializeField] private UnitsView unitsView;
        [SerializeField] private AttackEffectsView attackEffectsView;
        [SerializeField] private BoardCameraRig cameraRig;
        [SerializeField] private BoardInput boardInput;
        [SerializeField] private SelectionController selection;
        [SerializeField] private PlacementController placement;
        [SerializeField] private PlacementPreviewView placementPreview;
        [SerializeField] private HudView hud;
        [SerializeField] private BotDriver botDriver;

        private ServerConnection _connection;
        private int _lastRequestId;

        /// <summary>이펙트 풀 오브젝트의 최상단 부모(씬 루트).</summary>
        private EffectPoolSystem _effectPools;

        /// <summary>유닛별 이펙트 풀 묶음(장판/평타가 함께 쓴다). 새 경기마다 비운다.</summary>
        private EffectCasterRegistry _effectCasters;

        /// <summary>경기 설정(서버/로컬과 같은 프리셋). 시점 스냅샷으로 경기를 다시 만들 때 쓴다(온라인: 서버 스냅샷, 로컬: 내 팀 시점).</summary>
        private GameConfig _viewConfig;

        /// <summary>
        /// 로컬 경기의 실제 경기(판정 전용, 서버 역할). 명령은 여기에 실행한다. 온라인이면 null(판정은 서버).
        /// 화면과 사람의 조회는 Game(시점 경기)을 쓴다.
        /// </summary>
        public GameController Authority { get; private set; }

        /// <summary>서버 결과를 기다리는 내 명령. 없으면 null.</summary>
        private CommandMessage _pendingCommand;

        /// <summary>
        /// 이 화면의 시점 경기(표시와 조회 전용). 경기를 만들지 못했거나 온라인 상대를 기다리는 중이면 null.
        ///   온라인: 서버가 보낸 내 팀 시점 스냅샷으로 만든 경기.
        ///   로컬(사람 한 팀 vs 봇): 실제 경기(Authority)의 사람 팀 시점 스냅샷으로 만든 경기(온라인과 같은 숨김 규칙, GameProjection).
        ///   로컬(한 화면 교대, 봇끼리): 숨길 상대가 없으므로 실제 경기 그대로.
        /// </summary>
        public GameController Game { get; private set; }

        /// <summary>
        /// 로컬에서 정보를 숨겨 보여 줄 팀(사람이 한 팀, 상대가 봇일 때 사람 팀). 한 화면 교대와 봇끼리는 전부 공개라 null(사용자 확정).
        /// </summary>
        private Team? HiddenViewer
        {
            get
            {
                if (online || IsBot(Team.Player1) == IsBot(Team.Player2))
                    return null;

                return IsBot(Team.Player1) ? Team.Player2 : Team.Player1;
            }
        }

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

        /// <summary>시작 화면에서 경기 방식(온라인/로컬)을 골랐는지. 고르기 전에는 경기를 만들지 않는다.</summary>
        public bool IsModeChosen { get; private set; }

        /// <summary>서버 연결이 있는지(접속 시도 중 포함).</summary>
        public bool HasConnection => _connection != null;

        /// <summary>온라인에서 이 클라이언트의 팀. 로컬이거나 아직 경기가 정해지지 않았으면 null.</summary>
        public Team? LocalTeam { get; private set; }

        /// <summary>보낸 명령의 서버 결과를 기다리는 중인지. 기다리는 동안은 새 입력을 받지 않는다.</summary>
        public bool IsAwaitingServer => _pendingCommand != null;

        /// <summary>
        /// 지금 경기의 번호. 새 경기(로컬 새 경기, 온라인 경기 시작, 재접속 대기)마다 바뀐다.
        /// 온라인은 상태가 바뀔 때마다 Game 객체를 새로 만드므로(스냅샷 복원), "같은 경기인지"는 객체가 아니라 이 번호로 판단한다.
        /// </summary>
        public int MatchId { get; private set; }

        /// <summary>현재 턴의 남은 시간(초, 온라인 전용). 시간이 다 되면 서버가 턴을 끝낸다. 턴 제한이 없으면 null.</summary>
        public float? TurnTimeLeft => _turnDeadline.HasValue ? Mathf.Max(0f, _turnDeadline.Value - Time.realtimeSinceStartup) : (float?)null;

        /// <summary>서버가 알려 준 턴 마감 시각(Time.realtimeSinceStartup 기준). 없으면 null.</summary>
        private float? _turnDeadline;

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
            if (gameOptions == null) gameOptions = Resources.Load<GameOptions>(GameOptions.ResourcePath);
            if (gameOptions == null) gameOptions = ScriptableObject.CreateInstance<GameOptions>();
            if (fieldEffects == null) fieldEffects = Resources.Load<FieldEffectCatalog>(FieldEffectCatalog.ResourcePath);
            if (skillEffects == null) skillEffects = Resources.Load<SkillEffectCatalog>(SkillEffectCatalog.ResourcePath);
            if (boardView == null) boardView = CreateChild<BoardView>("BoardView");
            if (fieldEffectsView == null) fieldEffectsView = CreateChild<FieldEffectsView>("FieldEffectsView");
            if (skillEffectsView == null) skillEffectsView = CreateChild<SkillEffectsView>("SkillEffectsView");
            if (statusEffectsView == null) statusEffectsView = CreateChild<StatusEffectsView>("StatusEffectsView");
            if (unitsView == null) unitsView = CreateChild<UnitsView>("UnitsView");
            if (attackEffectsView == null) attackEffectsView = CreateChild<AttackEffectsView>("AttackEffectsView");
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
            _effectPools = EffectPoolSystem.Create();
            _effectCasters = new EffectCasterRegistry(_effectPools);
            fieldEffectsView.Initialize(_effectCasters, fieldEffects);
            attackEffectsView.Initialize(_effectCasters, unitsView);
            skillEffectsView.Initialize(_effectCasters, skillEffects, unitsView);
            statusEffectsView.Initialize(_effectCasters, unitsView);
            boardView.HasFieldVisual = fieldEffectsView.HasVisual;

            // 경기는 시작 화면의 접속 패널에서 방식을 고르면 시작한다(StartOnline / StartLocal).
            StateChanged?.Invoke();
        }

        /// <summary>
        /// 입력한 서버 주소로 접속해 온라인 경기를 시작한다. "IP:포트"(예: 192.168.100.23:5055) 또는 "IP"(포트는 serverUrl 의 것).
        /// 경로(/ws)와 스킴은 serverUrl 을 따른다.
        /// </summary>
        public void StartOnline(string address)
        {
            try
            {
                var builder = new UriBuilder(serverUrl);
                string[] parts = address.Split(':');
                if (parts.Length > 2 || parts[0].Length == 0)
                    throw new UriFormatException();

                builder.Host = parts[0];
                if (parts.Length == 2)
                {
                    if (!int.TryParse(parts[1], out int port) || port <= 0 || port > 65535)
                        throw new UriFormatException();
                    builder.Port = port;
                }

                serverUrl = builder.Uri.ToString();
            }
            catch (UriFormatException)
            {
                ReportFailure($"주소 형식이 잘못되었습니다: {address} (예: 192.168.100.23:5055)");
                return;
            }

            online = true;
            IsModeChosen = true;
            StartNewGame();
        }

        /// <summary>서버 없이 이 화면에서 경기를 시작한다(봇/한 화면 교대).</summary>
        public void StartLocal()
        {
            online = false;
            IsModeChosen = true;
            StartNewGame();
        }

        private void Update()
        {
            if (_connection != null)
                PollServer();

            // 죽은 유닛의 이펙트 풀은 남은 이펙트(장판 등)가 모두 사라진 뒤 지운다.
            if (State != null)
                _effectCasters?.DisposeIdle(unitId => !(State.FindUnit(unitId)?.IsAlive ?? false));
        }

        private void OnDestroy()
        {
            _connection?.Dispose();
            if (_effectPools != null)
                Destroy(_effectPools.gameObject);
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

            Authority = game;
            BeginGame(LocalView());
        }

        /// <summary>로컬 실제 경기의 시점 경기. 숨길 팀이 있으면 그 팀 시점 스냅샷으로 다시 만들고, 없으면 실제 경기 그대로.</summary>
        private GameController LocalView()
        {
            Team? viewer = HiddenViewer;
            if (!viewer.HasValue)
                return Authority;

            _viewConfig = _viewConfig ?? OnlineMatch.CreateConfig();
            return GameController.Restore(_viewConfig, Authority.GetSnapshot(viewer.Value));
        }

        private void BeginGame(GameController game)
        {
            // 이전 경기의 유닛을 계속 붙잡고 있지 않도록 먼저 해제한다.
            selection.Deselect();

            MatchId++;
            Game = game;
            Coordinates =new BoardCoordinates(State.Board.Width, State.Board.Height, cellSize);

            boardView.Build(State.Board, Coordinates);
            boardView.SyncEffects(State.Board);
            boardView.SyncStrikes(State);
            _effectCasters.Clear();
            fieldEffectsView.Build(Coordinates);
            fieldEffectsView.Sync(State);
            unitsView.Build(State, Coordinates, characterVisuals);
            attackEffectsView.Build(Coordinates);
            skillEffectsView.Build(Coordinates);
            CameraSettings cameraSettings = gameOptions.Camera;
            cameraRig.Frame(Coordinates, cameraSettings, cameraSettings.FaceLocalTeam ? GetViewYaw(ViewTeam) : 0f);
            boardInput.Initialize(cameraRig.Camera, Coordinates);

            StateChanged?.Invoke();
        }

        /// <summary>
        /// 화면을 어느 팀 진영 쪽에서 볼지. 온라인은 내 팀, 로컬은 사람이 조작하는 팀이 하나뿐일 때 그 팀.
        /// 한 화면 교대(둘 다 사람)나 봇끼리는 Player1 쪽에서 본다.
        /// </summary>
        private Team ViewTeam
        {
            get
            {
                if (online)
                    return LocalTeam ?? Team.Player1;

                bool player1Human = !IsBot(Team.Player1);
                bool player2Human = !IsBot(Team.Player2);
                return player2Human && !player1Human ? Team.Player2 : Team.Player1;
            }
        }

        /// <summary>
        /// team 의 시작 구역 쪽에서 보드 중심을 바라보는 카메라 yaw(도, 90 단위). 내 진영이 화면 아래쪽에 오게 한다.
        /// 시작 구역이 없거나 보드 중심에 있으면 Player1 0, Player2 180.
        /// </summary>
        private float GetViewYaw(Team team)
        {
            float fallback = team == Team.Player1 ? 0f : 180f;

            IReadOnlyList<Position> spawns = Game.GetPlacementCells(team);
            if (spawns == null || spawns.Count == 0)
                return fallback;

            Vector3 spawnCenter = Vector3.zero;
            foreach (Position position in spawns)
                spawnCenter += Coordinates.ToWorld(position);
            spawnCenter /= spawns.Count;

            // 카메라는 시작 구역 쪽에 서서 보드 중심을 본다 → 바라보는 방향은 (중심 - 시작 구역).
            Vector3 forward = Coordinates.Center - spawnCenter;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return fallback;

            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return Mathf.Round(yaw / 90f) * 90f;
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

        /// <summary>인스펙터 라인업에서 빈 칸("(없음)")을 뺀 구성. 남는 게 없으면 프리셋 기본 구성.</summary>
        private static IReadOnlyList<string> ToLineup(List<string> ids, string[] fallback)
        {
            List<string> lineup = ids?.Where(id => !string.IsNullOrEmpty(id)).ToList();
            return lineup == null || lineup.Count == 0 ? fallback : lineup;
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

        /// <summary>
        /// 명령을 로컬 실제 경기에 실행하고 화면에 반영한다(로컬 전용, 서버 역할).
        /// 숨길 팀이 있으면 온라인과 똑같이 그 팀 시점의 스냅샷과 걸러진 이벤트로 화면을 맞춘다.
        /// </summary>
        private bool Apply(Team team, CommandMessage command)
        {
            CommandOutcome outcome = CommandExecutor.Execute(Authority, team, command);
            if (!outcome.Success)
                return Fail(FailReasonText.Describe(command, outcome.Reason));

            Team? viewer = HiddenViewer;
            if (!viewer.HasValue)
            {
                PlayEvents(outcome.Events);
                return Succeed();
            }

            ShowView(LocalView(), Authority.GetVisibleEvents(viewer.Value, outcome.Events));
            return true;
        }

        /// <summary>
        /// 새 시점 경기로 화면을 바꾼다(온라인 상태 갱신, 로컬 숨김 경기 공용).
        /// 연출이 먼저: 타격 이펙트 위치를 유닛 뷰에서 읽으므로, Rebind 가 죽은 유닛의 뷰를 지우기 전에 재생한다.
        /// </summary>
        private void ShowView(GameController view, IReadOnlyList<GameEvent> events)
        {
            PlayEvents(events);
            Game = view;
            unitsView.Rebind(State);
            Succeed();
        }

        /// <summary>
        /// 일어난 일을 연출한다(로컬/온라인 공용).
        ///   - 기본 공격: 공격자 위에 기본 공격 아이콘, 맞은 자리에 타격 이펙트. 스킬: 시전자 위에 스킬 시전 아이콘, 범위 이펙트(일자 베기 등).
        ///     (유닛 뷰 위치를 쓰므로 뷰를 새 상태로 맞추기 전에 호출한다)
        ///   - 스킬로 시전자가 직접 이동했으면(돌진 등) 그 경로에 경로 이펙트.
        ///   - 스킬로 시전자가 유닛을 순간이동시켰으면(실 관문 등) 워프 이펙트, 그 유닛은 관문으로 가라앉았다 솟아오른다.
        ///   - 칸을 거치는 이동(일반 이동, 돌진, 밀려남/당겨짐)은 순간이동 대신 거친 칸을 차례로 지나간다(워프/위치 교환은 바로).
        ///   - 장애물 공격: 깔린 장애물 이펙트(실 그물 등)가 출렁인다.
        ///   - 상태가 새로 걸리거나 풀림: 그 유닛 자리에서 걸림/풀림 이펙트(봉인 → 마법석 문양/파편). 몸체가 굳고 깨지는 연출은 UnitView 가 상태대로 맞춘다.
        ///   - 호위자가 피해를 대신 받음: 보호 대상 → 호위자로 호위 반응 이펙트.
        ///   - 덫 발동, 포격 착탄: 그 칸을 잠깐 표시한다(온라인에서 숨겨져 있던 상대 덫/조준 칸이 이때 드러난다).
        /// </summary>
        private void PlayEvents(IEnumerable<GameEvent> events)
        {
            IReadOnlyList<GameEvent> list = events as IReadOnlyList<GameEvent> ?? events.ToList();
            PlayMoves(list);
            for (int i = 0; i < list.Count; i++)
            {
                GameEvent gameEvent = list[i];
                if (gameEvent is SkillUsedEvent cast)
                {
                    PlaySkillPath(list, i, cast);
                    PlaySkillWarps(list, i, cast);
                }

                switch (gameEvent)
                {
                    case UnitAttackedEvent attacked:
                        ShowActionIcon(attacked.AttackerId, characterVisuals?.BasicAttackIcon);
                        attackEffectsView.PlayOnUnit(characterVisuals?.BasicAttackEffect, attacked.AttackerId, attacked.TargetId);
                        break;
                    case ObstacleHitEvent hit:
                        ShowActionIcon(hit.AttackerId, characterVisuals?.BasicAttackIcon);
                        attackEffectsView.PlayOnCell(characterVisuals?.BasicAttackEffect, hit.AttackerId, hit.Position);
                        fieldEffectsView.PlayReaction(hit.Position, CellEffectLayer.Obstacle);
                        break;
                    case SkillUsedEvent used:
                        ShowActionIcon(used.CasterId, characterVisuals?.SkillCastIcon);
                        skillEffectsView.Play(used.CasterId, used.SkillId, used.AffectedCells);
                        skillEffectsView.PlayCast(used.CasterId, used.SkillId);
                        skillEffectsView.PlayHit(used.CasterId, used.SkillId, used.AffectedCells);
                        Dictionary<Position, Position> pushes = FindPushes(list, i);
                        skillEffectsView.PlayProjectile(used.CasterId, used.SkillId, used.AffectedCells, pushes);
                        skillEffectsView.PlayPush(used.CasterId, used.SkillId, used.Targets, pushes);
                        break;
                    case CellEffectTriggeredEvent triggered when triggered.OwnerTeam.HasValue:
                        boardView.RevealTrap(triggered.Position, triggered.OwnerTeam.Value);
                        fieldEffectsView.PlayTrigger(triggered.Kind, triggered.Position, triggered.OwnerTeam);
                        break;
                    case StatusAppliedEvent applied when !applied.Refreshed:
                        statusEffectsView.PlayApplied(applied.Status.StatusId, applied.TargetId);
                        break;
                    case StatusRemovedEvent removed when removed.Reason != StatusRemoveReason.Replaced:
                        statusEffectsView.PlayRemoved(removed.StatusId, removed.TargetId);
                        break;
                    case UnitDamagedEvent damaged when damaged.Type == DamageType.Redirected:
                        PlayGuardReaction(list, i, damaged);
                        break;
                    case StrikeLandedEvent landed:
                        boardView.RevealStrike(landed.Strike.Cells, landed.Strike.SourceTeam);
                        skillEffectsView.PlayStrike(landed.Strike.SourceId, landed.Strike.SkillId, landed.Strike.Cells);
                        break;
                }
            }
        }

        /// <summary>
        /// events[castIndex] 의 스킬 사용 뒤(다음 스킬 사용 전까지) 시전자가 일으킨 순간이동마다 워프 이펙트(실 관문 등)를 재생하고,
        /// 이펙트가 있으면 옮겨지는 유닛을 출발 칸으로 가라앉혔다가 도착 칸에서 솟아오르게 한다(없으면 원래대로 바로 옮겨짐).
        /// </summary>
        private void PlaySkillWarps(IReadOnlyList<GameEvent> events, int castIndex, SkillUsedEvent used)
        {
            for (int i = castIndex + 1; i < events.Count && !(events[i] is SkillUsedEvent); i++)
            {
                if (!(events[i] is UnitMovedEvent moved) || moved.Kind != MoveKind.Warp || moved.InitiatorId != used.CasterId)
                    continue;

                WarpEffect effect = skillEffectsView.PlayWarp(used.CasterId, used.SkillId, moved.From, moved.To);
                if (effect != null)
                    unitsView.PlayPortal(moved.UnitId, moved.From, moved.To, effect.SinkDelay, effect.SinkTime, effect.RiseTime);
            }
        }

        /// <summary>
        /// events[castIndex] 의 스킬 사용 뒤(다음 스킬 사용 전까지) 밀려나거나 당겨진 유닛의 처음 칸 → 마지막 도착 칸.
        /// 이동 이벤트는 칸마다 기록되므로 유닛마다 첫 출발 칸과 마지막 도착 칸을 잇는다.
        /// </summary>
        private static Dictionary<Position, Position> FindPushes(IReadOnlyList<GameEvent> events, int castIndex)
        {
            var starts = new Dictionary<int, Position>();
            var ends = new Dictionary<int, Position>();
            for (int i = castIndex + 1; i < events.Count && !(events[i] is SkillUsedEvent); i++)
            {
                if (events[i] is UnitMovedEvent moved && (moved.Kind == MoveKind.Knockback || moved.Kind == MoveKind.Pull))
                {
                    if (!starts.ContainsKey(moved.UnitId))
                        starts[moved.UnitId] = moved.From;
                    ends[moved.UnitId] = moved.To;
                }
            }

            var pushes = new Dictionary<Position, Position>();
            foreach (KeyValuePair<int, Position> start in starts)
                pushes[start.Value] = ends[start.Key];
            return pushes;
        }

        /// <summary>
        /// 호위자가 피해를 대신 받았으면(Redirected) 보호 대상 → 호위자로 호위 반응 이펙트를 재생한다.
        /// 보호 대상은 이 뒤에 기록되는 같은 출처의 직접 피해 대상이다(호위 가로채기가 원래 피해 처리 도중에 일어나므로).
        /// </summary>
        private void PlayGuardReaction(IReadOnlyList<GameEvent> events, int redirectIndex, UnitDamagedEvent redirected)
        {
            for (int i = redirectIndex + 1; i < events.Count; i++)
            {
                if (events[i] is UnitDamagedEvent original && original.Type == DamageType.Direct && original.SourceId == redirected.SourceId)
                {
                    statusEffectsView.PlayReaction(StatusLibrary.GuardId, original.TargetId, redirected.TargetId);
                    return;
                }
            }
        }

        /// <summary>
        /// events[castIndex] 의 스킬 사용 뒤(다음 스킬 사용 전까지) 시전자가 스스로 이동한 경로가 있으면 경로 이펙트를 재생하고,
        /// 돌진이면 유닛을 미끄러지듯 옮긴다(이동 이벤트는 칸마다 기록되므로 첫 출발 칸 → 마지막 도착 칸).
        /// </summary>
        private void PlaySkillPath(IReadOnlyList<GameEvent> events, int castIndex, SkillUsedEvent used)
        {
            UnitMovedEvent first = null;
            UnitMovedEvent last = null;
            for (int i = castIndex + 1; i < events.Count && !(events[i] is SkillUsedEvent); i++)
            {
                if (events[i] is UnitMovedEvent moved && moved.UnitId == used.CasterId && moved.IsVoluntary)
                {
                    first = first ?? moved;
                    last = moved;
                }
            }

            if (first == null)
                return;

            skillEffectsView.PlayPath(used.CasterId, used.SkillId, first.From, last.To);
        }

        /// <summary>
        /// 이번에 일어난 이동을 유닛마다 모아, 순간이동 대신 거친 칸을 차례로 지나가게 한다(일반 이동, 돌진, 밀려남/당겨짐).
        /// 워프/위치 교환이 섞인 유닛은 원래 뜻대로 바로 옮긴다(연출 없음). 돌진만으로 이동한 유닛은 돌진 속도로 지나고,
        /// 밀려남/당겨짐이 섞인 유닛은 타격 연출이 닿을 때까지 기다렸다가 감속하며 미끄러진다.
        /// </summary>
        private void PlayMoves(IReadOnlyList<GameEvent> events)
        {
            var paths = new Dictionary<int, (List<Position> Cells, bool Dash, bool Knockback, bool Instant)>();
            foreach (GameEvent gameEvent in events)
            {
                if (!(gameEvent is UnitMovedEvent moved))
                    continue;

                if (!paths.TryGetValue(moved.UnitId, out var path))
                    path = (new List<Position>(), true, false, false);

                bool instant = moved.Kind == MoveKind.Warp || moved.Kind == MoveKind.Swap;
                path.Cells.Add(moved.To);
                bool knockback = moved.Kind == MoveKind.Knockback || moved.Kind == MoveKind.Pull;
                paths[moved.UnitId] = (path.Cells, path.Dash && moved.Kind == MoveKind.Dash, path.Knockback || knockback, path.Instant || instant);
            }

            foreach (KeyValuePair<int, (List<Position> Cells, bool Dash, bool Knockback, bool Instant)> pair in paths)
            {
                if (pair.Value.Instant)
                    continue;

                UnitMoveStyle style = pair.Value.Knockback ? UnitMoveStyle.Knockback : pair.Value.Dash ? UnitMoveStyle.Dash : UnitMoveStyle.Walk;
                unitsView.PlayMove(pair.Key, pair.Value.Cells, style);
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
            fieldEffectsView.Sync(State);
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
            _connection = null;
            _pendingCommand = null;
            _lastRequestId = 0;
            _turnDeadline = null;
            LocalTeam = null;
            selection.Deselect();
            MatchId++;
            Authority = null;
            Game = null;
            StateChanged?.Invoke();

            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri uri) || (uri.Scheme != "ws" && uri.Scheme != "wss"))
            {
                ReportFailure($"서버 주소가 잘못되었습니다: {serverUrl}");
                return;
            }

            var connection = new ServerConnection();
            _connection = connection;
            StateChanged?.Invoke();
            ReportFailure("서버에 접속하는 중...");
            LogNetwork($"connecting to {uri}");

            try
            {
                await connection.ConnectAsync(uri);
                if (connection != _connection || connection.IsClosed)
                    return;

                await connection.SendAsync(new JoinMatchMessage { ProtocolVersion = ProtocolInfo.Version, ConfigHash = OnlineMatch.ConfigHash() });
                ReportFailure("상대를 기다리는 중...");
            }
            catch (Exception e)
            {
                // async void 라 여기서 잡지 않으면 예외가 Unity 로 그대로 빠져나간다.
                Debug.LogException(e);
                if (connection == _connection)
                    DropConnection("서버에 접속하지 못했습니다");
            }
        }

        /// <summary>연결을 끊고 안내를 띄운다(복원 실패 등 계속할 수 없는 상태).</summary>
        private void DropConnection(string message)
        {
            LogNetwork($"dropping connection: {message}");
            _connection?.Dispose();
            _connection = null;
            _pendingCommand = null;
            _turnDeadline = null;
            ReportFailure(message);
            StateChanged?.Invoke();
        }

        /// <summary>받은 서버 메시지를 메인 스레드에서 처리한다.</summary>
        private void PollServer()
        {
            ServerConnection connection = _connection;
            while (connection == _connection && connection.TryReceive(out ServerMessage message))
            {
                if (message is PongMessage)
                    continue;

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

            if (connection == null || connection != _connection)
                return;

            if (connection.IsClosed)
            {
                LogNetwork($"connection closed: {connection.CloseReason ?? "normal"}");
                _connection = null;
                _pendingCommand = null;
                _turnDeadline = null;
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

            _turnDeadline = null;
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
                .Select(ProtocolJson.ReadEvent)
                .Where(gameEvent => gameEvent != null)
                .ToList();

            _turnDeadline = update.TurnTimeLeftMs.HasValue ? Time.realtimeSinceStartup + update.TurnTimeLeftMs.Value / 1000f : (float?)null;
            ShowView(game, events);

            foreach (GameEndedEvent ended in events.OfType<GameEndedEvent>())
            {
                if (ended.Reason == GameEndReason.Disconnect && ended.Winner == LocalTeam)
                    ReportFailure("상대의 연결이 끊겼습니다");
                else if (ended.Reason == GameEndReason.Aborted)
                    ReportFailure("서버 오류로 경기가 무효 처리되었습니다");
            }
        }

        /// <summary>
        /// 서버 스냅샷으로 경기를 만든다(표시/조회 전용). 만들 수 없으면 화면이 서버와 어긋난 채 멈추므로,
        /// 연결을 끊고 안내를 띄운 뒤 null 을 돌려준다.
        /// </summary>
        private GameController RestoreOnline(GameSnapshot snapshot)
        {
            try
            {
                _viewConfig = _viewConfig ?? OnlineMatch.CreateConfig();
                return OnlineMatch.Restore(_viewConfig, snapshot);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSession] 서버 상태를 복원할 수 없음: {e}");
                DropConnection("서버 상태를 화면에 옮기지 못했습니다. 다시 접속하세요");
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
