using MiniChess.Client.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 상태이상 설명 창. 화면 오른쪽의 버튼을 누르면 모든 상태(StatusVisualCatalog 순서)의 아이콘, 이름, 설명을 목록으로 띄우고, 다시 누르거나 닫기를 누르면 닫는다.
    /// 설명이 없는 상태는 목록에서 뺀다. HUD 캔버스에 코드로 만든다.
    /// </summary>
    public class StatusGuideView : MonoBehaviour
    {
        private static readonly Color ButtonColor = new Color(0.15f, 0.15f, 0.18f, 0.9f);
        private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.15f, 0.95f);
        private static readonly Color BorderColor = new Color(0.55f, 0.75f, 1f, 0.9f);
        private const float PanelWidth = 660f;
        private const float RowHeight = 56f;
        private const float NameWidth = 130f;
        private const float IconWidth = 40f;
        private const float RowSpacing = 12f;
        private const int SidePadding = 18;
        /// <summary>설명 칸 폭: 창 폭에서 여백, 아이콘, 이름, 간격을 뺀 나머지(고정해 왼쪽 정렬이 흐트러지지 않게).</summary>
        private const float DescriptionWidth = PanelWidth - SidePadding * 2 - IconWidth - NameWidth - RowSpacing * 2;

        private GameObject _panel;

        /// <param name="root">HUD 캔버스.</param>
        /// <param name="buttonPosition">버튼 자리(오른쪽 위 기준).</param>
        public static StatusGuideView Create(Transform root, StatusVisualCatalog catalog, Vector2 buttonPosition)
        {
            var view = UiFactory.CreateRect(root, "StatusGuide").gameObject.AddComponent<StatusGuideView>();
            UiFactory.Stretch((RectTransform)view.transform);
            view.Build(catalog, buttonPosition);
            return view;
        }

        private void Build(StatusVisualCatalog catalog, Vector2 buttonPosition)
        {
            Button button = UiFactory.CreateButton(transform, "Button", "상태이상", ButtonColor, Toggle);
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 30f;
            UiFactory.Place((RectTransform)button.transform, new Vector2(1f, 1f), buttonPosition, new Vector2(280f, 64f));

            // 창: 테두리 + 안쪽 목록(위에서 아래로). 버튼 바로 아래, 오른쪽에 붙인다.
            Image border = UiFactory.CreatePanel(transform, "Panel", BorderColor);
            _panel = border.gameObject;
            var borderRect = border.rectTransform;
            borderRect.anchorMin = borderRect.anchorMax = borderRect.pivot = new Vector2(1f, 1f);
            borderRect.anchoredPosition = buttonPosition + new Vector2(0f, -76f);
            var borderLayout = _panel.AddComponent<VerticalLayoutGroup>();
            borderLayout.padding = new RectOffset(2, 2, 2, 2);
            borderLayout.childControlWidth = borderLayout.childControlHeight = true;
            borderLayout.childForceExpandWidth = true;
            borderLayout.childForceExpandHeight = false;
            var fit = _panel.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image fill = UiFactory.CreatePanel(_panel.transform, "Fill", PanelColor);
            var list = fill.gameObject.AddComponent<VerticalLayoutGroup>();
            list.padding = new RectOffset(SidePadding, SidePadding, 14, 14);
            list.spacing = 8f;
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            fill.gameObject.AddComponent<LayoutElement>().preferredWidth = PanelWidth;

            AddHeader(fill.transform);
            if (catalog != null)
            {
                foreach (StatusVisual visual in catalog.Visuals)
                {
                    if (visual != null && !string.IsNullOrWhiteSpace(visual.Description))
                        AddRow(fill.transform, visual);
                }
            }

            _panel.SetActive(false);
        }

        private void AddHeader(Transform parent)
        {
            RectTransform header = UiFactory.CreateRect(parent, "Header");
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            TextMeshProUGUI title = UiFactory.CreateText(header, "Title", 28f, TextAlignmentOptions.Left);
            title.text = "상태이상";
            title.fontStyle = FontStyles.Bold;
            UiFactory.Stretch(title.rectTransform);

            Button close = UiFactory.CreateButton(header, "Close", "X", new Color(1f, 1f, 1f, 0.08f), Toggle);
            close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 26f;
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
        }

        // 한 줄: 아이콘(상태 색) + 이름(상태 색) + 설명. 설명이 길면 줄을 바꾼다.
        private static void AddRow(Transform parent, StatusVisual visual)
        {
            RectTransform row = UiFactory.CreateRect(parent, visual.StatusId);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = RowSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.gameObject.AddComponent<LayoutElement>().minHeight = RowHeight;

            Image icon = UiFactory.CreatePanel(row, "Icon", visual.Color);
            icon.sprite = visual.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconSize = icon.gameObject.AddComponent<LayoutElement>();
            iconSize.minWidth = iconSize.preferredWidth = iconSize.preferredHeight = IconWidth;
            if (visual.Icon == null)
                icon.color = Color.clear;

            TextMeshProUGUI name = UiFactory.CreateText(row, "Name", 24f, TextAlignmentOptions.Left);
            name.text = visual.DisplayName;
            name.color = visual.Color;
            name.fontStyle = FontStyles.Bold;
            var nameSize = name.gameObject.AddComponent<LayoutElement>();
            nameSize.minWidth = nameSize.preferredWidth = NameWidth; // 설명 칸 시작을 맞춘다
            nameSize.flexibleWidth = 0f;

            TextMeshProUGUI description = UiFactory.CreateText(row, "Description", 22f, TextAlignmentOptions.Left);
            description.text = visual.Description;
            description.enableWordWrapping = true;
            description.alignment = TextAlignmentOptions.MidlineLeft;
            var descriptionSize = description.gameObject.AddComponent<LayoutElement>();
            descriptionSize.minWidth = descriptionSize.preferredWidth = DescriptionWidth;
            descriptionSize.flexibleWidth = 0f;
        }

        private void Toggle()
        {
            _panel.SetActive(!_panel.activeSelf);
        }
    }
}
