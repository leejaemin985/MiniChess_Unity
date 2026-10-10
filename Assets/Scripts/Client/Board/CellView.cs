using MiniChess.Client.Common;
using MiniChess.Core.Common;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 한 칸의 표시. 색은 지형 → 장판 색조 → 하이라이트 순으로 겹쳐 그린다.
    /// 덫은 칸 위의 작은 표식, 장애물은 칸 위의 상자, 예약 포격은 칸 위의 X 자로 그린다. 게임 상태는 갖지 않는다.
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

        private GameObject _strikeMarker;
        private MaterialPropertyBlock _strikeBlock;

        private GameObject _obstacle;
        private Renderer _obstacleRenderer;
        private MaterialPropertyBlock _obstacleBlock;
        private TextMeshPro _obstacleLabel;

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

        /// <summary>예약 포격 조준 표식(칸 위의 X 자)을 보이거나 숨긴다.</summary>
        public void SetStrikeMarker(bool visible, Color color, Vector3 worldTop, float size)
        {
            if (!visible)
            {
                if (_strikeMarker != null)
                    _strikeMarker.SetActive(false);
                return;
            }

            if (_strikeMarker == null)
                CreateStrikeMarker();

            _strikeMarker.SetActive(true);
            _strikeMarker.transform.position = worldTop + Vector3.up * 0.03f;
            _strikeMarker.transform.localScale = Vector3.one * size;

            foreach (Renderer bar in _strikeMarker.GetComponentsInChildren<Renderer>())
            {
                bar.GetPropertyBlock(_strikeBlock);
                _strikeBlock.SetColor(ColorId, color);
                bar.SetPropertyBlock(_strikeBlock);
            }
        }

        private void CreateStrikeMarker()
        {
            // 칸은 납작하게 늘린 큐브라, 표식은 크기 왜곡이 없도록 칸의 부모(보드) 아래에 둔다.
            _strikeMarker = new GameObject($"Strike {Position}");
            _strikeMarker.transform.SetParent(transform.parent, false);
            _strikeBlock = new MaterialPropertyBlock();

            foreach (float angle in new[] { 45f, -45f })
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Bar";
                Destroy(bar.GetComponent<Collider>()); // 클릭은 칸이 받는다
                bar.transform.SetParent(_strikeMarker.transform, false);
                bar.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                bar.transform.localScale = new Vector3(1f, 0.02f, 0.12f);
            }
        }

        /// <summary>
        /// 장애물 블록을 보이거나 숨긴다. 칸 윗면 위에 놓인 상자이며, 위에 파괴까지 남은 피격 횟수를 표시한다.
        /// </summary>
        public void SetObstacle(bool visible, Color color, int hitsRemaining, Vector3 worldTop, float size, float height)
        {
            if (!visible)
            {
                if (_obstacle != null)
                {
                    _obstacle.SetActive(false);
                    _obstacleLabel.gameObject.SetActive(false);
                }
                return;
            }

            if (_obstacle == null)
                CreateObstacle();

            _obstacle.SetActive(true);
            _obstacleLabel.gameObject.SetActive(true);
            _obstacle.transform.position = worldTop + Vector3.up * (height * 0.5f);
            _obstacle.transform.localScale = new Vector3(
                size / transform.lossyScale.x, height / transform.lossyScale.y, size / transform.lossyScale.z);

            _obstacleRenderer.GetPropertyBlock(_obstacleBlock);
            _obstacleBlock.SetColor(ColorId, color);
            _obstacleRenderer.SetPropertyBlock(_obstacleBlock);

            _obstacleLabel.transform.position = worldTop + Vector3.up * (height + 0.15f);
            _obstacleLabel.text = hitsRemaining.ToString();
        }

        private void CreateObstacle()
        {
            _obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacle.name = "Obstacle";
            _obstacle.transform.SetParent(transform, true);
            Destroy(_obstacle.GetComponent<Collider>()); // 클릭은 칸이 받는다
            _obstacleRenderer = _obstacle.GetComponent<Renderer>();
            _obstacleBlock = new MaterialPropertyBlock();

            // 칸은 납작하게 늘린 큐브라, 라벨은 크기 왜곡이 없도록 칸의 부모(보드) 아래에 둔다.
            var labelObject = new GameObject($"Obstacle Hits {Position}");
            labelObject.transform.SetParent(transform.parent, false);
            labelObject.AddComponent<Billboard>();
            _obstacleLabel = labelObject.AddComponent<TextMeshPro>();
            KoreanFont.Apply(_obstacleLabel);
            _obstacleLabel.fontSize = 3f;
            _obstacleLabel.alignment = TextAlignmentOptions.Center;
            _obstacleLabel.color = Color.white;
            _obstacleLabel.outlineWidth = 0.25f;
            _obstacleLabel.outlineColor = Color.black;
            _obstacleLabel.rectTransform.sizeDelta = new Vector2(1f, 0.6f);
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

        private void OnDestroy()
        {
            if (_obstacleLabel != null)
                Destroy(_obstacleLabel.gameObject);
            if (_strikeMarker != null)
                Destroy(_strikeMarker);
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
