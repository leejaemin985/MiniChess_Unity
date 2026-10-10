using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using MiniChess.Core.Effects;
using MiniChess.Core.Effects.Zones;
using MiniChess.Core.State;
using UnityEngine;
using CoreBoard = MiniChess.Core.State.Board;

namespace MiniChess.Client.Board
{
    /// <summary>코어 Board 의 칸 구성(바닥/벽/점령 칸/시작 위치)을 큐브로 그린다. 점령 칸이 소멸하면 SyncEffects 에서 바닥색으로 바뀐다.</summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private float tileThickness = 0.1f;
        [SerializeField] private float tileGap = 0.05f;
        [SerializeField] private float wallHeight = 0.8f;

        [Header("Colors")]
        [SerializeField] private Color groundLight = new Color(0.82f, 0.82f, 0.78f);
        [SerializeField] private Color groundDark = new Color(0.68f, 0.68f, 0.64f);
        [SerializeField] private Color wallColor = new Color(0.25f, 0.25f, 0.28f);
        [SerializeField] private Color captureColor = new Color(0.95f, 0.78f, 0.25f);
        [SerializeField] private Color spawnPlayer1Tint = new Color(0.55f, 0.7f, 0.95f);
        [SerializeField] private Color spawnPlayer2Tint = new Color(0.95f, 0.6f, 0.55f);
        [SerializeField, Range(0f, 1f)] private float spawnTintStrength = 0.35f;

        [Header("Highlight")]
        [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.2f);
        [SerializeField] private Color moveColor = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color attackColor = new Color(0.95f, 0.2f, 0.2f);
        [SerializeField] private Color skillTargetColor = new Color(0.2f, 0.8f, 0.95f);
        [SerializeField] private Color skillAreaColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color skillChosenColor = new Color(0.75f, 0.35f, 0.95f);
        [SerializeField, Range(0f, 1f)] private float highlightStrength = 0.65f;

        [Header("Cell Effects")]
        [SerializeField] private Color healFieldColor = new Color(0.35f, 0.9f, 0.45f);
        [SerializeField] private Color damageFieldColor = new Color(0.6f, 0.3f, 0.85f);
        [SerializeField] private Color otherFieldColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField, Range(0f, 1f)] private float fieldTintStrength = 0.5f;
        [SerializeField] private Color player1TrapColor = new Color(0.15f, 0.3f, 0.8f);
        [SerializeField] private Color player2TrapColor = new Color(0.8f, 0.2f, 0.15f);
        [SerializeField] private float trapMarkerSize = 0.45f;

        private readonly Dictionary<Position, CellView> _cells = new Dictionary<Position, CellView>();
        private readonly List<CellView> _highlighted = new List<CellView>();
        private readonly Dictionary<Position, Color> _spawnTints = new Dictionary<Position, Color>();
        private BoardCoordinates _coordinates;

        public IReadOnlyDictionary<Position, CellView> Cells => _cells;

        /// <summary>
        /// 칸 효과(장판/덫)를 현재 코어 상태대로 표시한다. 장판은 칸 색조, 덫은 설치한 팀 색의 표식.
        /// [가정] 덫 공개 여부는 명세 TBD 이며, 지금은 양 팀 모두에게 보인다.
        /// </summary>
        public void SyncEffects(CoreBoard board)
        {
            foreach (KeyValuePair<Position, CellView> pair in _cells)
            {
                BoardCell cell = board.GetCell(pair.Key);
                CellView view = pair.Value;

                if (!cell.IsWall)
                    view.SetTerrainColor(GetTerrainColor(pair.Key, cell));

                ICellEffect area = cell.GetEffect(CellEffectLayer.AreaEffect);
                view.SetEffectTint(area == null ? (Color?)null : GetFieldColor(area), fieldTintStrength);

                ICellEffect trap = cell.GetEffect(CellEffectLayer.Trap);
                Vector3 top = view.transform.position + Vector3.up * (view.transform.lossyScale.y * 0.5f);
                view.SetTrapMarker(trap != null, GetTrapColor(trap), top, trapMarkerSize * _coordinates.CellSize);
            }
        }

        private Color GetFieldColor(ICellEffect effect)
        {
            switch (effect)
            {
                case HealField _: return healFieldColor;
                case DamageField _: return damageFieldColor;
                default: return otherFieldColor;
            }
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
                case CellHighlight.SkillTarget: return skillTargetColor;
                case CellHighlight.SkillArea: return skillAreaColor;
                case CellHighlight.SkillChosen: return skillChosenColor;
                default: return Color.white;
            }
        }

        public void Build(CoreBoard board, BoardCoordinates coordinates)
        {
            Clear();
            _coordinates = coordinates;

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

            foreach (CellView cell in _cells.Values)
            {
                if (cell != null)
                    Destroy(cell.gameObject);
            }

            _cells.Clear();
        }

        private CellView CreateGround(Position position, BoardCell cell, BoardCoordinates coordinates)
        {
            Color color = GetTerrainColor(position, cell);

            float size = coordinates.CellSize - tileGap;
            Vector3 center = coordinates.ToWorld(position) + Vector3.down * (tileThickness * 0.5f);

            return CellView.Create(transform, position, center, new Vector3(size, tileThickness, size), color);
        }

        private CellView CreateWall(Position position, BoardCoordinates coordinates)
        {
            float size = coordinates.CellSize - tileGap;
            float height = tileThickness + wallHeight;
            Vector3 center = coordinates.ToWorld(position) + Vector3.up * (height * 0.5f - tileThickness);

            return CellView.Create(transform, position, center, new Vector3(size, height, size), wallColor);
        }

        /// <summary>바닥 칸의 기본 색(점령 칸 / 체크 무늬 + 시작 위치 색조).</summary>
        private Color GetTerrainColor(Position position, BoardCell cell)
        {
            Color color = GetGroundColor(position, cell);
            if (_spawnTints.TryGetValue(position, out Color tint))
                color = Color.Lerp(color, tint, spawnTintStrength);

            return color;
        }

        private Color GetGroundColor(Position position, BoardCell cell)
        {
            if (cell.IsCaptureTile)
                return captureColor;

            return (position.X + position.Y) % 2 == 0 ? groundDark : groundLight;
        }
    }
}
