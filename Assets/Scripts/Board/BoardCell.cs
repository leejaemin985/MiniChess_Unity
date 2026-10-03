using UnityEngine;

public class BoardCell : MonoBehaviour
{
    public Vector2Int Position { get; private set; }
    private Renderer cellRenderer;
    private MaterialPropertyBlock colors;

    public void SetHighlight(Color? color)
    {
        if (cellRenderer == null) cellRenderer = GetComponent<Renderer>();
        if (colors == null) colors = new MaterialPropertyBlock();
        Color tint = color ?? ((Position.x + Position.y) % 2 == 0
            ? new Color(0.72f, 0.76f, 0.81f) : new Color(0.32f, 0.38f, 0.46f));
        colors.SetColor("_Color", tint);
        colors.SetColor("_BaseColor", tint);
        cellRenderer.SetPropertyBlock(colors);
    }

    public static BoardCell Create(
        Board board,
        int x,
        int y)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);

        obj.name = $"Cell_{x}_{y}";
        obj.transform.SetParent(board.transform, false);

        BoardCell cell = obj.AddComponent<BoardCell>();

        cell.Position = new Vector2Int(x, y);

        float cellSize = board.CellSize;

        obj.transform.localPosition = new Vector3(
            x * cellSize,
            0f,
            y * cellSize
        );

        obj.transform.localScale = new Vector3(
            cellSize * 0.95f,
            0.1f,
            cellSize * 0.95f
        );

        cell.SetHighlight(null);
        return cell;
    }
}
