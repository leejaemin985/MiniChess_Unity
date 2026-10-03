using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniChess
{
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private Board board;
        [SerializeField] private Camera battleCamera;
        [SerializeField, Tooltip("Costs, shield amount and AP rules. Empty uses the built-in defaults. Applies on Restart.")]
        private BattleRulesAsset battleRules = null;
        [SerializeField, Tooltip("Draft pool. Each side draws one unit per position (근접/수호/원거리/제어). "
            + "Empty uses the built-in nine units.")]
        private UnitDefinitionAsset[] unitPool = new UnitDefinitionAsset[0];
        [SerializeField, Tooltip("Selectable bot difficulties. The HUD button cycles through them in Play Mode.")]
        private BotSettingsAsset[] botDifficulties = new BotSettingsAsset[0];
        [SerializeField, Min(0), Tooltip("Which entry of Bot Difficulties to start on.")]
        private int botDifficultyIndex;
        private BotSettings botSettings = new BotSettings();
        private CancellationTokenSource botCancellation;
        private Task<BotPlan> botTask;
        private BotPlan botPlan;
        private int botActionIndex;
        // Re-searches allowed inside one bot turn after a hidden trap spoils the plan.
        private const int MaxBotReplans = 3;
        private int botReplans;
        private float nextBotActionTime;
        private GameState botMatch;
        private int botTurnNumber;
        private readonly List<Unit> views = new List<Unit>();
        public Board Board => board;
        public Camera BattleCamera => battleCamera;
        public IReadOnlyList<Unit> Views => views;
        public GameState State => Turns?.State;
        public TurnSystem Turns { get; private set; }
        public BattleSystem Battle { get; private set; }
        public DeploymentSystem Deployment { get; private set; }
        public bool IsBotTurn => State?.Phase == GamePhase.Battle && State.CurrentPlayer == PlayerId.PlayerTwo;
        public bool IsBotThinking => botTask != null;
        public event Action StateChanged;

        private void Start()
        {
            if (board == null) board = GetComponent<Board>();
            if (board == null) board = FindObjectOfType<Board>();
            if (board == null || board.BoardSize < 4)
            {
                Debug.LogError("A board of at least 4 x 4 is required.", this);
                enabled = false;
                return;
            }
            if (battleCamera == null) battleCamera = Camera.main;
            if (battleCamera == null)
                battleCamera = new GameObject("Battle Camera", typeof(Camera)).GetComponent<Camera>();
            RestartMatch();
            var ui = GetComponent<PrototypeUI>();
            if (ui == null) ui = gameObject.AddComponent<PrototypeUI>();
            ui.Initialize(this);
        }

        [ContextMenu("Restart Match")]
        public void RestartMatch()
        {
            if (!Application.isPlaying || board == null) return;
            CancelBot();
            if (Turns != null) Turns.StateChanged -= OnStateChanged;
            ClearViews();
            botSettings = CreateBotSettings();
            var draftRandom = new System.Random();
            List<UnitDefinition> pool = BuildPool();
            // Each side draws one unit per position; the bot also picks its own tiles.
            UnitDefinition[] playerRoster = UnitDraft.Draft(pool, draftRandom);
            var state = new GameState(board.BoardSize,
                battleRules != null ? battleRules.CreateRules() : new BattleRules());
            UnitDraft.PlaceRandomly(state, PlayerId.PlayerTwo, UnitDraft.Draft(pool, draftRandom), draftRandom);
            Turns = new TurnSystem(state);
            Battle = new BattleSystem(Turns);
            Deployment = new DeploymentSystem(State, playerRoster);
            Turns.StateChanged += OnStateChanged;
            SyncViews();
            OnStateChanged();
        }

        // Inspector assets win; otherwise the built-in roster supplies every position.
        private List<UnitDefinition> BuildPool()
        {
            var pool = new List<UnitDefinition>();
            if (unitPool != null)
                foreach (UnitDefinitionAsset asset in unitPool)
                    if (asset != null) pool.Add(asset.CreateDefinition());
            if (pool.Count == 0) pool.AddRange(UnitDefinitions.Pool);
            return pool;
        }

        // Redraws Player One's roster so the player can reroll before placing. The
        // obstacle layout is the player's own work, so a reroll keeps it; a wall the
        // newly placed bot happens to stand on is the only one that cannot come back.
        public void RedraftPlayerRoster()
        {
            if (!Application.isPlaying || State == null || State.Phase != GamePhase.Deployment) return;
            var kept = new List<int>(State.Obstacles);
            int boardSize = State.BoardSize;
            int keptCapture = State.CaptureTile;
            RestartMatch();
            if (keptCapture >= 0) State.TrySetCapturePoint(keptCapture % boardSize, keptCapture / boardSize);
            foreach (int tile in kept) State.TryAddObstacle(tile % boardSize, tile / boardSize);
            BeginDeployment();
        }

        private void SyncViews()
        {
            for (int player = 0; player < 2; player++)
                for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                {
                    UnitState state = State.GetUnit((PlayerId)player, index);
                    if (state == null || views.Exists(view => view != null && view.State == state)) continue;
                    // Shape reads the position at a glance now that no unit is a leader.
                    GameObject obj = GameObject.CreatePrimitive(ShapeFor(state.Definition.Position));
                    Unit view = AddUnitComponent(obj, state.Definition.Kind);
                    view.Initialize(state, board);
                    obj.transform.localScale = Vector3.one * board.CellSize * 0.6f;
                    var color = player == 0 ? new Color(0.18f, 0.55f, 1f) : new Color(1f, 0.32f, 0.25f);
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_Color", color);
                    block.SetColor("_BaseColor", color);
                    obj.GetComponent<Renderer>().SetPropertyBlock(block);
                    views.Add(view);
                }
        }

        public void BeginDeployment()
        {
            if (Deployment != null && Deployment.TryStart()) OnStateChanged();
        }

        public bool TryPlaceUnit(int index, int x, int y)
        {
            if (Deployment == null || !Deployment.TryPlace(index, x, y)) return false;
            SyncViews();
            OnStateChanged();
            return true;
        }

        [ContextMenu("Start Battle")]
        public void StartBattle()
        {
            if (Application.isPlaying && Turns != null && Deployment.Started) Turns.TryStartBattle();
        }

        [ContextMenu("End Turn")]
        public void EndTurn()
        {
            if (Application.isPlaying && State?.CurrentPlayer == PlayerId.PlayerOne)
                Turns.TryEndTurn(State.CurrentPlayer.Value);
        }

        public string DifficultyName => botSettings.DifficultyName;
        public bool HasDifficultyChoice => CountDifficulties() > 1;

        // Takes effect from the bot's next search, so switching mid-match is safe.
        [ContextMenu("Next Difficulty")]
        public void CycleDifficulty()
        {
            int count = CountDifficulties();
            if (count < 2) return;
            botDifficultyIndex = (botDifficultyIndex + 1) % count;
            botSettings = CreateBotSettings();
            CancelBot();
            OnStateChanged();
        }

        private int CountDifficulties()
        {
            if (botDifficulties == null) return 0;
            int count = 0;
            foreach (BotSettingsAsset asset in botDifficulties) if (asset != null) count++;
            return count;
        }

        // Skips empty array slots so a half-filled inspector list still resolves.
        private BotSettings CreateBotSettings()
        {
            int count = CountDifficulties();
            if (count == 0) return new BotSettings();
            botDifficultyIndex = ((botDifficultyIndex % count) + count) % count;
            int seen = 0;
            foreach (BotSettingsAsset asset in botDifficulties)
            {
                if (asset == null) continue;
                if (seen++ == botDifficultyIndex) return asset.CreateSettings();
            }
            return new BotSettings();
        }

        private void Update()
        {
            if (!IsBotTurn) { CancelBot(); return; }
            if (botTask == null && botPlan == null)
            {
                botMatch = State;
                botTurnNumber = State.TurnNumber;
                // The bot plans on what its own side can see, so the opponent's traps
                // are absent from its search and it can be caught by one.
                GameState snapshot = State.CloneAsSeenBy(State.CurrentPlayer.Value);
                DfsBot search = botSettings.CreateBot();
                botCancellation = new CancellationTokenSource();
                CancellationToken token = botCancellation.Token;
                botTask = Task.Run(() => search.FindTurn(snapshot, token), token);
            }
            if (botTask != null)
            {
                if (!botTask.IsCompleted) return;
                if (botTask.IsCanceled) { CancelBot(); return; }
                if (botTask.IsFaulted)
                {
                    Debug.LogException(botTask.Exception, this);
                    CancelBot();
                    Turns.TryEndTurn(PlayerId.PlayerTwo);
                    return;
                }
                botPlan = botTask.Result;
                botTask = null;
                botActionIndex = 0;
                nextBotActionTime = Time.unscaledTime + botSettings.ActionDelay;
            }
            if (!ReferenceEquals(botMatch, State) || botTurnNumber != State.TurnNumber)
            { CancelBot(); return; }
            if (Time.unscaledTime < nextBotActionTime) return;
            if (botActionIndex >= botPlan.Actions.Count)
            {
                Turns.TryEndTurn(PlayerId.PlayerTwo);
                CancelBot();
                return;
            }
            BotAction action = botPlan.Actions[botActionIndex++];
            if (!action.Apply(Turns))
            {
                // Expected now that traps are hidden: a sprung trap stops a unit short
                // and the rest of the plan no longer fits. Think again with what the
                // trap just revealed rather than throwing the turn away. Each replan
                // follows a sprung trap, and a side holds at most two, so this ends.
                if (botReplans < MaxBotReplans)
                {
                    botReplans++;
                    botPlan = null;
                    botActionIndex = 0;
                    return;
                }
                Debug.LogWarning("Bot plan became invalid too often; ending the turn.", this);
                Turns.TryEndTurn(PlayerId.PlayerTwo);
                CancelBot();
                return;
            }
            nextBotActionTime = Time.unscaledTime + botSettings.ActionDelay;
        }

        private void CancelBot()
        {
            if (botCancellation != null)
            {
                botCancellation.Cancel();
                botCancellation.Dispose();
                botCancellation = null;
            }
            // Observe faults even when a restarted match discards an unfinished task.
            if (botTask != null)
                botTask.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            botTask = null;
            botPlan = null;
            botMatch = null;
            // Replans are budgeted per bot turn, and every path out of a turn lands here.
            botReplans = 0;
        }

        private void OnDisable() => CancelBot();

        private void OnStateChanged() => StateChanged?.Invoke();

        private void ClearViews()
        {
            foreach (Unit view in views)
            {
                if (view == null) continue;
                view.gameObject.SetActive(false); // Disable old colliders before deferred Destroy.
                Destroy(view.gameObject);
            }
            views.Clear();
        }

        private void OnDestroy()
        {
            CancelBot();
            if (Turns != null) Turns.StateChanged -= OnStateChanged;
            ClearViews();
        }

        private static PrimitiveType ShapeFor(UnitPosition position)
        {
            switch (position)
            {
                case UnitPosition.Guard: return PrimitiveType.Cylinder;
                case UnitPosition.Ranged: return PrimitiveType.Capsule;
                case UnitPosition.Control: return PrimitiveType.Sphere;
                default: return PrimitiveType.Cube;
            }
        }

        private static Unit AddUnitComponent(GameObject obj, UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Warrior: return obj.AddComponent<WarriorUnit>();
                case UnitKind.Guardian: return obj.AddComponent<GuardianUnit>();
                case UnitKind.Ranger: return obj.AddComponent<RangerUnit>();
                case UnitKind.Controller: return obj.AddComponent<ControllerUnit>();
                case UnitKind.Assassin: return obj.AddComponent<AssassinUnit>();
                case UnitKind.Pyromancer: return obj.AddComponent<PyromancerUnit>();
                case UnitKind.Warper: return obj.AddComponent<WarperUnit>();
                case UnitKind.Breaker: return obj.AddComponent<BreakerUnit>();
                case UnitKind.Trapper: return obj.AddComponent<TrapperUnit>();
                default: return obj.AddComponent<TestUnit>();
            }
        }
    }
}
