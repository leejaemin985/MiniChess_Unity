using MiniChess.Client.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// UI 요소(스킬 버튼, 상태 칸 등) 위에 띄우는 짧은 설명 말풍선(어두운 반투명 상자 + 하늘색 테두리 + 아래로 향한 꼬리). 코드로 만든다.
    /// 클릭은 막지 않는다(raycastTarget 끔). 글 길이에 맞춰 폭이 늘어나되 maxWidth 를 넘으면 줄을 바꾼다.
    /// </summary>
    public class InfoBubbleView : MonoBehaviour
    {
        private static readonly Color Fill = new Color(0.07f, 0.09f, 0.15f, 0.94f);
        private static readonly Color Border = new Color(0.55f, 0.75f, 1f, 0.9f);
        private const float MaxWidth = 640f;
        private const float FontSize = 24f;
        private const float Gap = 12f;
        private const float TailSize = 14f;
        /// <summary>화면 가장자리와 띄울 거리(월드).</summary>
        private const float Margin = 8f;

        private RectTransform _rect;
        private RectTransform _box;
        private TextMeshProUGUI _text;

        /// <summary>parent(패널 루트) 아래 맨 앞에 그려지는 말풍선을 만든다. 처음엔 숨겨져 있다.</summary>
        public static InfoBubbleView Create(Transform parent)
        {
            var root = new GameObject("Info Bubble", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<InfoBubbleView>();
            view.Build();
            root.SetActive(false);
            return view;
        }

        /// <summary>anchor(스킬 버튼, 상태 칸 등) 위 가운데에 text 를 띄운다. text 는 리치 텍스트(색 등)를 쓸 수 있다.</summary>
        public void Show(RectTransform anchor, string text)
        {
            _text.text = text;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            // 버튼 윗변 가운데(월드) 위에 꼬리 끝이 오도록 둔다(방금 만든 버튼도 자리가 정해지게 레이아웃을 먼저 맞춘다).
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
            _rect.position = topCenter;
            _rect.anchoredPosition += Vector2.up * Gap;
            KeepInsideScreen();
        }

        /// <summary>상자가 화면(캔버스) 밖으로 나가면 상자만 옆으로 밀어 넣는다. 꼬리는 그대로 가리키는 곳에 둔다.</summary>
        private void KeepInsideScreen()
        {
            _box.anchoredPosition = new Vector2(0f, _box.anchoredPosition.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_box);
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var screen = new Vector3[4];
            ((RectTransform)canvas.transform).GetWorldCorners(screen);
            var box = new Vector3[4];
            _box.GetWorldCorners(box);

            float shift = 0f;
            if (box[0].x < screen[0].x + Margin)
                shift = screen[0].x + Margin - box[0].x;
            else if (box[2].x > screen[2].x - Margin)
                shift = screen[2].x - Margin - box[2].x;
            if (shift != 0f)
                _box.position += new Vector3(shift, 0f, 0f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Build()
        {
            _rect = (RectTransform)transform;
            _rect.pivot = new Vector2(0.5f, 0f);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);

            // 상자: 테두리(바깥) + 채움(안쪽 2px). 글 크기에 맞춰 늘어난다.
            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(transform, false);
            var boxRect = (RectTransform)box.transform;
            _box = boxRect;
            boxRect.pivot = new Vector2(0.5f, 0f);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0f);
            boxRect.anchoredPosition = new Vector2(0f, TailSize * 0.5f);
            var border = box.AddComponent<Image>();
            border.color = Border;
            border.raycastTarget = false;
            var layout = box.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = box.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var inner = new GameObject("Fill", typeof(RectTransform));
            inner.transform.SetParent(box.transform, false);
            var fill = inner.AddComponent<Image>();
            fill.color = Fill;
            fill.raycastTarget = false;
            var innerLayout = inner.AddComponent<HorizontalLayoutGroup>();
            innerLayout.padding = new RectOffset(18, 18, 9, 9);
            innerLayout.childControlWidth = innerLayout.childControlHeight = true;
            innerLayout.childForceExpandWidth = innerLayout.childForceExpandHeight = false;

            var label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(inner.transform, false);
            _text = label.AddComponent<TextMeshProUGUI>();
            KoreanFont.Apply(_text);
            _text.fontSize = FontSize;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.Center;
            _text.raycastTarget = false;
            label.AddComponent<MaxWidthFitter>().Init(_text, MaxWidth);

            // 꼬리: 45도 돌린 작은 사각형(아래쪽 꼭짓점이 버튼을 가리킨다). 테두리 색 위에 채움 색을 겹친다.
            CreateTail("TailBorder", Border, TailSize);
            CreateTail("TailFill", Fill, TailSize - 4f).anchoredPosition += Vector2.up * 1.5f;
        }

        private RectTransform CreateTail(string name, Color color, float size)
        {
            var tail = new GameObject(name, typeof(RectTransform));
            tail.transform.SetParent(transform, false);
            var rect = (RectTransform)tail.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(0f, TailSize * 0.5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = tail.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>글이 maxWidth 를 넘으면 그 폭에서 줄을 바꾸고, 짧으면 글 폭만큼만 차지하게 한다.</summary>
        private class MaxWidthFitter : MonoBehaviour, ILayoutElement
        {
            private TMP_Text _text;
            private float _max;

            public void Init(TMP_Text text, float max)
            {
                _text = text;
                _max = max;
            }

            public float minWidth => -1f;
            public float preferredWidth => _text == null ? -1f : Mathf.Min(_text.GetPreferredValues(_text.text, float.PositiveInfinity, float.PositiveInfinity).x, _max);
            public float flexibleWidth => -1f;
            public float minHeight => -1f;
            public float preferredHeight => _text == null ? -1f : _text.GetPreferredValues(_text.text, preferredWidth, float.PositiveInfinity).y;
            public float flexibleHeight => -1f;
            public int layoutPriority => 2;
            public void CalculateLayoutInputHorizontal() { }
            public void CalculateLayoutInputVertical() { }
        }
    }
}
