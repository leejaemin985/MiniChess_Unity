using UnityEngine;

namespace MiniChess
{
    // Base class for the capture buff itself, so each one can be authored as its own
    // asset and dropped into CapturePointRulesAsset. Subclass it, add
    // [CreateAssetMenu], and override only the hooks that matter.
    //
    // Read ICapturePointEffect first. These hooks run inside the bot's search, on a
    // background thread, against cloned states: touch nothing but the GameState handed
    // in, and keep it deterministic. A Unity API call in here throws off the main
    // thread, and anything non-deterministic makes the search and the real match
    // disagree about what happened.
    public abstract class CapturePointEffectAsset : ScriptableObject, ICapturePointEffect
    {
        public abstract string Describe(BattleRules rules);
        public virtual void OnCaptured(GameState state, PlayerId owner) { }
        public virtual void OnLost(GameState state, PlayerId previousOwner) { }
        public virtual void OnOwnerTurnStart(GameState state, PlayerId owner) { }
        public virtual void OnBasicAttack(GameState state, UnitState attacker, UnitState target) { }
    }
}
