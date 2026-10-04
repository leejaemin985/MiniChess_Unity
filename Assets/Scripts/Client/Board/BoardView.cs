using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using UnityEngine;
using CoreBoard = MiniChess.Core.State.Board;

namespace MiniChess.Client.Board
{
    /// <summary>코어 Board 의 칸 구성(바닥/벽/점령 칸/시작 위치)을 큐브로 그린다.</summary>
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
        [SerializeField, Range(0f, 1f)] private float highlightStrength = 0.65f;

        private readonly Dictionary<Position, CellView> _cells = new Dictionary<Position, CellView>();
        private readonly List<CellView> _highlighted = new List<CellView>();

        public IReadOnlyDictionary<Position, CellView> Cells => _cells;

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
                default: return Color.white;
            }
        }

        public void Build(CoreBoard board, BoardCoordinates coordinates)
        {
            Clear();

            var spawnTints = new Dictionary<Position, Color>();
            foreach (Position p in board.GetSpawnPositions(Team.Player1)) spawnTints[p] = spawnPlayer1Tint;
            foreach (Position p in board.GetSpawnPositions(Team.Player2)) spawnTints[p] = spawnPlayer2Tint;

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var position = new Position(x, y);
                    BoardCell cell = board.GetCell(position);

                    _cells[position] = cell.IsWall
                        ? CreateWall(position, coordinates)
                        : CreateGround(position, cell, coordinates, spawnTints);
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

        private CellView CreateGround(Position position, BoardCell cell, BoardCoordinates coordinates, Dictionary<Position, Color> spawnTints)
        {
            Color color = GetGroundColor(position, cell);
            if (spawnTints.TryGetValue(position, out Color tint))
                color = Color.Lerp(color, tint, spawnTintStrength);

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

        private Color GetGroundColor(Position position, BoardCell cell)
        {
            if (cell.IsCaptureTile)
                return captureColor;

            return (position.X + position.Y) % 2 == 0 ? groundDark : groundLight;
        }
    }
}
