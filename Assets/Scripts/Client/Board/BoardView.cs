using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using MiniChess.Core.Effects;
using MiniChess.Core.Effects.Scheduled;
using MiniChess.Core.Effects.Zones;
using MiniChess.Core.State;
using UnityEngine;
using CoreBoard = MiniChess.Core.State.Board;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 코어 Board 의 칸 상태를 맵 씬의 칸 위에 그린다. 판/섬/석조물/하늘/조명은 맵 씬에 손으로 배치되어 있고(MapSceneSettings),
    /// 이 뷰는 그 칸들에 칸 색(바닥/벽/점령 칸/시작 위치), 장판/덫/장애물/포격 표식, 하이라이트만 입힌다. 점령 칸이 소멸하면 SyncEffects 에서 바닥색으로 바뀐다.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        public const string StrikeReticlePath = "Materials/StrikeReticle";

        [Header("Shape")]
        [Tooltip("벽 칸 꼭대기 높이(표식/하이라이트를 올리는 높이).")]
        [SerializeField] private float wallHeight = 0.8f;

        [Header("Colors")]
        [Tooltip("바닥 판석 색에 곱하는 색. 판석마다의 톤 차이는 돌 셰이더가 만든다.")]
        [SerializeField] private Color groundColor = Color.white;
        [SerializeField] private Color wallColor = new Color(0.86f, 0.83f, 0.86f);
        [SerializeField] private Color captureColor = new Color(0.95f, 0.78f, 0.25f);
        [SerializeField] private Color spawnPlayer1Tint = new Color(0.55f, 0.7f, 0.95f);
        [SerializeField] private Color spawnPlayer2Tint = new Color(0.95f, 0.6f, 0.55f);
        [SerializeField, Range(0f, 1f)] private float spawnTintStrength = 0.35f;

        [Header("Highlight (빛나는 테두리 + 영역 경계의 막, CellHighlightRenderer)")]
        [SerializeField] private Color selectedColor = new Color(0.35f, 0.68f, 1f);
        [SerializeField] private Color moveColor = new Color(0.3f, 0.95f, 0.85f);
        [SerializeField] private Color attackColor = new Color(1f, 0.38f, 0.22f);
        [Tooltip("스킬 사거리 미리보기(스킬 버튼에 마우스를 올렸을 때, 대상 유무와 무관).")]
        [SerializeField] private Color skillRangeColor = new Color(0.72f, 0.55f, 0.98f);
        [SerializeField] private Color skillTargetColor = new Color(0.2f, 0.8f, 0.95f);
        [SerializeField] private Color skillAreaColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color skillChosenColor = new Color(0.75f, 0.35f, 0.95f);
        [SerializeField] private Color pushDestinationColor = new Color(1f, 0.25f, 0.6f);
        [Tooltip("영역 경계에 세우는 막의 높이(칸 크기 대비).")]
        [SerializeField, Min(0f)] private float curtainHeight = 0.62f;

        [Header("Cell Effects")]
        [SerializeField] private Color healFieldColor = new Color(0.35f, 0.9f, 0.45f);
        [SerializeField] private Color damageFieldColor = new Color(0.6f, 0.3f, 0.85f);
        [SerializeField] private Color poisonFieldColor = new Color(0.55f, 0.85f, 0.15f);
        [SerializeField] private Color otherFieldColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField, Range(0f, 1f)] private float fieldTintStrength = 0.5f;
        [SerializeField] private Color player1TrapColor = new Color(0.15f, 0.3f, 0.8f);
        [SerializeField] private Color player2TrapColor = new Color(0.8f, 0.2f, 0.15f);
        [SerializeField] private float trapMarkerSize = 0.45f;
        [Tooltip("덫을 이펙트(곰덫 모델 등)로 그릴 때 표식 지름. 모델 밖으로 얇게 드러나는 팀 색 테두리가 된다.")]
        [SerializeField] private float trapRimSize = 0.8f;
        [SerializeField] private Color player1ObstacleColor = new Color(0.3f, 0.4f, 0.65f);
        [SerializeField] private Color player2ObstacleColor = new Color(0.65f, 0.35f, 0.3f);
        [SerializeField] private float obstacleSize = 0.7f;
        [SerializeField] private float obstacleHeight = 0.6f;
        [SerializeField] private Color player1StrikeColor = new Color(0.2f, 0.55f, 1f);
        [SerializeField] private Color player2StrikeColor = new Color(1f, 0.3f, 0.2f);
        [Tooltip("조준점 크기(칸 대비). 네 모서리 꺾쇠가 칸 가장자리 안쪽에 오도록 맞춘다.")]
        [SerializeField] private float strikeMarkerSize = 0.95f;
        [Tooltip("예약 포격 조준점 머티리얼(MiniChess/GlowMarker, 밝기는 머티리얼 Intensity). 색은 쏜 팀(블루/레드) 색으로 입힌다. 비우면 Resources/Materials/StrikeReticle 을 불러온다.")]
        [SerializeField] private Material strikeReticleMaterial;

        [Header("Reveal (숨겨진 상대 덫/포격이 발동할 때 잠깐 보여 줌)")]
        [SerializeField, Min(0f)] private float revealDuration = 1.5f;

        private readonly Dictionary<Position, CellView> _cells = new Dictionary<Position, CellView>();
        private readonly Dictionary<Position, CellHighlightRenderer.Style> _highlights = new Dictionary<Position, CellHighlightRenderer.Style>();
        private CellHighlightRenderer _highlightRenderer;
        private System.Func<Position, Vector3> _cellTop;
        private readonly Dictionary<Position, Color> _spawnTints = new Dictionary<Position, Color>();
        private readonly List<(Position Cell, Team Team, float Until)> _revealedTraps = new List<(Position, Team, float)>();
        private readonly List<(IReadOnlyList<Position> Cells, Team Team, float Until)> _revealedStrikes = new List<(IReadOnlyList<Position>, Team, float)>();
        private BoardCoordinates _coordinates;
        private BoardCellTints _cellTints;
        private CoreBoard _syncedBoard;
        private GameState _syncedState;

        public IReadOnlyDictionary<Position, CellView> Cells => _cells;

        /// <summary>
        /// 칸 효과 종류를 이펙트로 그리는지. 그리는 장판은 칸 색조를 쓰지 않고, 그리는 덫은 표식을 모델 밖 테두리 크기로 줄인다. null 이면 모두 색조/표식.
        /// </summary>
        public System.Predicate<string> HasFieldVisual { get; set; }

        private void Update()
        {
            float now = Time.time;
            bool trapsExpired = _revealedTraps.RemoveAll(reveal => reveal.Until <= now) > 0;
            bool strikesExpired = _revealedStrikes.RemoveAll(reveal => reveal.Until <= now) > 0;

            if (trapsExpired && _syncedBoard != null)
                SyncEffects(_syncedBoard);
            if (strikesExpired && _syncedState != null)
                SyncStrikes(_syncedState);
        }

        /// <summary>발동한 덫 칸에 설치한 팀 색의 덫 표식을 잠깐 띄운다(덫이 소모되었거나 숨겨져 있어도).</summary>
        public void RevealTrap(Position cell, Team team)
        {
            _revealedTraps.Add((cell, team, Time.time + revealDuration));
            if (_syncedBoard != null)
                SyncEffects(_syncedBoard);
        }

        /// <summary>착탄한 포격의 범위에 쏜 팀 색의 조준점을 잠깐 띄운다.</summary>
        public void RevealStrike(IReadOnlyList<Position> cells, Team team)
        {
            _revealedStrikes.Add((cells, team, Time.time + revealDuration));
            if (_syncedState != null)
                SyncStrikes(_syncedState);
        }

        /// <summary>
        /// 칸 효과(장판/덫/장애물)를 현재 코어 상태대로 표시한다. 장판은 칸 색조(이펙트가 있는 종류는 FieldEffectsView 가 그림), 덫은 설치한 팀 색의 표식, 장애물은 설치한 팀 색의 상자(이펙트가 있으면 상자 없이 남은 피격 횟수만).
        /// 온라인에서는 상대 덫이 상태에 없으므로(서버가 숨김) 그리지 않고, 발동할 때 RevealTrap 으로 잠깐 보여 준다.
        /// </summary>
        public void SyncEffects(CoreBoard board)
        {
            _syncedBoard = board;

            foreach (KeyValuePair<Position, CellView> pair in _cells)
            {
                BoardCell cell = board.GetCell(pair.Key);
                CellView view = pair.Value;

                if (!cell.IsWall)
                    view.SetTerrainColor(GetTerrainColor(pair.Key, cell));

                ICellEffect area = cell.GetEffect(CellEffectLayer.AreaEffect);
                bool tinted = area != null && (HasFieldVisual == null || !HasFieldVisual(area.Kind));
                view.SetEffectTint(tinted ? GetFieldColor(area) : (Color?)null, fieldTintStrength);

                ICellEffect trap = cell.GetEffect(CellEffectLayer.Trap);
                Color trapColor = GetTrapColor(trap);
                bool revealed = TryGetRevealedTrap(pair.Key, out Team revealedTeam);
                if (trap == null && revealed)
                    trapColor = revealedTeam == Team.Player1 ? player1TrapColor : player2TrapColor;

                Vector3 top = view.Top;
                // 드러난 상대 덫은 상태에 없으므로 덫 종류(속박 덫)로 본다.
                bool trapModel = HasFieldVisual != null && HasFieldVisual(trap?.Kind ?? CellEffectKinds.RootTrap);
                float trapSize = trapModel ? trapRimSize : trapMarkerSize;
                view.SetTrapMarker(trap != null || revealed, trapColor, top, trapSize * _coordinates.CellSize);

                var obstacle = cell.GetEffect(CellEffectLayer.Obstacle) as Obstacle;
                bool obstacleModel = HasFieldVisual != null && HasFieldVisual(CellEffectKinds.Obstacle);
                view.SetObstacle(
                    obstacle != null, !obstacleModel, GetObstacleColor(obstacle), obstacle?.HitsRemaining ?? 0,
                    top, obstacleSize * _coordinates.CellSize, obstacleHeight);
            }
        }

        private bool TryGetRevealedTrap(Position cell, out Team team)
        {
            foreach ((Position revealedCell, Team revealedTeam, float _) in _revealedTraps)
            {
                if (revealedCell == cell)
                {
                    team = revealedTeam;
                    return true;
                }
            }

            team = default;
            return false;
        }

        /// <summary>장판 색. 구체 타입 대신 종류(Kind)로 고른다(스냅샷으로 복원한 경기의 효과도 같은 종류를 가진다).</summary>
        private Color GetFieldColor(ICellEffect effect)
        {
            switch (effect.Kind)
            {
                case CellEffectKinds.HealField: return healFieldColor;
                case CellEffectKinds.DamageField: return damageFieldColor;
                case CellEffectKinds.PoisonField: return poisonFieldColor;
                default: return otherFieldColor;
            }
        }

        /// <summary>
        /// 예약 포격의 조준 칸마다 바닥에 쏜 팀 색의 조준점을 그린다.
        /// 온라인에서는 상대 포격이 상태에 없으므로(서버가 숨김) 그리지 않고, 착탄할 때 RevealStrike 로 잠깐 보여 준다.
        /// </summary>
        public void SyncStrikes(GameState state)
        {
            _syncedState = state;
            if (strikeReticleMaterial == null)
                strikeReticleMaterial = Resources.Load<Material>(StrikeReticlePath);

            var strikeTeams = new Dictionary<Position, Team>();
            foreach (ScheduledStrike strike in state.ScheduledStrikes)
            {
                foreach (Position cell in strike.Cells)
                    strikeTeams[cell] = strike.SourceTeam;
            }

            foreach ((IReadOnlyList<Position> cells, Team team, float _) in _revealedStrikes)
            {
                foreach (Position cell in cells)
                    strikeTeams[cell] = team;
            }

            foreach (KeyValuePair<Position, CellView> pair in _cells)
            {
                CellView view = pair.Value;
                bool marked = strikeTeams.TryGetValue(pair.Key, out Team team);
                Color color = team == Team.Player1 ? player1StrikeColor : player2StrikeColor;
                Vector3 top = view.Top;
                view.SetStrikeMarker(marked, color, top, strikeMarkerSize * _coordinates.CellSize, strikeReticleMaterial);
            }
        }

        private Color GetObstacleColor(Obstacle obstacle)
        {
            if (obstacle == null)
                return otherFieldColor;

            return obstacle.Zone.OwnerTeam == Team.Player1 ? player1ObstacleColor : player2ObstacleColor;
        }

        private Color GetTrapColor(ICellEffect effect)
        {
            if (effect is ZoneCellEffect zoneEffect)
                return zoneEffect.Zone.OwnerTeam == Team.Player1 ? player1TrapColor : player2TrapColor;

            return otherFieldColor;
        }

        /// <summary>칸을 하이라이트한다. 같은 칸을 여러 번 하이라이트하면 마지막 종류가 남는다.</summary>
        public void Highlight(Position position, CellHighlight kind)
        {
            if (!_cells.ContainsKey(position))
                return;

            _highlights[position] = GetHighlightStyle(kind);
        }

        public void ClearHighlights()
        {
            _highlights.Clear();
        }

        private void LateUpdate()
        {
            _cellTints?.Apply();

            if (_highlights.Count == 0 || _coordinates == null)
                return;

            if (_highlightRenderer == null)
            {
                var material = Resources.Load<Material>(CellHighlightRenderer.MaterialPath);
                if (material == null)
                {
                    Debug.LogError($"[BoardView] Resources/{CellHighlightRenderer.MaterialPath} 머티리얼이 없어 하이라이트를 그릴 수 없습니다");
                    enabled = false;
                    return;
                }

                _highlightRenderer = new CellHighlightRenderer(material) { CurtainHeight = curtainHeight };
                _cellTop = position => _cells[position].Top;
            }

            _highlightRenderer.Draw(_highlights, _cellTop, _coordinates.CellSize, transform.rotation, gameObject.layer);
        }

        /// <summary>
        /// 하이라이트 종류별 모양: 테두리 밝기, 안쪽 채움, 경계 막 여부.
        /// 선택 칸은 테두리가 더 밝고 안이 채워지며, 스킬 사거리 미리보기(잠깐 보임)는 막 없이 테두리만.
        /// </summary>
        private CellHighlightRenderer.Style GetHighlightStyle(CellHighlight kind)
        {
            switch (kind)
            {
                case CellHighlight.Selected: return Style(selectedColor, 1.6f, 0.38f, true);
                case CellHighlight.Move: return Style(moveColor, 1f, 0.18f, true);
                case CellHighlight.Attack: return Style(attackColor, 1.2f, 0.2f, true);
                case CellHighlight.SkillRange: return Style(skillRangeColor, 0.75f, 0.08f, false);
                case CellHighlight.SkillTarget: return Style(skillTargetColor, 1f, 0.16f, true);
                case CellHighlight.SkillArea: return Style(skillAreaColor, 1.1f, 0.22f, true);
                case CellHighlight.SkillChosen: return Style(skillChosenColor, 1.5f, 0.35f, true);
                case CellHighlight.PushDestination: return Style(pushDestinationColor, 1.3f, 0.28f, true);
                default: return Style(Color.white, 1f, 0.1f, false);
            }
        }

        private static CellHighlightRenderer.Style Style(Color color, float border, float fill, bool curtain)
        {
            return new CellHighlightRenderer.Style { Color = color, Border = border, Fill = fill, Curtain = curtain };
        }


        /// <summary>
        /// 맵 씬의 칸 오브젝트들을 코어 보드의 칸과 잇는다(칸을 새로 만들지 않는다). 맵 씬은 MapLoader 가 불러와 코어 보드와 검사해 둔 것이다.
        /// 칸 색(지형/시작 위치/점령 칸)을 칠하고, 이후 SyncEffects/SyncStrikes/Highlight 가 이 칸들에 그린다.
        /// </summary>
        public void Build(CoreBoard board, BoardCoordinates coordinates, MapSceneSettings map)
        {
            Clear();
            _coordinates = coordinates;
            _cellTints = new BoardCellTints(board.Width, board.Height);

            _spawnTints.Clear();
            foreach (Position p in board.GetSpawnPositions(Team.Player1)) _spawnTints[p] = spawnPlayer1Tint;
            foreach (Position p in board.GetSpawnPositions(Team.Player2)) _spawnTints[p] = spawnPlayer2Tint;

            foreach (MapSceneSettings.CellEntry entry in map.Cells)
            {
                var position = new Position(entry.position.x, entry.position.y);
                BoardCell cell = board.GetCell(position);
                _cells[position] = cell.IsWall
                    ? CellView.Attach(entry.cell.gameObject, position, wallHeight, wallColor, _cellTints)
                    : CellView.Attach(entry.cell.gameObject, position, 0f, GetTerrainColor(position, cell), _cellTints);
            }
        }

        /// <summary>칸과의 연결을 끊는다(맵 씬의 칸 오브젝트는 그대로 두고, 경기 중에 붙인 컴포넌트/표식만 지운다).</summary>
        public void Clear()
        {
            _highlights.Clear();
            _revealedTraps.Clear();
            _revealedStrikes.Clear();
            _syncedBoard = null;
            _syncedState = null;

            foreach (CellView cell in _cells.Values)
            {
                if (cell != null)
                    cell.Detach();
            }

            _cells.Clear();
            _cellTints?.Dispose();
            _cellTints = null;
        }

        private void OnDestroy()
        {
            Clear();
        }

        /// <summary>바닥 칸의 기본 색(점령 칸 / 체크 무늬 + 시작 위치 색조).</summary>
        private Color GetTerrainColor(Position position, BoardCell cell)
        {
            Color color = GetGroundColor(cell);
            if (_spawnTints.TryGetValue(position, out Color tint))
                color = Color.Lerp(color, tint, spawnTintStrength);

            return color;
        }

        private Color GetGroundColor(BoardCell cell)
        {
            return cell.IsCaptureTile ? captureColor : groundColor;
        }
    }
}
