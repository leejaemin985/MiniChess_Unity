using MiniChess.Client.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 월드 공간 얼굴 표시: 팀 색 테두리 위에 마스크 모양으로 잘린 얼굴. 항상 카메라를 바라본다.
    /// 모양(마스크/테두리 스프라이트)을 넘기지 않으면 코드로 만든 원을 쓴다.
    /// 얼굴 스프라이트는 가로세로 1 유닛(CharacterVisual.GetFaceSprite)이라 size 로 크기를 정한다.
    /// </summary>
    public class FaceBadge : MonoBehaviour
    {
        private const int CircleResolution = 128;

        private static Sprite _circle;

        private SpriteRenderer _image;
        private Color _imageColor = Color.white;

        /// <summary>테두리를 포함한 한 변 길이(월드 단위).</summary>
        public float OuterSize { get; private set; }

        /// <summary>코드로 만든 1 유닛 원(가장자리 부드럽게). 기본 마스크/테두리 모양.</summary>
        public static Sprite DefaultCircle => _circle != null ? _circle : _circle = CreateCircle();

        /// <param name="face">얼굴 스프라이트(1 유닛).</param>
        /// <param name="size">얼굴 한 변 길이(월드 단위).</param>
        /// <param name="frameThickness">테두리 두께(월드 단위).</param>
        /// <param name="mask">얼굴을 자를 모양(1 유닛 기준). null 이면 원.</param>
        /// <param name="frame">테두리 모양(1 유닛 기준). null 이면 원.</param>
        public static FaceBadge Create(
            Transform parent, Sprite face, Color frameColor, float size, float frameThickness, Sprite mask = null, Sprite frame = null)
        {
            var root = new GameObject("Face");
            root.transform.SetParent(parent, false);
            root.AddComponent<Billboard>();

            // 마스크가 다른 유닛의 얼굴까지 자르지 않도록 유닛마다 정렬 그룹으로 묶는다.
            root.AddComponent<SortingGroup>();

            var badge = root.AddComponent<FaceBadge>();
            badge.OuterSize = size + frameThickness * 2f;

            // Billboard 는 카메라 회전을 그대로 따르므로 +z 가 카메라에서 먼 쪽이다. 테두리를 살짝 뒤에 둔다.
            SpriteRenderer frameRenderer = CreateRenderer(root.transform, "Frame", frame != null ? frame : DefaultCircle, badge.OuterSize, 0.01f, 0);
            frameRenderer.color = frameColor;

            badge._image = CreateRenderer(root.transform, "Image", face, size, 0f, 1);
            badge._image.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var maskObject = new GameObject("Mask");
            maskObject.transform.SetParent(root.transform, false);
            Sprite maskSprite = mask != null ? mask : DefaultCircle;
            maskObject.transform.localScale = Vector3.one * ScaleFor(maskSprite, size);
            maskObject.AddComponent<SpriteMask>().sprite = maskSprite;

            return badge;
        }

        /// <summary>얼굴 색(소환물은 옅게 등). 어둡게 표시 중이면 그 위에 적용된다.</summary>
        public void SetImageColor(Color color)
        {
            _imageColor = color;
            _image.color = color;
        }

        /// <summary>행동을 마친 유닛 등 어둡게 표시.</summary>
        public void SetDimmed(bool dimmed)
        {
            _image.color = dimmed ? Color.Lerp(_imageColor, Color.black, 0.45f) : _imageColor;
        }

        private static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, float size, float depth, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, depth);
            go.transform.localScale = Vector3.one * ScaleFor(sprite, size);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>스프라이트 가로가 size(월드 단위)가 되는 배율. 픽셀당 유닛 설정이 달라도 같은 크기로 맞춘다.</summary>
        private static float ScaleFor(Sprite sprite, float size)
        {
            float width = sprite != null ? sprite.bounds.size.x : 0f;
            return width > 0f ? size / width : size;
        }

        private static Sprite CreateCircle()
        {
            var texture = new Texture2D(CircleResolution, CircleResolution, TextureFormat.RGBA32, false)
            {
                name = "FaceCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float radius = CircleResolution * 0.5f;
            var pixels = new Color32[CircleResolution * CircleResolution];
            for (int y = 0; y < CircleResolution; y++)
            {
                for (int x = 0; x < CircleResolution; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    // 가장자리 1.5 픽셀을 부드럽게.
                    float alpha = Mathf.Clamp01((radius - distance) / 1.5f);
                    pixels[y * CircleResolution + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, CircleResolution, CircleResolution), new Vector2(0.5f, 0.5f), CircleResolution);
            sprite.name = "FaceCircle";
            return sprite;
        }
    }
}
