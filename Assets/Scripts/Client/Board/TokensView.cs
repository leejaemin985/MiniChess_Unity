using MiniChess.Client.Common;
using MiniChess.Core.State;
using MiniChess.Core.Tokens;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 위 토큰(최대 1개)을 임시 모양으로 보여 준다: 칸 위에 떠서 돌며 흔들리는 유리 보석(팔각 마름모, SM_TokenGem)과 라벨.
    /// 재질은 반투명 유리(TokenGlass)에 같은 색을 약하게 발광시켜 바닥 위에서도 색이 보이게 한다. 색은 힐 = HP 바 연두, AP = HUD AP 라벤더.
    /// 코어 상태대로만 맞추며(먹음/소멸 연출 없음), 모델이 나오면 교체한다.
    /// </summary>
    public class TokensView : MonoBehaviour
    {
        public const string GlassMaterialPath = "Materials/TokenGlass";
        public const string GemMeshPath = "Meshes/SM_TokenGem";

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Color healColor = new Color(0.4f, 0.8f, 0.5f);
        [SerializeField] private Color apColor = new Color32(0xB9, 0x9C, 0xFF, 0xFF);
        [Tooltip("유리의 불투명도.")]
        [SerializeField, Range(0f, 1f)] private float glassAlpha = 0.6f;
        [Tooltip("유리 자체 발광 세기(색 × 이 값). 1 을 넘기면 블룸이 걸린다.")]
        [SerializeField, Min(0f)] private float glassGlow = 0.4f;
        [Tooltip("보석 폭(칸 = 1). 높이는 메시 비율(폭의 약 1.46배)을 따른다.")]
        [SerializeField, Min(0.05f)] private float size = 0.55f;
        [SerializeField] private float height = 0.7f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 2.5f;
        [Tooltip("초당 회전 각도.")]
        [SerializeField] private float spinSpeed = 60f;

        private static Mesh _mesh;

        private BoardCoordinates _coordinates;
        private Token _shown;
        private GameObject _marker;
        private Transform _gem;

        public void Build(BoardCoordinates coordinates)
        {
            Clear();
            _coordinates = coordinates;
        }

        /// <summary>토큰 표시를 현재 코어 상태에 맞춘다.</summary>
        public void Sync(GameState state)
        {
            // 온라인은 갱신마다 상태를 새로 만들므로 객체가 아니라 값으로 비교한다.
            Token token = state?.Token;
            if (token?.Kind == _shown?.Kind && token?.Position == _shown?.Position && token?.Team == _shown?.Team)
                return;

            Clear();
            if (token == null || _coordinates == null)
                return;

            _shown = token;
            _marker = CreateMarker(token);
        }

        public void Clear()
        {
            if (_marker != null)
                Destroy(_marker);
            _marker = null;
            _gem = null;
            _shown = null;
        }

        private void Update()
        {
            if (_gem == null)
                return;

            _gem.localPosition = Vector3.up * (height + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
            _gem.localRotation = Quaternion.Euler(0f, Time.time * spinSpeed, 0f);
        }

        private GameObject CreateMarker(Token token)
        {
            Color color = token.Kind == TokenKind.Heal ? healColor : apColor;
            var root = new GameObject($"Token {token.Kind} {token.Position}");
            root.transform.SetParent(transform, false);
            root.transform.position = _coordinates.ToWorld(token.Position);

            var gem = new GameObject("Gem");
            gem.transform.SetParent(root.transform, false);
            _gem = gem.transform;

            CreateGem(gem.transform, LoadMaterial(GlassMaterialPath), new Color(color.r, color.g, color.b, glassAlpha), color * glassGlow);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            float gemTop = GetMesh() != null ? GetMesh().bounds.max.y * size : size * 0.75f;
            labelObject.transform.localPosition = Vector3.up * (height + gemTop + bobAmplitude + 0.1f);
            labelObject.AddComponent<Billboard>();

            var text = labelObject.AddComponent<TextMeshPro>();
            KoreanFont.Apply(text);
            text.text = token.Kind == TokenKind.Heal ? "HP" : "AP";
            text.fontSize = 3.5f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Bottom;
            text.enableWordWrapping = false;
            text.color = color;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.rectTransform.pivot = new Vector2(0.5f, 0f);
            text.rectTransform.sizeDelta = new Vector2(2f, 0.8f);

            Update();
            return root;
        }

        private static Material LoadMaterial(string path)
        {
            var material = Resources.Load<Material>(path);
            if (material == null)
                Debug.LogError($"[TokensView] Resources/{path} 머티리얼이 없음");
            return material;
        }

        private void CreateGem(Transform parent, Material material, Color color, Color emission)
        {
            var go = new GameObject("Glass");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var block = new MaterialPropertyBlock();
            block.SetColor(ColorId, color);
            block.SetColor(EmissionId, emission);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>팔각 마름모 메시(폭 1, 중심 = 적도). 에디터 메뉴 MiniChess/Build/Token Gem Mesh 로 만든다.</summary>
        private static Mesh GetMesh()
        {
            if (_mesh == null)
                _mesh = Resources.Load<Mesh>(GemMeshPath);
            if (_mesh == null)
                Debug.LogError($"[TokensView] Resources/{GemMeshPath} 메시가 없음");
            return _mesh;
        }
    }
}
