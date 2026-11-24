using System;
using System.Collections.Generic;
using MiniChess.Bot;
using MiniChess.Client.Bootstrap;
using MiniChess.Core.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 로컬 경기 설정 창: 선공/후공 사람·봇, 팀별 봇 난이도, 테스트 모드.
    /// 시작 화면(로컬 플레이 옆)과 경기 종료 화면(RESTART 옆)의 설정 버튼으로 연다. 에디터 없이(모바일/웹 빌드) 테스트 조건을 바꾸기 위한 창.
    /// 값은 GameSession 의 Inspector 값을 바로 바꾸고 저장하지 않는다. 다음 로컬 경기 시작부터 쓰인다.
    /// 다른 HUD 위에 뜨도록 자체 캔버스(정렬 순서 높음)를 쓴다.
    /// </summary>
    public class LocalSettingsPanelView : MonoBehaviour
    {
        private const int SortingOrder = 100;
        private const float WindowWidth = 900f;
        private const float RowHeight = 84f;

        private static readonly Color SelectedFill = new Color(0.2f, 0.5f, 0.3f, 1f);

        /// <summary>한 줄의 선택지 버튼들. 지금 값과 같은 버튼만 강조한다.</summary>
        private sealed class ChoiceRow
        {
            public Button[] Buttons;
            public Func<int> Get;
        }

        private GameSession _session;
        private GameObject _overlay;
        private readonly List<ChoiceRow> _rows = new List<ChoiceRow>();
        private GameObject _player1DifficultyRow;
        private GameObject _player2DifficultyRow;

        public static LocalSettingsPanelView Create(Transform parent, GameSession session)
        {
            Canvas canvas = UiFactory.CreateCanvas(parent, "LocalSettingsCanvas");
            canvas.sortingOrder = SortingOrder;

            var view = canvas.gameObject.AddComponent<LocalSettingsPanelView>();
            view._session = session;
            view.Build(canvas.transform);
            view._overlay.SetActive(false);
            return view;
        }

        public void Open()
        {
            Refresh();
            _overlay.SetActive(true);
        }

        private void Close()
        {
            _overlay.SetActive(false);
        }

        private void Refresh()
        {
            foreach (ChoiceRow row in _rows)
            {
                int selected = row.Get();
                for (int i = 0; i < row.Buttons.Length; i++)
                    ((Image)row.Buttons[i].targetGraphic).color = i == selected ? SelectedFill : HudPalette.ButtonFill;
            }

            _player1DifficultyRow.SetActive(_session.GetControl(Team.Player1) == PlayerControl.Bot);
            _player2DifficultyRow.SetActive(_session.GetControl(Team.Player2) == PlayerControl.Bot);
        }

        #region Build

        private void Build(Transform root)
        {
            RectTransform overlay = UiFactory.CreateRect(root, "LocalSettings");
            UiFactory.Stretch(overlay);
            _overlay = overlay.gameObject;

            // 뒤 화면 입력을 막는 어두운 바탕. 바탕을 누르면 닫는다.
            // 창의 자식으로 두면 창 안 클릭이 바탕 버튼까지 올라가 닫히므로, 창과 형제로 둔다.
            Image dim = UiFactory.CreatePanel(overlay, "Dim", new Color(0f, 0f, 0f, 0.6f));
            UiFactory.Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            // 뒤의 접속 패널이 비치지 않게 불투명하게 칠한다.
            Color windowColor = HudPalette.Panel;
            windowColor.a = 1f;
            Image window = UiFactory.CreatePanel(overlay, "Window", windowColor);
            window.gameObject.AddComponent<RoundedFrame>().Thickness = 0f;
            UiFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowWidth, 0f));

            // 봇 난이도 줄은 봇일 때만 보이므로, 보이는 줄만큼 창 높이가 맞춰지게 세로 배치를 쓴다.
            var layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 36, 36);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            window.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(window.transform, "Title", "로컬 경기 설정", 52f, 80f).fontStyle = FontStyles.Bold;

            AddControlRow(window.transform, "선공 (PLAYER 1)", Team.Player1);
            _player1DifficultyRow = AddDifficultyRow(window.transform, "  봇 난이도", Team.Player1);
            AddControlRow(window.transform, "후공 (PLAYER 2)", Team.Player2);
            _player2DifficultyRow = AddDifficultyRow(window.transform, "  봇 난이도", Team.Player2);

            AddText(window.transform, "TestHeader", "테스트 모드 (사람 팀에만 적용)", 34f, 64f).color = HudPalette.ButtonBorder;
            AddToggleRow(window.transform, "AP 무한", () => _session.TestFreeAp, value => _session.TestFreeAp = value);
            AddToggleRow(window.transform, "행동 제한 없음", () => _session.TestUnlimitedActions, value => _session.TestUnlimitedActions = value);

            AddText(window.transform, "Hint", "다음 경기 시작부터 적용됩니다", 30f, 56f).color = HudPalette.ButtonBorder;

            Button close = UiFactory.CreateButton(window.transform, "Close", "닫기", SelectedFill, Close);
            AddLayoutHeight(close.gameObject, 96f);
        }

        private void AddControlRow(Transform parent, string label, Team team)
        {
            AddChoiceRow(parent, label, new[] { "사람", "봇" },
                () => _session.GetControl(team) == PlayerControl.Bot ? 1 : 0,
                index => _session.SetControl(team, index == 1 ? PlayerControl.Bot : PlayerControl.Human));
        }

        /// <summary>쉬움/보통/어려움. Inspector 에서 Custom 으로 둔 경우는 어느 버튼도 강조하지 않는다(고르면 바뀐다).</summary>
        private GameObject AddDifficultyRow(Transform parent, string label, Team team)
        {
            BotDifficulty[] options = { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };
            return AddChoiceRow(parent, label, new[] { "쉬움", "보통", "어려움" },
                () => Array.IndexOf(options, _session.GetBotDifficulty(team)),
                index => _session.SetBotDifficulty(team, options[index]));
        }

        private void AddToggleRow(Transform parent, string label, Func<bool> get, Action<bool> set)
        {
            AddChoiceRow(parent, label, new[] { "끔", "켬" }, () => get() ? 1 : 0, index => set(index == 1));
        }

        /// <summary>왼쪽 이름 + 오른쪽 선택지 버튼 한 줄. 버튼을 누르면 값을 바꾸고 창 전체를 다시 그린다.</summary>
        private GameObject AddChoiceRow(Transform parent, string label, string[] options, Func<int> get, Action<int> set)
        {
            RectTransform row = UiFactory.CreateRect(parent, label.Trim());
            AddLayoutHeight(row.gameObject, RowHeight);

            TextMeshProUGUI name = UiFactory.CreateText(row, "Label", 36f, TextAlignmentOptions.MidlineLeft);
            name.text = label;
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0.4f, 1f);
            name.rectTransform.offsetMin = Vector2.zero;
            name.rectTransform.offsetMax = Vector2.zero;

            const float gap = 12f;
            var buttons = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                Button button = UiFactory.CreateButton(row, options[i], options[i], HudPalette.ButtonFill, () =>
                {
                    set(index);
                    Refresh();
                });
                button.targetGraphic.gameObject.AddComponent<RoundedFrame>().Thickness = 0f;

                // 오른쪽 60% 를 선택지 수로 나눈다.
                var rect = (RectTransform)button.transform;
                float width = 0.6f / options.Length;
                rect.anchorMin = new Vector2(0.4f + width * i, 0f);
                rect.anchorMax = new Vector2(0.4f + width * (i + 1), 1f);
                rect.offsetMin = new Vector2(gap * 0.5f, 0f);
                rect.offsetMax = new Vector2(-gap * 0.5f, 0f);
                buttons[i] = button;
            }

            _rows.Add(new ChoiceRow { Buttons = buttons, Get = get });
            return row.gameObject;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string text, float fontSize, float height)
        {
            TextMeshProUGUI label = UiFactory.CreateText(parent, name, fontSize, TextAlignmentOptions.Center);
            label.text = text;
            AddLayoutHeight(label.gameObject, height);
            return label;
        }

        private static void AddLayoutHeight(GameObject target, float height)
        {
            var element = target.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        #endregion
    }
}
