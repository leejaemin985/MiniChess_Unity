using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>보드 한 칸의 표시. 기본 색과 하이라이트 색만 다루며 게임 상태는 갖지 않는다.</summary>
    public class CellView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _baseColor;

        public Position Position { get; private set; }

        public static CellView Create(Transform parent, Position position, Vector3 center, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Cell {position}";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;

            var view = go.AddComponent<CellView>();
            view.Position = position;
            view._renderer = go.GetComponent<Renderer>();
            view._block = new MaterialPropertyBlock();
            view._baseColor = color;
            view.ApplyColor(color);

            return view;
        }

        /// <summary>기본 색 위에 하이라이트 색을 섞는다.</summary>
        public void SetHighlight(Color highlight, float strength)
        {
            ApplyColor(Color.Lerp(_baseColor, highlight, strength));
        }

        public void ClearHighlight()
        {
            ApplyColor(_baseColor);
        }

        private void ApplyColor(Color color)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
