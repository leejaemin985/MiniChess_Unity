using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// Image 를 둥근 사각형(채움) 또는 둥근 테두리로 그린다. Image 가 있는 오브젝트에 붙이고 Image Color 로 색을 정한다.
    ///   Radius    : 모서리 반지름(UI 픽셀). Circle 을 켜면 짧은 변의 절반(정사각형이면 원).
    ///   Thickness : 0 이면 채움, 0 보다 크면 그 두께의 테두리만(가운데 비움).
    /// 모양 그림을 코드로 만들어 9-slice 로 늘려 그리므로 크기가 바뀌어도 둥글기/두께가 유지되고, 기본 UI 셰이더를 그대로 쓴다.
    /// 그림은 Image.overrideSprite 로만 넣어 저장되지 않으며(Source Image 는 비워 둔다), 같은 모양끼리 공유한다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class RoundedFrame : MonoBehaviour
    {
        /// <summary>그림 1 픽셀 = UI 1 단위가 되도록 캔버스 기준(100)과 맞춘다.</summary>
        private const float PixelsPerUnit = 100f;

        private static readonly Dictionary<(int Radius, int Thickness), Sprite> Cache = new Dictionary<(int, int), Sprite>();

        [Tooltip("켜면 반지름을 짧은 변의 절반으로(정사각형이면 원).")]
        [SerializeField] private bool circle;

        [Tooltip("모서리 반지름(UI 픽셀).")]
        [SerializeField, Min(0f)] private float radius = 6f;

        [Tooltip("0 이면 채움, 0 보다 크면 그 두께의 테두리만 남기고 가운데를 비운다(UI 픽셀).")]
        [SerializeField, Min(0f)] private float thickness = 1.5f;

        private Image _image;

        public float Radius
        {
            get => radius;
            set { radius = Mathf.Max(0f, value); Apply(); }
        }

        public float Thickness
        {
            get => thickness;
            set { thickness = Mathf.Max(0f, value); Apply(); }
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            if (_image != null)
                _image.overrideSprite = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (circle)
                Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Apply();
        }
#endif

        private void Apply()
        {
            if (_image == null)
                _image = GetComponent<Image>();
            if (_image == null || !isActiveAndEnabled)
                return;

            Vector2 size = ((RectTransform)transform).rect.size;
            float r = circle ? Mathf.Min(size.x, size.y) * 0.5f : radius;

            _image.overrideSprite = GetSprite(Mathf.Max(1, Mathf.RoundToInt(r)), Mathf.RoundToInt(thickness * 4f));
            _image.type = Image.Type.Sliced;
            _image.fillCenter = true;
            _image.pixelsPerUnitMultiplier = 1f;
        }

        /// <param name="r">모서리 반지름(픽셀).</param>
        /// <param name="thicknessQuarter">테두리 두께 × 4(0 이면 채움). 0.25 픽셀 단위로 공유한다.</param>
        private static Sprite GetSprite(int r, int thicknessQuarter)
        {
            if (Cache.TryGetValue((r, thicknessQuarter), out Sprite cached) && cached != null)
                return cached;

            // 모서리(r + 1) 양쪽 + 늘어나는 가운데 1 픽셀.
            int border = r + 1;
            int size = border * 2 + 1;
            float half = size * 0.5f;
            float thicknessPixels = thicknessQuarter / 4f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"RoundedFrame r{r} t{thicknessPixels}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                    float d = RoundedRectDistance(p, new Vector2(half, half), r);

                    // 바깥선 안쪽만, 두께가 있으면 바깥선에서 thickness 이내만(1 픽셀 부드럽게).
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (thicknessPixels > 0f)
                        alpha *= Mathf.Clamp01(0.5f + d + thicknessPixels);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Cache[(r, thicknessQuarter)] = sprite;
            return sprite;
        }

        /// <summary>중심 기준 p 에서 반크기 halfSize, 모서리 반지름 r 인 둥근 사각형까지의 부호 거리(안쪽이 음수).</summary>
        private static float RoundedRectDistance(Vector2 p, Vector2 halfSize, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }
    }
}
