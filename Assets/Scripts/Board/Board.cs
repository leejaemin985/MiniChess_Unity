using UnityEngine;

public class Board : MonoBehaviour
{
    [SerializeField]
    private int boardSize = 6;

    [SerializeField]
    private float cellSize = 1f;

    private BoardCell[,] cells;

    public int BoardSize => boardSize;
    public float CellSize => cellSize;

    private void Awake()
    {
        CreateBoard();
    }

    private void CreateBoard()
    {
        cells = new BoardCell[boardSize, boardSize];

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                BoardCell cell = BoardCell.Create(
                    this,
                    x,
                    y
                );

                cells[x, y] = cell;
            }
        }
    }

    public BoardCell GetCell(int x, int y)
    {
        if (x < 0 || x >= boardSize ||
            y < 0 || y >= boardSize)
        {
            return null;
        }

        return cells[x, y];
    }
}