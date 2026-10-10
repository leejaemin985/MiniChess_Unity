using System.Collections.Generic;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Interaction;
using MiniChess.Core.Actions;
using MiniChess.Core.Skills;
using MiniChess.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 선택한 유닛의 스킬 버튼(화면 아래 가운데). 스킬 수만큼 버튼을 만든다(슬롯 스킬 + 고유 기능). 누르면 스킬 지정 모드로 들어가고, 다시 누르면 취소한다.
    /// 사용할 수 없는 스킬도 누를 수 있으며, 그때는 이유가 토스트로 뜬다.
    /// </summary>
    public class SkillPanelView : MonoBehaviour
    {
        private static readonly Color UsableColor = new Color(0.15f, 0.35f, 0.55f, 0.95f);
        private static readonly Color UnusableColor = new Color(0.25f, 0.25f, 0.28f, 0.8f);
        private static readonly Color ActiveColor = new Color(0.95f, 0.55f, 0.1f, 1f);
        private const float ButtonWidth = 300f;
        private const float ButtonSpacing = 320f;

        private GameSession _session;
        private SelectionController _selection;
        private RectTransform _root;
        private TextMeshProUGUI _title;
        private readonly List<(Button Button, Image Background, TextMeshProUGUI Label)> _buttons =
            new List<(Button, Image, TextMeshProUGUI)>();
        private readonly List<string> _buttonSkillIds = new List<string>();

        public static SkillPanelView Create(Transform canvasRoot, GameSession session)
        {
            RectTransform root = UiFactory.CreateRect(canvasRoot, "SkillPanel");
            UiFactory.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1000f, 170f));

            var view = root.gameObject.AddComponent<SkillPanelView>();
            view.Build(root);
            view.Bind(session);
            return view;
        }

        private void Build(RectTransform root)
        {
            _root = root;
            _title = UiFactory.CreateText(root, "Title", 30f, TextAlignmentOptions.Bottom);
            UiFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1000f, 50f));
        }

        /// <summary>버튼을 count 개 이상 확보한다. 버튼은 지우지 않고 남는 것은 숨긴다.</summary>
        private void EnsureButtons(int count)
        {
            for (int i = _buttons.Count; i < count; i++)
            {
                int index = i;
                Button button = UiFactory.CreateButton(_root, $"Skill{i}", string.Empty, UnusableColor, () => OnSkillClicked(index));

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 28f;
                _buttons.Add((button, (Image)button.targetGraphic, label));
                _buttonSkillIds.Add(null);
            }
        }

        /// <summary>보이는 버튼 count 개를 가운데 정렬로 배치한다.</summary>
        private void LayoutButtons(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * ButtonSpacing;
                UiFactory.Place((RectTransform)_buttons[i].Button.transform, new Vector2(0.5f, 0f), new Vector2(x, 0f), new Vector2(ButtonWidth, 100f));
            }
        }

        private void Bind(GameSession session)
        {
            _session = session;
            _selection = session.Selection;
            _selection.SelectionChanged += Refresh;
            _session.StateChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_selection != null) _selection.SelectionChanged -= Refresh;
            if (_session != null) _session.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            Unit unit = _selection.Selected;
            GameState state = _session.State;

            if (unit == null || state == null || state.IsGameOver)
            {
                _title.text = string.Empty;
                SetButtonsVisible(0);
                return;
            }

            List<(string Id, SkillDefinition Definition)> skills = SkillQueries.GetSkills(state, unit);
            _title.text = skills.Count == 0 ? $"{unit.Stats.Base.Name}  -  스킬 없음" : unit.Stats.Base.Name;
            EnsureButtons(skills.Count);
            SetButtonsVisible(skills.Count);
            LayoutButtons(skills.Count);

            for (int i = 0; i < skills.Count; i++)
            {
                (string id, SkillDefinition definition) = skills[i];
                _buttonSkillIds[i] = id;

                bool usable = UseSkillAction.ValidateUsable(state, unit, id) == SkillFailReason.None;
                bool active = _selection.PendingSkillId == id;
                string cost = definition?.ApCost != null ? $"{definition.ApCost} AP" : "TBD";

                _buttons[i].Label.text = $"{DisplayName(unit, id, definition)}\n<size=75%>{cost}</size>";
                _buttons[i].Background.color = active ? ActiveColor : usable ? UsableColor : UnusableColor;
            }
        }

        private void SetButtonsVisible(int count)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Button.gameObject.SetActive(i < count);
                if (i >= count)
                    _buttonSkillIds[i] = null;
            }
        }

        private void OnSkillClicked(int index)
        {
            string skillId = _buttonSkillIds[index];
            if (skillId == null)
                return;

            if (_selection.PendingSkillId == skillId)
                _selection.CancelSkill();
            else
                _selection.BeginSkill(skillId);
        }

        /// <summary>스킬 정의의 이름(예: "강타"). 정의가 없으면 Id 에서 캐릭터 접두어를 뗀 것("WARRIOR_SMASH" → "SMASH").</summary>
        private static string DisplayName(Unit unit, string skillId, SkillDefinition definition)
        {
            if (!string.IsNullOrEmpty(definition?.Name))
                return definition.Name;

            string prefix = unit.Stats.Base.Id + "_";
            return skillId.StartsWith(prefix) ? skillId.Substring(prefix.Length) : skillId;
        }
    }
}
