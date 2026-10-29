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
    /// 코어 Board 의 칸 구성(바닥/벽/점령 칸/시작 위치)을 판석으로 그린다(BoardArt). 점령 칸이 소멸하면 SyncEffects 에서 바닥색으로 바뀐다.
    /// 판석 모양(변형/회전/높이/기울기)은 칸 좌표로 정하므로 같은 맵은 항상 같아 보인다. 보드를 만들 때 BoardArt 의 조명을 씬에 적용한다.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        public const string StrikeReticlePath = "Materials/StrikeReticle";

        [Tooltip("판석 메시/머티리얼/조명. 비우면 Resources/BoardArt 를 불러온다(없으면 기본 상자로 그린다).")]
        [SerializeField] private BoardArt art;

        [Header("Shape")]
        [SerializeField] private float wallHeight = 0.8f;

        [Header("Colors")]
        [Tooltip("바닥 판석 색에 곱하는 색. 판석마다의 톤 차이는 돌 셰이더가 만든다.")]
        [SerializeField] private Color groundColor = Color.white;
        [SerializeField] private Color wallColor = new Color(0.86f, 0.83f, 0.86f);
        [SerializeField] private Color captureColor = new Color(0.95f, 0.78f, 0.25f);
        [SerializeField] private Color spawnPlayer1Tint = new Color(0.55f, 0.7f, 0.95f);
        [SerializeField] private Color spawnPlayer2Tint = new Color(0.95f, 0.6f, 0.55f);
        [SerializeField, Range(0f, 1f)] private float spawnTintStrength = 0.35f;

        [Header("Highlight")]
        [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.2f);
        [SerializeField] private Color moveColor = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color attackColor = new Color(0.95f, 0.2f, 0.2f);
        [Tooltip("스킬 사거리 미리보기(스킬 버튼에 마우스를 올렸을 때, 대상 유무와 무관).")]
        [SerializeField] private Color skillRangeColor = new Color(0.72f, 0.55f, 0.98f);
        [SerializeField] private Color skillTargetColor = new Color(0.2f, 0.8f, 0.95f);
        [SerializeField] private Color skillAreaColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color skillChosenColor = new Color(0.75f, 0.35f, 0.95f);
        [SerializeField] private Color pushDestinationColor = new Color(1f, 0.25f, 0.6f);
        [SerializeField, Range(0f, 1f)] private float highlightStrength = 0.65f;

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
        private readonly List<CellView> _highlighted = new List<CellView>();
        private readonly Dictionary<Position, Color> _spawnTints = new Dictionary<Position, Color>();
        private readonly List<(Position Cell, Team Team, float Until)> _revealedTraps = new List<(Position, Team, float)>();
        private readonly List<(IReadOnlyList<Position> Cells, Team Team, float Until)> _revealedStrikes = new List<(IReadOnlyList<Position>, Team, float)>();
        private BoardCoordinates _coordinates;
        private GameObject _bed;
        private readonly List<GameObject> _border = new List<GameObject>();
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

        public void Highlight(Position position, CellHighlight kind)
        {
            if (!_cells.TryGetValue(position, out CellView cell))
                return;

            cell.SetHighlight(GetHighlightColor(kind), highlightStrength);
            _highlighted.Add(cell);
        }

        public void ClearHighlights()
        {
            foreach (CellView cell in _highlighted)
            {
                if (cell != null)
                    cell.ClearHighlight();
            }

            _highlighted.Clear();
        }

        private Color GetHighlightColor(CellHighlight kind)
        {
            switch (kind)
            {
                case CellHighlight.Selected: return selectedColor;
                case CellHighlight.Move: return moveColor;
                case CellHighlight.Attack: return attackColor;
                case CellHighlight.SkillRange: return skillRangeColor;
                case CellHighlight.SkillTarget: return skillTargetColor;
                case CellHighlight.SkillArea: return skillAreaColor;
                case CellHighlight.SkillChosen: return skillChosenColor;
                case CellHighlight.PushDestination: return pushDestinationColor;
                default: return Color.white;
            }
        }

        public void Build(CoreBoard board, BoardCoordinates coordinates)
        {
            Clear();
            _coordinates = coordinates;

            if (art == null)
                art = Resources.Load<BoardArt>(BoardArt.ResourcePath);
            if (art != null)
                art.ApplyLighting();
            CreateBed(coordinates);
            CreateBorder(coordinates);

            _spawnTints.Clear();
            foreach (Position p in board.GetSpawnPositions(Team.Player1)) _spawnTints[p] = spawnPlayer1Tint;
            foreach (Position p in board.GetSpawnPositions(Team.Player2)) _spawnTints[p] = spawnPlayer2Tint;

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var position = new Position(x, y);
                    BoardCell cell = board.GetCell(position);

                    _cells[position] = cell.IsWall
                        ? CreateWall(position, coordinates)
                        : CreateGround(position, cell, coordinates);
                }
            }
        }

        public void Clear()
        {
            _highlighted.Clear();
            _revealedTraps.Clear();
            _revealedStrikes.Clear();
            _syncedBoard = null;
            _syncedState = null;

            foreach (CellView cell in _cells.Values)
            {
                if (cell != null)
                    Destroy(cell.gameObject);
            }

            _cells.Clear();

            if (_bed != null)
                Destroy(_bed);
            _bed = null;

            foreach (GameObject piece in _border)
            {
                if (piece != null)
                    Destroy(piece);
            }

            _border.Clear();
        }

        private CellView CreateGround(Position position, BoardCell cell, BoardCoordinates coordinates)
        {
            // 시작 구역/점령 칸은 깨지거나 꺼지지 않은 판석(배치/점령이 잘 읽히게)
            bool plain = cell.IsCaptureTile || _spawnTints.ContainsKey(position);
            CellView.SlabLook slab = GetSlabLook(position, plain);
            return CellView.Create(transform, position, coordinates.ToWorld(position), coordinates.CellSize, 0f, slab, default, GetTerrainColor(position, cell));
        }

        private CellView CreateWall(Position position, BoardCoordinates coordinates)
        {
            CellView.SlabLook slab = GetSlabLook(position, plain: true);
            CellView.SlabLook wall = default;
            if (art != null)
            {
                var random = CellRandom(position, 1);
                wall = new CellView.SlabLook
                {
                    Mesh = art.GetWallMesh(random.Next(1024)),
                    Material = art.WallMaterial,
                    Rotation = Quaternion.Euler(0f, random.Next(4) * 90f, 0f),
                };
            }

            return CellView.Create(transform, position, coordinates.ToWorld(position), coordinates.CellSize, wallHeight, slab, wall, wallColor);
        }

        /// <summary>칸 좌표로 정하는 난수(같은 칸은 항상 같은 값). salt 로 용도마다 다른 수열을 쓴다.</summary>
        private static System.Random CellRandom(Position position, int salt)
        {
            // 이웃한 칸의 시드가 비슷하면 System.Random 의 첫 값들이 서로 닮으므로, 시드를 한 번 섞어서 쓴다.
            unchecked
            {
                uint h = (uint)(position.X * 374761393 + position.Y * 668265263 + salt * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return new System.Random((int)(h & 0x7fffffff));
            }
        }

        /// <summary>
        /// 칸 좌표로 정한 바닥 판석 모양: 변형(일부는 깨진 판석), 90도 회전, 높이/기울기 흔들림(일부는 통째로 내려앉음).
        /// plain 이면 온전한 판석에 흔들림만.
        /// </summary>
        private CellView.SlabLook GetSlabLook(Position position, bool plain)
        {
            if (art == null)
                return default;

            System.Random random = CellRandom(position, 0);
            float Jitter(float amount) => ((float)random.NextDouble() * 2f - 1f) * amount;

            bool broken = !plain && art.HasBrokenSlabs && random.NextDouble() < art.BrokenChance;
            bool sunk = !plain && !broken && random.NextDouble() < art.SinkChance;
            Mesh mesh = broken ? art.GetBrokenSlabMesh(random.Next(1024)) : art.GetSlabMesh(random.Next(1024));
            float yaw = random.Next(4) * 90f;
            float tilt = sunk ? art.SinkTilt : art.TiltJitter;
            float heightOffset = Jitter(art.HeightJitter) - (sunk ? art.SinkDepth : 0f);

            return new CellView.SlabLook
            {
                Mesh = mesh,
                Material = art.FloorMaterial,
                Rotation = Quaternion.Euler(Jitter(tilt), yaw, Jitter(tilt)),
                HeightOffset = heightOffset,
            };
        }

        /// <summary>
        /// 보드 둘레의 낮은 외곽 벽: 가장자리 칸마다 성벽 조각(칸 좌표로 변형 선택), 네 모서리에 기둥. 클릭 대상 아님.
        /// 조각 메시는 원점 중심, x 방향 길이 1 이므로 위/아래 변은 그대로(위 변은 180도), 왼쪽/오른쪽 변은 90도 돌려 놓는다.
        /// </summary>
        private void CreateBorder(BoardCoordinates coordinates)
        {
            if (art == null || !art.HasBorder)
                return;

            float cell = coordinates.CellSize;
            float offset = 0.5f * cell + art.BorderOffset * cell;
            float right = (coordinates.Width - 1) * cell;
            float top = (coordinates.Height - 1) * cell;

            for (int x = 0; x < coordinates.Width; x++)
            {
                AddBorderPiece(art.GetBorderMesh(CellRandom(new Position(x, -1), 2).Next(1024)), new Vector3(x * cell, 0f, -offset), 0f);
                AddBorderPiece(art.GetBorderMesh(CellRandom(new Position(x, coordinates.Height), 2).Next(1024)), new Vector3(x * cell, 0f, top + offset), 180f);
            }

            for (int y = 0; y < coordinates.Height; y++)
            {
                AddBorderPiece(art.GetBorderMesh(CellRandom(new Position(-1, y), 2).Next(1024)), new Vector3(-offset, 0f, y * cell), 90f);
                AddBorderPiece(art.GetBorderMesh(CellRandom(new Position(coordinates.Width, y), 2).Next(1024)), new Vector3(right + offset, 0f, y * cell), -90f);
            }

            if (art.BorderCornerMesh != null)
            {
                AddBorderPiece(art.BorderCornerMesh, new Vector3(-offset, 0f, -offset), 0f);
                AddBorderPiece(art.BorderCornerMesh, new Vector3(right + offset, 0f, -offset), 90f);
                AddBorderPiece(art.BorderCornerMesh, new Vector3(right + offset, 0f, top + offset), 180f);
                AddBorderPiece(art.BorderCornerMesh, new Vector3(-offset, 0f, top + offset), 270f);
            }
        }

        private void AddBorderPiece(Mesh mesh, Vector3 localPosition, float yaw)
        {
            if (mesh == null)
                return;

            var piece = new GameObject("Border");
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            piece.transform.localScale = Vector3.one * _coordinates.CellSize;
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterial = art.WallMaterial;
            _border.Add(piece);
        }

        /// <summary>판석 사이 줄눈으로 보이는 보드 아래 어두운 바닥(클릭/그림자 대상 아님).</summary>
        private void CreateBed(BoardCoordinates coordinates)
        {
            if (art == null || art.BedMaterial == null)
                return;

            _bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _bed.name = "Floor Bed";
            Destroy(_bed.GetComponent<Collider>());
            _bed.transform.SetParent(transform, false);
            const float thickness = 0.1f;
            Vector3 center = coordinates.Center;
            center.y = -BoardArt.SlabThickness * 0.6f - thickness * 0.5f;
            _bed.transform.localPosition = center;
            _bed.transform.localScale = new Vector3(coordinates.Size.x + 0.2f, thickness, coordinates.Size.y + 0.2f);
            var bedRenderer = _bed.GetComponent<Renderer>();
            bedRenderer.sharedMaterial = art.BedMaterial;
            bedRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
