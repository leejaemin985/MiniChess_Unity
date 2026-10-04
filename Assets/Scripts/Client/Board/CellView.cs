using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 한 칸의 표시. 색은 지형 → 장판 색조 → 하이라이트 순으로 겹쳐 그린다.
    /// 덫은 칸 위의 작은 표식으로 그린다. 게임 상태는 갖지 않는다.
    /// </summary>
    public class CellView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _terrainColor;

        private Color? _effectTint;
        private float _effectStrength;
        private Color? _highlight;
        private float _highlightStrength;

        private GameObject _trapMarker;
        private Renderer _trapRenderer;
        private MaterialPropertyBlock _trapBlock;

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
            view._terrainColor = color;
            view.Refresh();

            return view;
        }

        /// <summary>지형 기본 색(점령 칸 소멸 등으로 바뀔 때).</summary>
        public void SetTerrainColor(Color color)
        {
            if (_terrainColor == color)
                return;

            _terrainColor = color;
            Refresh();
        }

        /// <summary>장판 색조. null 이면 장판 없음.</summary>
        public void SetEffectTint(Color? tint, float strength)
        {
            _effectTint = tint;
            _effectStrength = strength;
            Refresh();
        }

        public void SetHighlight(Color highlight, float strength)
        {
            _highlight = highlight;
            _highlightStrength = strength;
            Refresh();
        }

        public void ClearHighlight()
        {
            _highlight = null;
            Refresh();
        }

        /// <summary>덫 표식을 보이거나 숨긴다. 표식은 칸 윗면 위에 놓인 납작한 원통이다.</summary>
        public void SetTrapMarker(bool visible, Color color, Vector3 worldTop, float size)
        {
            if (!visible)
            {
                if (_trapMarker != null)
                    _trapMarker.SetActive(false);
                return;
            }

            if (_trapMarker == null)
                CreateTrapMarker();

            _trapMarker.SetActive(true);
            // 기본 원통은 지름 1, 높이 2. 부모(칸 큐브)의 스케일을 상쇄해 월드 크기로 맞춘다.
            _trapMarker.transform.position = worldTop + Vector3.up * 0.02f;
            _trapMarker.transform.localScale = new Vector3(
                size / transform.lossyScale.x, 0.02f / transform.lossyScale.y, size / transform.lossyScale.z);

            _trapRenderer.GetPropertyBlock(_trapBlock);
            _trapBlock.SetColor(ColorId, color);
            _trapRenderer.SetPropertyBlock(_trapBlock);
        }

        private void CreateTrapMarker()
        {
            _trapMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _trapMarker.name = "Trap";
            _trapMarker.transform.SetParent(transform, true);
            Destroy(_trapMarker.GetComponent<Collider>()); // 클릭은 칸이 받는다
            _trapRenderer = _trapMarker.GetComponent<Renderer>();
            _trapBlock = new MaterialPropertyBlock();
        }

        private void Refresh()
        {
            Color color = _terrainColor;
            if (_effectTint.HasValue)
                color = Color.Lerp(color, _effectTint.Value, _effectStrength);
            if (_highlight.HasValue)
                color = Color.Lerp(color, _highlight.Value, _highlightStrength);

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
