using System.Text;

namespace MiniChess
{
    // Runs several effects as one, so a capture can carry more than a single buff.
    // Order is the order given, and every hook reaches every effect.
    public sealed class CompositeCapturePointEffect : ICapturePointEffect
    {
        private readonly ICapturePointEffect[] effects;

        public CompositeCapturePointEffect(params ICapturePointEffect[] effects)
        {
            this.effects = effects ?? new ICapturePointEffect[0];
        }

        public int Count => effects.Length;

        // Lets a HUD list the buffs one per line instead of showing one joined string.
        public System.Collections.Generic.IReadOnlyList<ICapturePointEffect> Effects => effects;

        public string Describe(BattleRules rules)
        {
            if (effects.Length == 0) return NoCapturePointEffect.Instance.Describe(rules);
            var text = new StringBuilder();
            foreach (ICapturePointEffect effect in effects)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(effect.Describe(rules));
            }
            return text.ToString();
        }

        public void OnCaptured(GameState state, PlayerId owner)
        {
            foreach (ICapturePointEffect effect in effects) effect.OnCaptured(state, owner);
        }

        public void OnLost(GameState state, PlayerId previousOwner)
        {
            foreach (ICapturePointEffect effect in effects) effect.OnLost(state, previousOwner);
        }

        public void OnOwnerTurnStart(GameState state, PlayerId owner)
        {
            foreach (ICapturePointEffect effect in effects) effect.OnOwnerTurnStart(state, owner);
        }

        public void OnBasicAttack(GameState state, UnitState attacker, UnitState target)
        {
            foreach (ICapturePointEffect effect in effects) effect.OnBasicAttack(state, attacker, target);
        }
    }

    // Capture buff 1: every basic attack by the owning side leaves a scorch that lands
    // at the start of the victim's next turn. Only basic attacks, not skills. Two hits
    // before the tick stack, so the victim pays for both.
    //
    // The effect only writes the pending damage; TurnSystem applies it at the turn
    // start. That way losing the point does not cancel a burn already inflicted, and
    // nothing has to run during turn resolution.
    public sealed class BurnOnAttackCaptureEffect : ICapturePointEffect
    {
        public int Damage { get; }

        public BurnOnAttackCaptureEffect(int damage = 2)
        {
            if (damage < 1) throw new System.ArgumentOutOfRangeException(nameof(damage));
            Damage = damage;
        }

        public string Describe(BattleRules rules) => "화상: 기본 공격이 다음 상대 턴에 " + Damage + " 추가 피해";

        public void OnCaptured(GameState state, PlayerId owner) { }
        public void OnLost(GameState state, PlayerId previousOwner) { }
        public void OnOwnerTurnStart(GameState state, PlayerId owner) { }

        public void OnBasicAttack(GameState state, UnitState attacker, UnitState target)
        {
            if (target == null || !target.IsAlive) return;
            target.AttackBurnDamage += Damage;
            target.AttackBurnAtOwnTurn = state.GetTurnCount(target.Owner) + 1;
        }
    }

    // Capture buff 2: every living unit on the owning side carries a standing shield.
    // It does not tick away like the Guardian's, and it is topped back up at each of
    // the owner's turn starts, so spending it in a fight is not permanent. Losing the
    // point takes it back.
    public sealed class PermanentShieldCaptureEffect : ICapturePointEffect
    {
        public int Amount { get; }

        public PermanentShieldCaptureEffect(int amount = 3)
        {
            if (amount < 1) throw new System.ArgumentOutOfRangeException(nameof(amount));
            Amount = amount;
        }

        public string Describe(BattleRules rules) => "보호막: 아군 전원 " + Amount + " 유지";

        public void OnCaptured(GameState state, PlayerId owner) => Apply(state, owner);
        public void OnOwnerTurnStart(GameState state, PlayerId owner) => Apply(state, owner);
        public void OnBasicAttack(GameState state, UnitState attacker, UnitState target) { }

        public void OnLost(GameState state, PlayerId previousOwner)
        {
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                UnitState unit = state.GetUnit(previousOwner, index);
                if (unit != null && unit.IsAlive) unit.RemoveAuraShield(Amount);
            }
        }

        private void Apply(GameState state, PlayerId owner)
        {
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                UnitState unit = state.GetUnit(owner, index);
                if (unit != null && unit.IsAlive) unit.GrantAuraShield(Amount);
            }
        }
    }
}
