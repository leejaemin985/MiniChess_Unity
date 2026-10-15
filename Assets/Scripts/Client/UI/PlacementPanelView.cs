using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Interaction;
using MiniChess.Core.Common;
using MiniChess.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 배치 단계 UI. 아래에서부터 [캐릭터 버튼 격자] → [역할 탭] → [안내] 순으로 쌓고, 오른쪽 아래에 확정 버튼을 둔다.
    /// 캐릭터는 역할(전투형/수호형/유틸형) 탭으로 나눠, 누른 탭의 캐릭터만 보여준다. 열린 탭을 다시 누르면 닫힌다.
    /// 한 역할의 캐릭터가 많으면 줄을 바꿔 위로 쌓는다.
    /// 입력은 PlacementController 에 넘기고, 배치 단계가 아니면 숨는다.
    /// </summary>
    public class PlacementPanelView : MonoBehaviour
    {
        private static readonly Color AvailableColor = new Color(0.15f, 0.35f, 0.55f, 0.95f);
        private static readonly Color SelectedColor = new Color(0.95f, 0.55f, 0.1f, 1f);
        private static readonly Color PlacedColor = new Color(0.2f, 0.5f, 0.3f, 0.95f);
        private static readonly Color DisabledColor = new Color(0.25f, 0.25f, 0.28f, 0.8f);
        private static readonly Color TabColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
        private static readonly Color OpenTabColor = new Color(0.3f, 0.3f, 0.38f, 1f);

        /// <summary>탭 순서와 표시 이름. 코어의 Control(제어형)은 화면에서 유틸형으로 부른다.</summary>
        private static readonly (CharacterRole Role, string Label)[] Roles =
        {
            (CharacterRole.Combat, "전투형"),
            (CharacterRole.Guardian, "수호형"),
            (CharacterRole.Control, "유틸형"),
        };

        private const float ButtonWidth = 190f;
        private const float ButtonHeight = 90f;
        private const float ButtonSpacing = 200f;
        private const float RowSpacing = 100f;
        private const int MaxPerRow = 8;
        private const float BottomMargin = 30f;
        private const float TabWidth = 220f;
        private const float TabHeight = 60f;
        private const float TabSpacing = 230f;

        private GameSession _session;
        private PlacementController _placement;

        private RectTransform _root;
        private TextMeshProUGUI _title;
        private Button _confirmButton;
        private readonly List<(CharacterRole Role, Button Button, Image Background, TextMeshProUGUI Label)> _tabs =
            new List<(CharacterRole, Button, Image, TextMeshProUGUI)>();
        private readonly List<(CharacterDefinition Character, Button Button, Image Background)> _buttons =
            new List<(CharacterDefinition, Button, Image)>();

        /// <summary>열린 역할 탭. 모두 닫혀 있으면 null.</summary>
        private CharacterRole? _openRole = CharacterRole.Combat;

        public static PlacementPanelView Create(Transform canvasRoot, GameSession session, PlacementController placement)
        {
            RectTransform root = UiFactory.CreateRect(canvasRoot, "PlacementPanel");
            UiFactory.Stretch(root);

            var view = root.gameObject.AddComponent<PlacementPanelView>();
            view.Build(root);
            view.Bind(session, placement);
            return view;
        }

        private void Build(RectTransform root)
        {
            _root = root;

            _title = UiFactory.CreateText(root, "Title", 34f, TextAlignmentOptions.Bottom);

            for (int i = 0; i < Roles.Length; i++)
            {
                CharacterRole role = Roles[i].Role;
                Button tab = UiFactory.CreateButton(root, $"Tab {role}", Roles[i].Label, TabColor, () => ToggleRole(role));
                TextMeshProUGUI label = tab.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 28f;
                _tabs.Add((role, tab, (Image)tab.targetGraphic, label));
            }

            _confirmButton = UiFactory.CreateButton(root, "Confirm", "배치 확정", SelectedColor, () => _placement.Confirm());
            UiFactory.Place((RectTransform)_confirmButton.transform, new Vector2(1f, 0f), new Vector2(-40f, 150f), new Vector2(280f, 90f));
        }

        private void Bind(GameSession session, PlacementController placement)
        {
            _session = session;
            _placement = placement;
            _placement.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_placement != null)
                _placement.Changed -= Refresh;
        }

        private void ToggleRole(CharacterRole role)
        {
            _openRole = _openRole == role ? (CharacterRole?)null : role;
            Refresh();
        }

        private void Refresh()
        {
            Team? team = _placement.PlacingTeam;
            _root.gameObject.SetActive(team.HasValue);
            if (!team.HasValue)
                return;

            EnsureButtons();

            float gridTop = LayoutCharacterButtons();
            float tabsY = gridTop + 10f;
            LayoutTabs(tabsY);
            UiFactory.Place(_title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, tabsY + TabHeight + 10f), new Vector2(1800f, 90f));

            string teamLabel = team.Value == Team.Player1 ? "PLAYER 1" : "PLAYER 2";
            _title.text = $"{teamLabel} 배치  ({_placement.Pending.Count}/{_placement.RequiredCount})\n"
                + "<size=75%>역할 탭에서 캐릭터를 고르고 하이라이트된 자기 시작 칸을 클릭하세요. 놓인 칸을 다시 클릭하면 뺍니다.</size>";

            _confirmButton.interactable = _placement.CanConfirm;
        }

        /// <summary>열린 탭의 캐릭터 버튼만 보이게 배치한다. 버튼 격자의 위쪽 끝(y)을 반환한다.</summary>
        private float LayoutCharacterButtons()
        {
            List<int> visible = new List<int>();
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool show = _openRole.HasValue && _buttons[i].Character.Role == _openRole.Value;
                _buttons[i].Button.gameObject.SetActive(show);
                if (show)
                    visible.Add(i);
            }

            int rows = (visible.Count + MaxPerRow - 1) / MaxPerRow;
            for (int n = 0; n < visible.Count; n++)
            {
                int row = n / MaxPerRow;
                int column = n % MaxPerRow;
                int inRow = Math.Min(MaxPerRow, visible.Count - row * MaxPerRow);

                // 첫 줄이 가장 위, 마지막 줄이 화면 아래.
                float x = (column - (inRow - 1) * 0.5f) * ButtonSpacing;
                float y = BottomMargin + (rows - 1 - row) * RowSpacing;

                (CharacterDefinition character, Button button, Image background) = _buttons[visible[n]];
                UiFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(ButtonWidth, ButtonHeight));
                background.color = GetCharacterColor(character);
                button.interactable = PlacementController.IsConfigured(character);
            }

            return rows == 0 ? BottomMargin : BottomMargin + (rows - 1) * RowSpacing + ButtonHeight;
        }

        /// <summary>역할 탭을 가로로 배치한다. 탭에는 그 역할에서 놓은 수를 함께 표시한다.</summary>
        private void LayoutTabs(float y)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                (CharacterRole role, Button tab, Image background, TextMeshProUGUI label) = _tabs[i];

                float x = (i - (_tabs.Count - 1) * 0.5f) * TabSpacing;
                UiFactory.Place((RectTransform)tab.transform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(TabWidth, TabHeight));

                int placed = _buttons.Count(entry => entry.Character.Role == role && _placement.IsPlaced(entry.Character.Id));
                string arrow = _openRole == role ? "▼" : "▲";
                label.text = placed > 0 ? $"{Roles[i].Label} ({placed}) {arrow}" : $"{Roles[i].Label} {arrow}";
                background.color = _openRole == role ? OpenTabColor : TabColor;
            }
        }

        private Color GetCharacterColor(CharacterDefinition character)
        {
            if (!PlacementController.IsConfigured(character))
                return DisabledColor;
            if (_placement.SelectedCharacterId == character.Id)
                return SelectedColor;
            if (_placement.IsPlaced(character.Id))
                return PlacedColor;
            return AvailableColor;
        }

        /// <summary>경기의 캐릭터 목록으로 버튼을 만든다. 새 경기에서 목록이 바뀌면 다시 만든다.</summary>
        private void EnsureButtons()
        {
            IReadOnlyList<CharacterDefinition> characters = _session.Game.GetCharacters();
            if (_buttons.Count == characters.Count && _buttons.Select(b => b.Character.Id).SequenceEqual(characters.Select(c => c.Id)))
                return;

            foreach (var entry in _buttons)
                Destroy(entry.Button.gameObject);
            _buttons.Clear();

            foreach (CharacterDefinition character in characters)
            {
                Button button = UiFactory.CreateButton(_root, $"Character {character.Id}", character.Name, AvailableColor,
                    () => _placement.SelectCharacter(character.Id));

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 22f;
                label.enableWordWrapping = true;
                label.margin = new Vector4(8f, 4f, 8f, 4f);

                _buttons.Add((character, button, (Image)button.targetGraphic));
            }
        }
    }
}
