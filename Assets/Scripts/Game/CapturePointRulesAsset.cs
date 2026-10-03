using System.Collections.Generic;
using UnityEngine;

namespace MiniChess
{
    // Everything about the capture point lives here, separate from the AP rules, so
    // the hold length and the buffs can be retuned without touching movement or costs.
    [CreateAssetMenu(fileName = "CapturePointRules", menuName = "Mini Chess/Capture Point Rules")]
    public sealed class CapturePointRulesAsset : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("Own turn starts a side must hold the tile through "
            + "before it counts as captured. 1 means it is enough to still be standing there "
            + "when your next turn comes round.")]
        private int roundsToCapture = 2;
        [SerializeField, Tooltip("Leave off and stepping off the tile hands the point back. "
            + "Turn on and a capture is permanent once earned.")]
        private bool keepAfterLeaving = false;

        [Header("Buffs for every unit on the owning side")]
        [SerializeField, Tooltip("Basic attacks leave a scorch that lands at the start of the "
            + "victim's next turn. Skills do not apply it.")]
        private bool burnOnAttack = true;
        [SerializeField, Min(1)] private int burnDamage = 2;
        [SerializeField, Tooltip("Every living unit on the owning side carries a standing "
            + "shield, topped back up at each of the owner's turn starts.")]
        private bool permanentShield = true;
        [SerializeField, Min(1)] private int shieldAmount = 3;

        [Header("Extra buffs")]
        [SerializeField, Tooltip("Custom effects, applied after the two built-in ones. "
            + "Read ICapturePointEffect before writing one.")]
        private CapturePointEffectAsset[] customEffects = null;

        public CapturePointRules CreateRules()
        {
            var effects = new List<ICapturePointEffect>();
            if (burnOnAttack) effects.Add(new BurnOnAttackCaptureEffect(Mathf.Max(1, burnDamage)));
            if (permanentShield) effects.Add(new PermanentShieldCaptureEffect(Mathf.Max(1, shieldAmount)));
            if (customEffects != null)
                foreach (CapturePointEffectAsset asset in customEffects)
                    if (asset != null) effects.Add(asset);
            // One effect needs no wrapper; none at all falls back to the no-op default.
            ICapturePointEffect effect = effects.Count == 0 ? null
                : effects.Count == 1 ? effects[0]
                : new CompositeCapturePointEffect(effects.ToArray());
            return new CapturePointRules(Mathf.Max(1, roundsToCapture), keepAfterLeaving, effect);
        }
    }
}
