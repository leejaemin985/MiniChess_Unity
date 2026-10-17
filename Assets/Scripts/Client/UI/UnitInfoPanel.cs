using System;
using System.Collections.Generic;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.Common;
using MiniChess.Client.Interaction;
using MiniChess.Client.Visuals;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Skills;
using MiniChess.Core.State;
using MiniChess.Core.Statuses;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 화면 아래 유닛 정보 패널(프리팹). 선택한 유닛, 없으면 정보만 보는 유닛(상대 유닛 등)을 보여준다: 얼굴, 이름, HP, 보호막, 상태효과, 스킬 버튼.
    /// 모양은 프리팹(Resources/UI/UnitInfoPanel)에서 자유롭게 바꾸고, 아래 참조만 유지하면 된다.
    /// 상태 칸은 상태 칸 프리팹, 스킬 버튼은 템플릿(비활성 자식)을 복제해 만든다. 유닛을 고르지 않았으면 Content 를 숨긴다.
    /// 스킬 버튼을 누르면 스킬 지정 모드로 들어가고, 다시 누르면 취소한다(사용 불가면 이유가 토스트로 뜬다).
    /// </summary>
    public class UnitInfoPanel : MonoBehaviour
    {
        public const string ResourcePath = "UI/UnitInfoPanel";
        public const string StatusChipPrefabPath = "UI/StatusChip";

        [Tooltip("유닛을 골랐을 때만 보이는 부분.")]
        [SerializeField] private GameObject content;

        [Header("Unit")]
        [SerializeField] private Image portrait;
        [Tooltip("팀 색으로 칠할 얼굴 테두리/배경. 비워도 된다.")]
        [SerializeField] private Image portraitFrame;
        [Tooltip("얼굴 데이터가 없을 때 보일 자리 표시. 비워도 된다.")]
        [SerializeField] private GameObject portraitPlaceholder;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("HP 비율만큼 가로로 채워지는 막대(앵커로 늘린다).")]
        [SerializeField] private RectTransform hpFill;

        [Header("Shield")]
        [Tooltip("보호막 양만큼 HP 막대 왼쪽부터 덮는 막대(앵커로 늘린다). 비율 기준은 최대 HP.")]
        [SerializeField] private RectTransform shieldFill;
        [Tooltip("보호막이 있을 때만 보이는 아이콘+숫자 묶음.")]
        [SerializeField] private GameObject shieldGroup;
        [SerializeField] private TMP_Text shieldText;

        [Header("Statuses")]
        [SerializeField] private Transform statusContainer;
        [Tooltip("상태 칸 프리팹. 비우면 Resources/UI/StatusChip 을 불러온다.")]
        [SerializeField] private StatusChipView statusChipPrefab;
        [Tooltip("상태 이름/색/아이콘. 비우면 Resources/StatusVisualCatalog 를 불러온다.")]
        [SerializeField] private StatusVisualCatalog statusVisuals;
        [Tooltip("표시 데이터가 없는 상태의 색.")]
        [SerializeField] private Color fallbackStatusColor = new Color(1f, 0.82f, 0.3f);

        [Header("Skills")]
        [SerializeField] private Transform skillContainer;
        [SerializeField] private SkillButtonView skillTemplate;
        [Tooltip("스킬이 없으면 숨길 구분선 등. 비워도 된다.")]
        [SerializeField] private GameObject skillSection;
        [SerializeField] private Color usableColor = new Color(0.2f, 0.25f, 0.35f, 1f);
        [SerializeField] private Color unusableColor = new Color(0.16f, 0.17f, 0.2f, 0.8f);
        [SerializeField] private Color activeColor = new Color(0.95f, 0.55f, 0.1f, 1f);

        private GameSession _session;
        private SelectionController _selection;
        private Func<Team, Color> _teamColor;

        private readonly List<StatusChipView> _chips = new List<StatusChipView>();
        private readonly List<SkillButtonView> _skillButtons = new List<SkillButtonView>();
        private readonly List<string> _skillIds = new List<string>();

        public void Bind(GameSession session, Func<Team, Color> teamColor)
        {
            _session = session;
            _selection = session.Selection;
            _teamColor = teamColor;
            if (statusVisuals == null)
                statusVisuals = Resources.Load<StatusVisualCatalog>(StatusVisualCatalog.ResourcePath);
            if (statusChipPrefab == null)
                statusChipPrefab = Resources.Load<StatusChipView>(StatusChipPrefabPath);

            KoreanFont.ApplyToDefaultTexts(gameObject);
            skillTemplate.gameObject.SetActive(false);

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
            // 선택한 유닛, 없으면 정보만 보는 유닛(상대 유닛 등).
            Unit unit = _selection.Displayed;
            GameState state = _session.State;

            bool visible = unit != null && state != null && !state.IsGameOver && unit.IsPlaced;
            content.SetActive(visible);
            if (!visible)
                return;

            RefreshUnit(unit);
            RefreshStatuses(unit);
            RefreshSkills(unit);
        }

        private void RefreshUnit(Unit unit)
        {
            Sprite face = FindFace(unit);
            portrait.sprite = face;
            portrait.enabled = face != null;
            if (portraitPlaceholder != null)
                portraitPlaceholder.SetActive(face == null);
            if (portraitFrame != null)
                portraitFrame.color = _teamColor(unit.Team);

            nameText.text = unit.Stats.Base.Name;
            hpText.text = $"{unit.Stats.CurrentHp} / {unit.Stats.MaxHp} HP";

            SetFill(hpFill, Ratio(unit.Stats.CurrentHp, unit.Stats.MaxHp));

            int shield = unit.Stats.Shield;
            shieldGroup.SetActive(shield > 0);
            shieldText.text = shield.ToString();
            shieldFill.gameObject.SetActive(shield > 0);
            SetFill(shieldFill, Ratio(shield, unit.Stats.MaxHp));
        }

        /// <summary>상태효과(부여 순). 아이콘/색/이름은 StatusVisualCatalog 에서, 숫자는 남은 횟수(영구 상태는 비움).</summary>
        private void RefreshStatuses(Unit unit)
        {
            var entries = new List<(string Id, string Count)>();
            foreach (StatusEffect status in unit.Statuses)
                entries.Add((status.Definition.Id, status.Definition.IsPermanent ? string.Empty : status.Remaining.ToString()));

            int before = _chips.Count;
            Ensure(_chips, statusChipPrefab, statusContainer, entries.Count);
            for (int i = before; i < _chips.Count; i++)
                KoreanFont.ApplyToDefaultTexts(_chips[i].gameObject);

            for (int i = 0; i < _chips.Count; i++)
            {
                bool used = i < entries.Count;
                _chips[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                (string id, string count) = entries[i];
                StatusVisual visual = statusVisuals != null ? statusVisuals.Find(id) : null;
                _chips[i].Set(
                    visual != null ? visual.Icon : null,
                    visual != null ? visual.DisplayName : id,
                    count,
                    visual != null ? visual.Color : fallbackStatusColor);
            }
        }

        private static float Ratio(int value, int max) => max > 0 ? Mathf.Clamp01((float)value / max) : 0f;

        /// <summary>막대를 왼쪽부터 ratio 만큼 채운다.</summary>
        private static void SetFill(RectTransform fill, float ratio)
        {
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(ratio, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        private void RefreshSkills(Unit unit)
        {
            List<SkillInfo> skills = _session.Game.GetSkills(unit.Id);
            if (skillSection != null)
                skillSection.SetActive(skills.Count > 0);

            int before = _skillButtons.Count;
            Ensure(_skillButtons, skillTemplate, skillContainer, skills.Count);
            for (int i = before; i < _skillButtons.Count; i++)
            {
                int index = i;
                _skillButtons[i].Button.onClick.AddListener(() => OnSkillClicked(index));
                _skillIds.Add(null);
            }

            for (int i = 0; i < _skillButtons.Count; i++)
            {
                bool used = i < skills.Count;
                _skillButtons[i].gameObject.SetActive(used);
                _skillIds[i] = used ? skills[i].Id : null;
                if (!used)
                    continue;

                SkillInfo skill = skills[i];
                // 정보만 보는 유닛(상대 등)의 스킬은 보여주기만 하고 누를 수 없다.
                bool own = unit == _selection.Selected;
                bool usable = own && skill.Usable == SkillFailReason.None;
                bool active = own && _selection.PendingSkillId == skill.Id;
                _skillButtons[i].Button.interactable = own;
                string cost = skill.Definition?.ApCost != null ? $"{skill.Definition.ApCost} AP" : "TBD";

                _skillButtons[i].Set(DisplayName(unit, skill.Id, skill.Definition), cost, null,
                    active ? activeColor : usable ? usableColor : unusableColor);
            }
        }

        private void OnSkillClicked(int index)
        {
            string skillId = _skillIds[index];
            if (skillId == null)
                return;

            if (_selection.PendingSkillId == skillId)
                _selection.CancelSkill();
            else
                _selection.BeginSkill(skillId);
        }

        /// <summary>유닛 얼굴. 소환물은 자기 데이터가 없으면 소환자 얼굴을 쓴다.</summary>
        private Sprite FindFace(Unit unit)
        {
            CharacterVisualCatalog visuals = _session.CharacterVisuals;
            if (visuals == null)
                return null;

            Sprite face = visuals.FindFace(unit.Stats.Base.Id);
            if (face == null && unit.IsSummon && unit.SummonOwner != null)
                face = visuals.FindFace(unit.SummonOwner.Stats.Base.Id);
            return face;
        }

        /// <summary>스킬 정의의 이름(예: "강타"). 정의가 없으면 Id 에서 캐릭터 접두어를 뗀 것.</summary>
        private static string DisplayName(Unit unit, string skillId, SkillDefinition definition)
        {
            if (!string.IsNullOrEmpty(definition?.Name))
                return definition.Name;

            string prefix = unit.Stats.Base.Id + "_";
            return skillId.StartsWith(prefix) ? skillId.Substring(prefix.Length) : skillId;
        }

        /// <summary>템플릿을 복제해 항목을 count 개 이상 확보한다. 남는 항목은 호출한 쪽이 숨긴다.</summary>
        private static void Ensure<T>(List<T> items, T template, Transform container, int count) where T : Component
        {
            while (items.Count < count)
                items.Add(Instantiate(template, container));
        }
    }
}
