using MiniChess.Client.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>프로토타입용 uGUI 요소를 코드로 만드는 헬퍼. 프리팹 없이 HUD 를 구성한다.</summary>
    public static class UiFactory
    {
        /// <summary>
        /// 세로 화면 기준 해상도. 모든 HUD 캔버스(코드로 만든 것, 패널 프리팹)가 같은 값을 쓴다.
        /// Expand 라 기준 영역(1080 x 1920)이 어떤 화면비에서도 다 들어오고, 남는 쪽(긴 폰의 세로, 가로 창의 가로)으로 늘어난다.
        /// </summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        public static Canvas CreateCanvas(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            ConfigureScaler(go.AddComponent<CanvasScaler>());

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>캔버스 스케일러를 세로 기준 해상도(ReferenceResolution, Expand)로 맞춘다.</summary>
        public static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        /// <summary>씬에 EventSystem 이 없으면 만든다(버튼 클릭에 필요).</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindObjectOfType<EventSystem>() != null)
                return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(parent, name);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            KoreanFont.Apply(text);
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color color, UnityAction onClick)
        {
            Image background = CreatePanel(parent, name, color);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);

            TextMeshProUGUI text = CreateText(background.transform, "Label", 36f, TextAlignmentOptions.Center);
            text.text = label;
            Stretch(text.rectTransform);

            return button;
        }

        /// <summary>앵커/피벗을 한 점에 두고 위치와 크기를 지정한다.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        /// <summary>부모 영역 전체를 채운다.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
