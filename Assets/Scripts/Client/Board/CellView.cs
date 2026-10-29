using MiniChess.Client.Common;
using MiniChess.Core.Common;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 한 칸의 표시. 칸 오브젝트는 칸 바닥면 중심(윗면 높이 0)에 놓이고 크기를 늘리지 않는다. 그림은 자식 판석(돌 셰이더)이다.
    /// 색: 지형 색조(시작 위치/점령 칸/벽)는 판석 색에 곱하고(_BaseColor), 장판 색조는 판석 위에 덧칠한다(_OverlayColor). 하이라이트는 BoardView 가 칸 위에 따로 그린다.
    /// 덫은 칸 위의 작은 표식, 장애물은 칸 위의 상자, 예약 포격은 칸 바닥의 조준점으로 그린다. 게임 상태는 갖지 않는다.
    /// 클릭 판정 상자는 칸 전체 크기라 판석 사이 줄눈을 눌러도 그 칸이 잡힌다.
    /// </summary>
    public class CellView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int OverlayId = Shader.PropertyToID("_OverlayColor");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Color _terrainColor;
        private float _height;

        private Color? _effectTint;
        private float _effectStrength;

        private GameObject _trapMarker;
        private Renderer _trapRenderer;
        private MaterialPropertyBlock _trapBlock;

        private GameObject _strikeMarker;
        private Renderer _strikeRenderer;
        private MaterialPropertyBlock _strikeBlock;

        private GameObject _obstacle;
        private Renderer _obstacleRenderer;
        private MaterialPropertyBlock _obstacleBlock;
        private TextMeshPro _obstacleLabel;

        public Position Position { get; private set; }

        /// <summary>칸 윗면 중심(월드). 덫/장애물/조준점을 이 위에 놓는다. 벽은 벽 꼭대기.</summary>
        public Vector3 Top => transform.position + Vector3.up * _height;

        /// <param name="floorCenter">칸 바닥면 중심(보드 기준 로컬 좌표, 윗면 높이 0).</param>
        /// <param name="height">윗면 높이(벽 꼭대기, 표식/클릭 판정 기준). 바닥 칸은 0.</param>
        /// <param name="slab">바닥 판석 그림. 메시가 없으면 기본 상자로 그린다.</param>
        /// <param name="wall">벽 칸이면 판석 위에 세울 벽 그림(메시가 없으면 벽 없이 판석만, 높이가 있으면 판석을 세로로 늘린다).</param>
        public static CellView Create(Transform parent, Position position, Vector3 floorCenter, float cellSize, float height, SlabLook slab, SlabLook wall, Color color)
        {
            var go = new GameObject($"Cell {position}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = floorCenter;

            float thickness = BoardArt.SlabThickness;
            var collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, (height - thickness) * 0.5f, 0f);
            collider.size = new Vector3(cellSize, height + thickness, cellSize);

            var view = go.AddComponent<CellView>();
            view.Position = position;
            view._height = height;
            bool stretch = height > 0f && wall.Mesh == null;
            Renderer slabRenderer = CreateSlab(go.transform, cellSize, stretch ? height : 0f, slab);
            view._renderers = wall.Mesh != null
                ? new[] { slabRenderer, CreatePart(go.transform, "Wall", cellSize, wall) }
                : new[] { slabRenderer };
            view._block = new MaterialPropertyBlock();
            view._terrainColor = color;
            view.Refresh();

            return view;
        }

        /// <summary>판석/벽 한 장의 모양. BoardView 가 칸 좌표로 정한다(같은 칸은 항상 같은 모양).</summary>
        public struct SlabLook
        {
            public Mesh Mesh;
            public Material Material;
            public Quaternion Rotation;
            public float HeightOffset;
        }

        /// <param name="height">0 보다 크면 판석을 그 높이까지 세로로 늘린다(벽 메시가 없을 때의 임시 벽).</param>
        private static Renderer CreateSlab(Transform cell, float cellSize, float height, SlabLook slab)
        {
            float thickness = BoardArt.SlabThickness;
            if (slab.Mesh == null)
            {
                GameObject box = RuntimePrimitive.Create(PrimitiveType.Cube);
                Destroy(box.GetComponent<Collider>());
                box.name = "Slab";
                box.transform.SetParent(cell, false);
                box.transform.localPosition = Vector3.up * ((height - thickness) * 0.5f);
                box.transform.localScale = new Vector3(cellSize * 0.94f, height + thickness, cellSize * 0.94f);
                return box.GetComponent<Renderer>();
            }

            Renderer renderer = CreatePart(cell, "Slab", cellSize, slab);
            Transform t = renderer.transform;
            t.localPosition += Vector3.up * height;
            // 판석은 윗면 y = 0, 두께 thickness. 임시 벽은 바닥면 아래까지 이어지도록 세로로 늘린다.
            t.localScale = new Vector3(cellSize, (height + thickness) / thickness, cellSize);
            return renderer;
        }

        private static Renderer CreatePart(Transform cell, string name, float cellSize, SlabLook look)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = look.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = look.Material;
            go.transform.SetParent(cell, false);
            go.transform.localPosition = Vector3.up * look.HeightOffset;
            go.transform.localRotation = look.Rotation;
            go.transform.localScale = new Vector3(cellSize, cellSize, cellSize);
            return renderer;
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
            // 기본 원통은 지름 1, 높이 2. 칸 오브젝트는 크기를 늘리지 않으므로 로컬 크기가 곧 월드 크기다.
            _trapMarker.transform.position = worldTop + Vector3.up * 0.02f;
            _trapMarker.transform.localScale = new Vector3(size, 0.02f, size);

            _trapRenderer.GetPropertyBlock(_trapBlock);
            _trapBlock.SetColor(ColorId, color);
            _trapRenderer.SetPropertyBlock(_trapBlock);
        }

        /// <summary>예약 포격 조준 표식(칸 위의 X 자)을 보이거나 숨긴다.</summary>
        /// <param name="material">조준점 그림 머티리얼(색은 color 로 입힌다).</param>
        public void SetStrikeMarker(bool visible, Color color, Vector3 worldTop, float size, Material material)
        {
            if (!visible)
            {
                if (_strikeMarker != null)
                    _strikeMarker.SetActive(false);
                return;
            }

            if (_strikeMarker == null)
                CreateStrikeMarker(material);

            _strikeMarker.SetActive(true);
            _strikeMarker.transform.position = worldTop + Vector3.up * 0.02f;
            _strikeMarker.transform.localScale = Vector3.one * size;

            _strikeRenderer.GetPropertyBlock(_strikeBlock);
            _strikeBlock.SetColor(ColorId, color);
            _strikeRenderer.SetPropertyBlock(_strikeBlock);
        }

        private void CreateStrikeMarker(Material material)
        {
            _strikeMarker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _strikeMarker.name = $"Strike {Position}";
            Destroy(_strikeMarker.GetComponent<Collider>()); // 클릭은 칸이 받는다
            _strikeMarker.transform.SetParent(transform.parent, false);
            _strikeMarker.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕힌다(윗면이 보이게)
            _strikeRenderer = _strikeMarker.GetComponent<Renderer>();
            _strikeRenderer.sharedMaterial = material;
            _strikeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _strikeRenderer.receiveShadows = false;
            _strikeBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 장애물 블록을 보이거나 숨긴다. 칸 윗면 위에 놓인 상자이며, 위에 파괴까지 남은 피격 횟수를 표시한다.
        /// drawBlock 이 false 면(장애물을 이펙트로 그림) 상자 없이 피격 횟수만 표시한다.
        /// </summary>
        public void SetObstacle(bool visible, bool drawBlock, Color color, int hitsRemaining, Vector3 worldTop, float size, float height)
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

            _obstacle.SetActive(drawBlock);
            _obstacleLabel.gameObject.SetActive(true);
            _obstacle.transform.position = worldTop + Vector3.up * (height * 0.5f);
            _obstacle.transform.localScale = new Vector3(size, height, size);

            _obstacleRenderer.GetPropertyBlock(_obstacleBlock);
            _obstacleBlock.SetColor(ColorId, color);
            _obstacleRenderer.SetPropertyBlock(_obstacleBlock);

            _obstacleLabel.transform.position = worldTop + Vector3.up * (height + 0.15f);
            _obstacleLabel.text = hitsRemaining.ToString();
        }

        private void CreateObstacle()
        {
            _obstacle = RuntimePrimitive.Create(PrimitiveType.Cube);
            _obstacle.name = "Obstacle";
            _obstacle.transform.SetParent(transform, true);
            Destroy(_obstacle.GetComponent<Collider>()); // 클릭은 칸이 받는다
            _obstacleRenderer = _obstacle.GetComponent<Renderer>();
            _obstacleBlock = new MaterialPropertyBlock();

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
            _trapMarker = RuntimePrimitive.Create(PrimitiveType.Cylinder);
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
            // 덧칠: 장판 색조. a = 판석 색을 덧칠 색으로 바꾸는 정도. 하이라이트는 BoardView 가 따로 그린다(CellHighlightRenderer).
            var overlay = new Color(0f, 0f, 0f, 0f);
            if (_effectTint.HasValue)
                overlay = new Color(_effectTint.Value.r, _effectTint.Value.g, _effectTint.Value.b, _effectStrength);

            foreach (Renderer renderer in _renderers)
            {
                renderer.GetPropertyBlock(_block);
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(OverlayId))
                {
                    _block.SetColor(ColorId, _terrainColor);
                    _block.SetColor(OverlayId, overlay);
                }
                else
                {
                    // 돌 셰이더가 아닌 기본 상자(BoardArt 없음): 덧칠을 색에 섞는다.
                    _block.SetColor(ColorId, Color.Lerp(_terrainColor, overlay, overlay.a));
                }
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
