using System.Collections.Generic;
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
    /// 배치 단계 UI: 화면 아래에 고를 수 있는 캐릭터 버튼, 그 위에 안내, 오른쪽 아래에 확정 버튼.
    /// 입력은 PlacementController 에 넘기고, 배치 단계가 아니면 숨는다.
    /// </summary>
    public class PlacementPanelView : MonoBehaviour
    {
        private static readonly Color AvailableColor = new Color(0.15f, 0.35f, 0.55f, 0.95f);
        private static readonly Color SelectedColor = new Color(0.95f, 0.55f, 0.1f, 1f);
        private static readonly Color PlacedColor = new Color(0.2f, 0.5f, 0.3f, 0.95f);
        private static readonly Color DisabledColor = new Color(0.25f, 0.25f, 0.28f, 0.8f);
        private const float ButtonWidth = 190f;
        private const float ButtonSpacing = 200f;

        private GameSession _session;
        private PlacementController _placement;

        private RectTransform _root;
        private TextMeshProUGUI _title;
        private Button _confirmButton;
        private readonly List<(CharacterDefinition Character, Button Button, Image Background, TextMeshProUGUI Label)> _buttons =
            new List<(CharacterDefinition, Button, Image, TextMeshProUGUI)>();

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
            UiFactory.Place(_title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1800f, 90f));

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

        private void Refresh()
        {
            Team? team = _placement.PlacingTeam;
            _root.gameObject.SetActive(team.HasValue);
            if (!team.HasValue)
                return;

            EnsureButtons();

            string teamLabel = team.Value == Team.Player1 ? "PLAYER 1" : "PLAYER 2";
            _title.text = $"{teamLabel} 배치  ({_placement.Pending.Count}/{_placement.RequiredCount})\n"
                + "<size=75%>캐릭터를 고르고 하이라이트된 자기 시작 칸을 클릭하세요. 놓인 칸을 다시 클릭하면 뺍니다.</size>";

            foreach ((CharacterDefinition character, Button button, Image background, TextMeshProUGUI _) in _buttons)
            {
                bool configured = PlacementController.IsConfigured(character);
                button.interactable = configured;

                if (!configured)
                    background.color = DisabledColor;
                else if (_placement.SelectedCharacterId == character.Id)
                    background.color = SelectedColor;
                else if (_placement.IsPlaced(character.Id))
                    background.color = PlacedColor;
                else
                    background.color = AvailableColor;
            }

            _confirmButton.interactable = _placement.CanConfirm;
        }

        /// <summary>경기의 캐릭터 목록으로 버튼을 만든다. 새 경기에서 목록이 바뀌면 다시 만든다.</summary>
        private void EnsureButtons()
        {
            IReadOnlyList<CharacterDefinition> characters = _session.Game.GetCharacters();
            if (_buttons.Count == characters.Count && SameCharacters(characters))
                return;

            foreach (var entry in _buttons)
                Destroy(entry.Button.gameObject);
            _buttons.Clear();

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterDefinition character = characters[i];
                Button button = UiFactory.CreateButton(_root, $"Character {character.Id}", character.Name, AvailableColor,
                    () => _placement.SelectCharacter(character.Id));

                float x = (i - (characters.Count - 1) * 0.5f) * ButtonSpacing;
                UiFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(x, 30f), new Vector2(ButtonWidth, 90f));

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 22f;
                label.enableWordWrapping = true;
                label.margin = new Vector4(8f, 4f, 8f, 4f);

                _buttons.Add((character, button, (Image)button.targetGraphic, label));
            }
        }

        private bool SameCharacters(IReadOnlyList<CharacterDefinition> characters)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (_buttons[i].Character.Id != characters[i].Id)
                    return false;
            }

            return true;
        }
    }
}
