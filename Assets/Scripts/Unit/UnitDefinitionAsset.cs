using UnityEngine;

namespace MiniChess
{
    [CreateAssetMenu(fileName = "NewUnit", menuName = "Mini Chess/Unit Definition")]
    public sealed class UnitDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private UnitKind kind = UnitKind.Warrior;
        [SerializeField] private string displayName = "Warrior";
        [SerializeField] private UnitRole role = UnitRole.Combat;
        [SerializeField, Tooltip("Draft slot. One unit per position is drawn for each roster.")]
        private UnitPosition position = UnitPosition.Melee;
        [SerializeField, Tooltip("Always-on effect. Aim uses the separate range bonus below.")]
        private UnitPassive passive = UnitPassive.None;
        [SerializeField, TextArea(2, 4)] private string description = "";
        [Header("Basic Stats")]
        [SerializeField, Min(1)] private int maxHP = 3;
        [SerializeField, Min(0)] private int attackDamage = 1;
        [SerializeField, Min(1)] private int attackRange = 1;
        [SerializeField, Min(0), Tooltip("Bonus basic range before this unit uses its movement action.")]
        private int aimRangeBonus = 0;
        [Header("Skill")]
        [SerializeField] private UnitSkill skill = UnitSkill.StrongStrike;
        [SerializeField] private string skillName = "Strong Strike";
        [SerializeField, Min(0)] private int skillCost = 2;
        [SerializeField, Min(0), Tooltip("0 uses the AP Rules default (3). Set 4 for a stronger skill. Separate from Skill Cost.")]
        private int skillAPCost = 0;
        [SerializeField, Min(1)] private int skillRange = 1;
        [SerializeField, Min(1), Tooltip("Damage, shield absorption, or knockback distance, depending on Skill.")]
        private int skillPower = 2;

        // Call on the main thread when starting a new match. No Unity references escape.
        public UnitDefinition CreateDefinition()
        {
            return new UnitDefinition(kind, string.IsNullOrWhiteSpace(displayName) ? kind.ToString() : displayName,
                role, Mathf.Max(1, maxHP), Mathf.Max(0, attackDamage), Mathf.Max(1, attackRange),
                skill, string.IsNullOrWhiteSpace(skillName) ? skill.ToString() : skillName,
                Mathf.Max(1, skillRange), Mathf.Max(1, skillPower), Mathf.Max(0, skillCost),
                Mathf.Max(0, aimRangeBonus), description, skillAPCost > 0 ? (int?)skillAPCost : null,
                position, passive);
        }
    }
}
