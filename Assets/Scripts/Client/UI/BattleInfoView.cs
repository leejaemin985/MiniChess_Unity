using System;
using System.Collections.Generic;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Visuals;
using MiniChess.Core.Capture;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 전투 정보 창. 위 정보 바의 전투 정보 버튼(TopBarView.InfoClicked)으로 여닫는다.
    ///   - 팀: 양 팀 이름(팀 색), AP(◆ 현재/최대), 점령 진행도. 상대 AP 는 이 창에서만 보인다(내 팀 AP 는 하단 AP 게이지).
    ///   - 상태이상: 모든 상태(StatusVisualCatalog 순서)의 아이콘, 이름, 설명. 설명이 없는 상태는 뺀다.
    /// 열려 있는 동안 상태가 바뀌면 팀 줄을 다시 쓴다. HUD 캔버스에 코드로 만든다.
    /// </summary>
    public class BattleInfoView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.1f, 0.96f);
        private static readonly Color BorderColor = new Color(0.55f, 0.53f, 0.6f, 0.9f);
        private const float PanelWidth = 660f;
        private const float RowHeight = 56f;
        private const float NameWidth = 130f;
        private const float IconWidth = 40f;
        private const float RowSpacing = 12f;
        private const int SidePadding = 18;
        /// <summary>설명 칸 폭: 창 폭에서 여백, 아이콘, 이름, 간격을 뺀 나머지(고정해 왼쪽 정렬이 흐트러지지 않게).</summary>
        private const float DescriptionWidth = PanelWidth - SidePadding * 2 - IconWidth - NameWidth - RowSpacing * 2;

        private GameSession _session;
        private Func<Team, Color> _teamColor;
        private GameObject _panel;
        private readonly List<(Team Team, TextMeshProUGUI Name, TextMeshProUGUI Ap, TextMeshProUGUI Capture)> _teamRows =
            new List<(Team, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI)>();

        public bool IsOpen => _panel.activeSelf;

        /// <param name="root">HUD 캔버스.</param>
        /// <param name="panelPosition">창의 오른쪽 위 모서리 자리(캔버스 오른쪽 위 기준).</param>
        public static BattleInfoView Create(Transform root, GameSession session, Func<Team, Color> teamColor, StatusVisualCatalog catalog, Vector2 panelPosition)
        {
            var view = UiFactory.CreateRect(root, "BattleInfo").gameObject.AddComponent<BattleInfoView>();
            UiFactory.Stretch((RectTransform)view.transform);
            view._session = session;
            view._teamColor = teamColor;
            view.Build(catalog, panelPosition);
            session.StateChanged += view.OnStateChanged;
            return view;
        }

        private void OnDestroy()
        {
            if (_session != null)
                _session.StateChanged -= OnStateChanged;
        }

        public void Toggle()
        {
            _panel.SetActive(!_panel.activeSelf);
            if (_panel.activeSelf)
                RefreshTeams();
        }

        private void OnStateChanged()
        {
            if (_panel.activeSelf)
                RefreshTeams();
        }

        private void RefreshTeams()
        {
            GameState state = _session.State;
            foreach ((Team team, TextMeshProUGUI name, TextMeshProUGUI ap, TextMeshProUGUI capture) in _teamRows)
            {
                string whose = TeamNames.Whose(_session, team);
                name.text = whose == TeamNames.Name(team) ? whose : $"{whose} 팀";
                name.color = _teamColor(team);

                if (state == null)
                {
                    ap.text = string.Empty;
                    capture.text = string.Empty;
                    continue;
                }

                ApPool pool = state.GetPlayer(team).Ap;
                ap.text = $"{HudPalette.Diamond} {pool.Current}/{pool.Max}";

                CaptureState progress = state.Capture;
                capture.text = progress.CapturedBy.HasValue ? (progress.CapturedBy == team ? "점령 완료" : string.Empty)
                    : progress.IsActive ? $"점령 {progress.GetProgress(team)}/{progress.Required}" : string.Empty;
            }
        }

        #region Build

        private void Build(StatusVisualCatalog catalog, Vector2 panelPosition)
        {
            // 창: 테두리 + 안쪽 목록(위에서 아래로). 오른쪽 위에 붙인다.
            Image border = UiFactory.CreatePanel(transform, "Panel", BorderColor);
            _panel = border.gameObject;
            var borderRect = border.rectTransform;
            borderRect.anchorMin = borderRect.anchorMax = borderRect.pivot = new Vector2(1f, 1f);
            borderRect.anchoredPosition = panelPosition;
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
            list.padding = new RectOffset(SidePadding, SidePadding, 14, 18);
            list.spacing = 8f;
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            fill.gameObject.AddComponent<LayoutElement>().preferredWidth = PanelWidth;

            AddHeader(fill.transform, "전투 정보", closable: true);
            AddTeamRow(fill.transform, Team.Player1);
            AddTeamRow(fill.transform, Team.Player2);

            AddHeader(fill.transform, "상태이상", closable: false);
            if (catalog != null)
            {
                foreach (StatusVisual visual in catalog.Visuals)
                {
                    if (visual != null && !string.IsNullOrWhiteSpace(visual.Description))
                        AddStatusRow(fill.transform, visual);
                }
            }

            _panel.SetActive(false);
        }

        private void AddHeader(Transform parent, string text, bool closable)
        {
            RectTransform header = UiFactory.CreateRect(parent, "Header");
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            TextMeshProUGUI title = UiFactory.CreateText(header, "Title", 28f, TextAlignmentOptions.BottomLeft);
            title.text = text;
            title.fontStyle = FontStyles.Bold;
            UiFactory.Stretch(title.rectTransform);

            if (!closable)
                return;

            Button close = UiFactory.CreateButton(header, "Close", "X", new Color(1f, 1f, 1f, 0.08f), Toggle);
            close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 26f;
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
        }

        // 한 줄: 팀 이름(팀 색) + AP + 점령 진행도.
        private void AddTeamRow(Transform parent, Team team)
        {
            RectTransform row = UiFactory.CreateRect(parent, team.ToString());
            row.gameObject.AddComponent<LayoutElement>().minHeight = 48f;

            TextMeshProUGUI name = UiFactory.CreateText(row, "Name", 26f, TextAlignmentOptions.MidlineLeft);
            name.fontStyle = FontStyles.Bold;
            UiFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(240f, 48f));

            TextMeshProUGUI ap = UiFactory.CreateText(row, "Ap", 30f, TextAlignmentOptions.MidlineLeft);
            UiFactory.Place(ap.rectTransform, new Vector2(0f, 0.5f), new Vector2(260f, 0f), new Vector2(180f, 48f));

            TextMeshProUGUI capture = UiFactory.CreateText(row, "Capture", 24f, TextAlignmentOptions.MidlineRight);
            capture.color = new Color(1f, 1f, 1f, 0.75f);
            UiFactory.Place(capture.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(170f, 48f));

            _teamRows.Add((team, name, ap, capture));
        }

        // 한 줄: 아이콘(상태 색) + 이름(상태 색) + 설명. 설명이 길면 줄을 바꾼다.
        private static void AddStatusRow(Transform parent, StatusVisual visual)
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

        #endregion
    }
}
