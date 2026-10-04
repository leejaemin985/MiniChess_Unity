using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>
    /// 코어 Position 과 월드 좌표 사이의 변환.
    /// 보드는 XZ 평면에 놓이며, Position.X → 월드 x, Position.Y → 월드 z 이다.
    /// 칸의 중심이 (X * CellSize, 0, Y * CellSize) 이고, 보드 바닥 윗면이 y = 0 이다.
    /// </summary>
    public class BoardCoordinates
    {
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        public BoardCoordinates(int width, int height, float cellSize)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
        }

        /// <summary>보드 중앙의 월드 좌표.</summary>
        public Vector3 Center => new Vector3((Width - 1) * 0.5f * CellSize, 0f, (Height - 1) * 0.5f * CellSize);

        /// <summary>보드 전체의 가로/세로 길이(월드 단위).</summary>
        public Vector2 Size => new Vector2(Width * CellSize, Height * CellSize);

        public Vector3 ToWorld(Position position)
        {
            return new Vector3(position.X * CellSize, 0f, position.Y * CellSize);
        }

        /// <summary>월드 좌표가 속한 칸. 보드 밖이면 false.</summary>
        public bool TryGetPosition(Vector3 world, out Position position)
        {
            int x = Mathf.RoundToInt(world.x / CellSize);
            int y = Mathf.RoundToInt(world.z / CellSize);
            position = new Position(x, y);

            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
    }
}
