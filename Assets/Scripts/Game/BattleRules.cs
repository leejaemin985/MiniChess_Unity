using System;

namespace MiniChess
{
    // Provisional balance values, not fixed numbers from the v0.1 design.
    public sealed class BattleRules
    {
        public int InitialCost { get; }
        public int SkillCost { get; }
        public int ShieldAmount { get; }
        public ActionPointRules AP { get; }
        public CapturePointRules Capture { get; }
        public BattleRules(int initialCost = 6, int skillCost = 2, int shieldAmount = 1,
            ActionPointRules ap = null, CapturePointRules capture = null)
        {
            if (initialCost < 0) throw new ArgumentOutOfRangeException(nameof(initialCost));
            if (skillCost < 1) throw new ArgumentOutOfRangeException(nameof(skillCost));
            if (shieldAmount < 1) throw new ArgumentOutOfRangeException(nameof(shieldAmount));
            InitialCost = initialCost; SkillCost = skillCost; ShieldAmount = shieldAmount;
            AP = ap ?? new ActionPointRules();
            Capture = capture ?? new CapturePointRules();
        }
    }
}
