using UnityEngine;

namespace MiniChess
{
    [CreateAssetMenu(fileName = "BattleRules", menuName = "Mini Chess/Battle Rules")]
    public sealed class BattleRulesAsset : ScriptableObject
    {
        [Header("Match-wide skill resource")]
        [SerializeField, Min(0), Tooltip("Skill resource each side starts with. It does not regenerate.")]
        private int initialCost = 6;
        [SerializeField, Min(1), Tooltip("Fallback skill resource cost for units without an override.")]
        private int skillCost = 2;
        [SerializeField, Min(1), Tooltip("Fallback Shield absorption for units whose Skill Power is 0.")]
        private int shieldAmount = 1;
        [Header("Action points")]
        [SerializeField, Tooltip("Leave empty to use the built-in 6/4/4 defaults.")]
        private ActionPointRulesAsset actionPointRules = null;
        [Header("Capture point")]
        [SerializeField, Tooltip("Leave empty for a two-round hold with no buff attached.")]
        private CapturePointRulesAsset capturePointRules = null;

        // Call on the main thread. The result is immutable, so a running match and an
        // in-flight bot search never observe later inspector edits.
        public BattleRules CreateRules() => new BattleRules(Mathf.Max(0, initialCost),
            Mathf.Max(1, skillCost), Mathf.Max(1, shieldAmount),
            actionPointRules != null ? actionPointRules.CreateRules() : new ActionPointRules(),
            capturePointRules != null ? capturePointRules.CreateRules() : new CapturePointRules());
    }
}
