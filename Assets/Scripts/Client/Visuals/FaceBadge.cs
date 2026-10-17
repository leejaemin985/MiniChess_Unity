using MiniChess.Client.Common;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 월드 공간 얼굴 표시: 팀 색 테두리(사각형) 위에 얼굴 스프라이트. 항상 카메라를 바라본다.
    /// 얼굴 스프라이트는 가로세로 1 유닛(CharacterVisual.GetFaceSprite)이라 size 로 크기를 정한다.
    /// </summary>
    public class FaceBadge : MonoBehaviour
    {
        private static Sprite _whiteSprite;

        private SpriteRenderer _image;
        private SpriteRenderer _frame;
        private Color _imageColor = Color.white;

        /// <summary>테두리를 포함한 한 변 길이(월드 단위).</summary>
        public float OuterSize { get; private set; }

        /// <param name="face">얼굴 스프라이트(1 유닛).</param>
        /// <param name="size">얼굴 한 변 길이(월드 단위).</param>
        /// <param name="frameThickness">테두리 두께(월드 단위).</param>
        public static FaceBadge Create(Transform parent, Sprite face, Color frameColor, float size, float frameThickness)
        {
            var root = new GameObject("Face");
            root.transform.SetParent(parent, false);
            root.AddComponent<Billboard>();

            var badge = root.AddComponent<FaceBadge>();
            badge.OuterSize = size + frameThickness * 2f;

            // Billboard 는 카메라 회전을 그대로 따르므로 +z 가 카메라에서 먼 쪽이다. 테두리를 살짝 뒤에 둔다.
            badge._frame = CreateRenderer(root.transform, "Frame", GetWhiteSprite(), badge.OuterSize, 0.01f, 0);
            badge._frame.color = frameColor;
            badge._image = CreateRenderer(root.transform, "Image", face, size, 0f, 1);

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
            go.transform.localScale = Vector3.one * size;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>테두리용 1 유닛 흰 사각형.</summary>
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
                _whiteSprite.name = "FaceFrame";
            }

            return _whiteSprite;
        }
    }
}
