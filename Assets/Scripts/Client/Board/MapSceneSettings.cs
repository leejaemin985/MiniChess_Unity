using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MiniChess.Core.Common;
using UnityEngine;
using CoreBoard = MiniChess.Core.State.Board;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 맵 씬(판/섬/석조물을 손으로 배치해 둔 씬)의 등록표. 맵 씬 루트에 하나 둔다.
    /// 판의 칸 오브젝트를 칸 좌표와 함께 등록해 두고, 게임을 불러올 때 코어 보드와 좌표로 연결한다.
    /// 규칙/판정은 코어가 하고 이 씬은 보이는 것만 담으므로, 불러올 때 Validate 로 코어 보드와 어긋나지 않는지 확인한다.
    /// 좌표: 이 오브젝트 위치가 칸 (0, 0) 의 바닥면 중심이고, 칸 (x, y) 는 로컬 (x * CellSize, 0, y * CellSize) 에 있다(BoardCoordinates 와 같다).
    /// </summary>
    public class MapSceneSettings : MonoBehaviour
    {
        [Serializable]
        public struct CellEntry
        {
            [Tooltip("칸 좌표(코어 Position 의 X, Y).")]
            public Vector2Int position;

            [Tooltip("칸 오브젝트(판석/벽과 클릭 판정 상자를 가진다).")]
            public Transform cell;

            [Tooltip("벽 칸으로 꾸몄는지. 코어 보드의 벽 칸과 같아야 한다.")]
            public bool isWall;
        }

        [Tooltip("코어 맵과 짝을 맞추는 이름.")]
        [SerializeField] private string mapId = "RuinsMap";

        [Tooltip("판 크기(칸 수). 코어 보드의 Width / Height 와 같아야 한다.")]
        [SerializeField] private Vector2Int boardSize = new Vector2Int(7, 7);

        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        [Header("Scene Objects")]
        [Tooltip("칸 오브젝트들의 부모. Collect Cells 가 이 아래에서 \"Cell (x, y)\" 이름의 칸을 찾아 등록한다.")]
        [SerializeField] private Transform cellsRoot;

        [Tooltip("판석 사이 줄눈 바닥.")]
        [SerializeField] private Transform floorBed;

        [Tooltip("줄눈 풀, 벽 칸 잔해 등 판 장식.")]
        [SerializeField] private Transform decoration;

        [Tooltip("보드를 띄우는 섬.")]
        [SerializeField] private Transform island;

        [Tooltip("둘레 장벽과 석조물.")]
        [SerializeField] private Transform props;

        [Header("Cells")]
        [SerializeField] private CellEntry[] cells = Array.Empty<CellEntry>();

        public string MapId => mapId;
        public Vector2Int BoardSize => boardSize;
        public float CellSize => cellSize;
        public Transform CellsRoot => cellsRoot;
        public Transform FloorBed => floorBed;
        public Transform Decoration => decoration;
        public Transform Island => island;
        public Transform Props => props;
        public IReadOnlyList<CellEntry> Cells => cells;

        public bool TryGetCell(Position position, out CellEntry entry)
        {
            foreach (CellEntry cell in cells)
            {
                if (cell.position.x == position.X && cell.position.y == position.Y)
                {
                    entry = cell;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        /// <summary>코어 보드와 어긋난 점들(판 크기, 빠진/겹친/판 밖 칸, 벽 칸 차이, 칸 위치). 비어 있으면 그대로 연결해도 된다.</summary>
        public List<string> Validate(CoreBoard board)
        {
            var issues = new List<string>();
            if (board.Width != boardSize.x || board.Height != boardSize.y)
                issues.Add($"판 크기가 다름: 씬 {boardSize.x}x{boardSize.y}, 코어 {board.Width}x{board.Height}");

            var registered = new HashSet<Vector2Int>();
            foreach (CellEntry entry in cells)
            {
                Vector2Int p = entry.position;
                if (entry.cell == null)
                    issues.Add($"칸 {p} 의 오브젝트가 비어 있음");
                if (!registered.Add(p))
                    issues.Add($"칸 {p} 이 두 번 등록됨");
                if (p.x < 0 || p.x >= board.Width || p.y < 0 || p.y >= board.Height)
                {
                    issues.Add($"칸 {p} 이 코어 보드 밖");
                    continue;
                }

                bool coreWall = board.GetCell(new Position(p.x, p.y)).IsWall;
                if (coreWall != entry.isWall)
                    issues.Add($"칸 {p} 벽 여부가 다름: 씬 {(entry.isWall ? "벽" : "바닥")}, 코어 {(coreWall ? "벽" : "바닥")}");

                if (entry.cell != null)
                {
                    Vector3 expected = new Vector3(p.x * cellSize, 0f, p.y * cellSize);
                    Vector3 actual = transform.InverseTransformPoint(entry.cell.position);
                    if ((actual - expected).sqrMagnitude > 0.0001f)
                        issues.Add($"칸 {p} 위치가 좌표와 다름: {actual} (기대 {expected})");
                }
            }

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (!registered.Contains(new Vector2Int(x, y)))
                        issues.Add($"칸 ({x}, {y}) 이 등록되지 않음");
                }
            }

            return issues;
        }

        private static readonly Regex CellName = new Regex(@"^Cell \((-?\d+),\s*(-?\d+)\)$");

        /// <summary>cellsRoot 아래 "Cell (x, y)" 칸들을 좌표 순으로 등록한다. 자식 "Wall" 이 있으면 벽 칸이다.</summary>
        [ContextMenu("Collect Cells")]
        public void CollectCells()
        {
            if (cellsRoot == null)
            {
                Debug.LogWarning("[MapSceneSettings] cellsRoot 를 먼저 지정하세요", this);
                return;
            }

            var list = new List<CellEntry>();
            foreach (Transform child in cellsRoot)
            {
                Match match = CellName.Match(child.name);
                if (!match.Success)
                    continue;

                list.Add(new CellEntry
                {
                    position = new Vector2Int(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)),
                    cell = child,
                    isWall = child.Find("Wall") != null,
                });
            }

            list.Sort((a, b) => a.position.y != b.position.y ? a.position.y.CompareTo(b.position.y) : a.position.x.CompareTo(b.position.x));
            cells = list.ToArray();
            Debug.Log($"[MapSceneSettings] 칸 {cells.Length}개 등록", this);
        }
    }
}
