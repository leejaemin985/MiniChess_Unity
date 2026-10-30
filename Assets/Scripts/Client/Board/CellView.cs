using MiniChess.Client.Common;
using MiniChess.Core.Common;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 한 칸의 표시. 칸 오브젝트는 맵 씬에 미리 놓여 있고(MapSceneSettings 에 등록), 경기를 시작할 때 BoardView 가 이 컴포넌트를 붙여 코어 칸과 잇는다(Attach).
    /// 칸 오브젝트는 칸 바닥면 중심(윗면 높이 0)에 놓이고 크기를 늘리지 않는다. 그림은 자식 판석/벽(돌 셰이더)이다.
    /// 색: 지형 색조(시작 위치/점령 칸/벽)는 판석 색에 곱하고, 장판 색조는 판석 위에 덧칠한다. 돌 셰이더는 보드 칸 색 텍스처(BoardCellTints)로,
    /// 그 밖의 머티리얼은 MaterialPropertyBlock 으로 칠한다. 하이라이트는 BoardView 가 칸 위에 따로 그린다.
    /// 덫은 칸 위의 작은 표식, 장애물은 칸 위의 상자, 예약 포격은 칸 바닥의 조준점으로 그린다. 게임 상태는 갖지 않는다.
    /// 클릭 판정 상자(맵 씬의 칸에 있음)는 칸 전체 크기라 판석 사이 줄눈을 눌러도 그 칸이 잡힌다.
    /// 경기가 끝나면 Detach 로 이 컴포넌트와 경기 중에 만든 표식을 지운다(맵 씬의 칸 오브젝트는 그대로 둔다).
    /// </summary>
    public class CellView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int OverlayId = Shader.PropertyToID("_OverlayColor");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private BoardCellTints _tints;
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

        /// <summary>
        /// 맵 씬의 칸 오브젝트에 이 컴포넌트를 붙여 코어 칸과 잇는다. 칸 그림은 칸 오브젝트 아래의 렌더러(판석/벽)다.
        /// </summary>
        /// <param name="height">윗면 높이(벽 꼭대기, 표식 기준). 바닥 칸은 0.</param>
        /// <param name="tints">돌 셰이더 판석/벽의 칸 색을 기록할 곳(보드 공용).</param>
        internal static CellView Attach(GameObject cell, Position position, float height, Color color, BoardCellTints tints)
        {
            var view = cell.AddComponent<CellView>();
            view.Position = position;
            view._height = height;
            view._renderers = cell.GetComponentsInChildren<Renderer>(true);
            view._block = new MaterialPropertyBlock();
            view._tints = tints;
            view._terrainColor = color;
            view.Refresh();

            return view;
        }

        /// <summary>경기 중에 만든 표식(덫/조준점/장애물)과 이 컴포넌트를 지운다. 칸 오브젝트와 그림은 맵 씬에 그대로 남는다.</summary>
        internal void Detach()
        {
            if (_trapMarker != null)
                Destroy(_trapMarker);
            if (_obstacle != null)
                Destroy(_obstacle);

            // 칸 색 덮어쓰기(기본 머티리얼)를 지워 맵 씬의 원래 모습으로 돌린다
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                    renderer.SetPropertyBlock(null);
            }

            Destroy(this); // OnDestroy 가 칸 밖에 둔 표식(조준점, 장애물 숫자)을 지운다
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

            bool written = false;
            foreach (Renderer renderer in _renderers)
            {
                if (_tints != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(OverlayId))
                {
                    // 돌 셰이더: 칸 색 텍스처에 기록(셰이더가 칸 좌표로 읽는다). 렌더러는 SRP Batcher 로 묶인 채 둔다.
                    if (!written)
                        _tints.Set(Position, _terrainColor, overlay);
                    written = true;
                    continue;
                }

                // 돌 셰이더가 아닌 머티리얼: 덧칠을 색에 섞는다.
                renderer.GetPropertyBlock(_block);
                _block.SetColor(ColorId, Color.Lerp(_terrainColor, overlay, overlay.a));
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
