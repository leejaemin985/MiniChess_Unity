using System;

namespace MiniChess
{
    // What a captured point does for its owner. Deliberately empty of balance: the
    // buff itself is still to be designed, so this only fixes the seams it will
    // plug into.
    //
    // Every implementation must obey two rules, because the bot search runs these
    // on a background thread against cloned states:
    //   - Touch nothing but the GameState handed in. No scene objects, no Unity API,
    //     no logging, no statics. A Unity call off the main thread throws.
    //   - Be deterministic. The same state and owner must give the same result, or
    //     the search and the real match will disagree.
    public interface ICapturePointEffect
    {
        // One short Korean line for the HUD, e.g. "점령: 매 턴 AP +1".
        string Describe(BattleRules rules);

        // The moment a side completes the hold. Fires once per capture.
        void OnCaptured(GameState state, PlayerId owner);

        // The owner lost the point, either to the other side or by being pushed off.
        void OnLost(GameState state, PlayerId previousOwner);

        // Start of each of the owner's turns while it still holds the point. This is
        // where a recurring buff belongs.
        void OnOwnerTurnStart(GameState state, PlayerId owner);

        // A basic attack by a unit of the owning side has just landed. Skills do not
        // come through here, and it fires only while that side owns the point.
        void OnBasicAttack(GameState state, UnitState attacker, UnitState target);
    }

    // The stand-in until a buff is designed: capturing is tracked and shown, and
    // nothing else happens. Keeping this rather than allowing null means the rules
    // never have to null-check an effect.
    public sealed class NoCapturePointEffect : ICapturePointEffect
    {
        public static readonly NoCapturePointEffect Instance = new NoCapturePointEffect();
        private NoCapturePointEffect() { }
        public string Describe(BattleRules rules) => "점령 효과 없음";
        public void OnCaptured(GameState state, PlayerId owner) { }
        public void OnLost(GameState state, PlayerId previousOwner) { }
        public void OnOwnerTurnStart(GameState state, PlayerId owner) { }
        public void OnBasicAttack(GameState state, UnitState attacker, UnitState target) { }
    }

    public sealed class CapturePointRules
    {
        // Own turn starts a side must hold the tile through before it counts as
        // captured. One means "still standing there when your next turn comes round".
        public int RoundsToCapture { get; }
        // Whether a completed capture survives the owner stepping off the tile.
        public bool KeepAfterLeaving { get; }
        public ICapturePointEffect Effect { get; }

        public CapturePointRules(int roundsToCapture = 2, bool keepAfterLeaving = false,
            ICapturePointEffect effect = null)
        {
            if (roundsToCapture < 1) throw new ArgumentOutOfRangeException(nameof(roundsToCapture));
            RoundsToCapture = roundsToCapture;
            KeepAfterLeaving = keepAfterLeaving;
            Effect = effect ?? NoCapturePointEffect.Instance;
        }

        public string GetSummary() => "같은 팀 유닛이 " + RoundsToCapture + "라운드 이상 버티면 점령"
            + (KeepAfterLeaving ? " (점령 후 벗어나도 유지)" : " (벗어나면 점령 해제)");
    }
}
