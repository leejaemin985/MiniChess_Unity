using System;

namespace MiniChess
{
    // Match data shared by all unit types. Every unit counts the same: a side loses
    // when all of its units are dead, so there is no leader designation.
    public sealed class UnitState
    {
        public PlayerId Owner { get; }
        public int UnitIndex { get; }
        public UnitRole Role { get; }
        public int MaxHP { get; }
        public int HP { get; private set; }
        public int AttackDamage { get; }
        public int AttackRange { get; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public bool IsAlive => HP > 0;
        public bool HasMovedThisTurn { get; internal set; }
        public bool HasCombatActedThisTurn { get; internal set; }
        public int MoveTilesThisTurn { get; internal set; }
        // Set when a trap interrupts this unit's walk: it is stuck where it stopped for
        // the rest of the turn, though it may still fight from there.
        public bool MovementStoppedThisTurn { get; internal set; }
        // Longest single move this turn, which is what Impact keys off.
        public int LongestMoveThisTurn { get; internal set; }
        // Scorch left by a basic attack from a side holding the capture point. It lands
        // at the start of this unit's own next turn, whether or not the attacker still
        // owns the point by then: the burn was already applied.
        public int AttackBurnDamage { get; internal set; }
        public int AttackBurnAtOwnTurn { get; internal set; }
        // Spatial Echo charges: each one shaves 1 off the next hit taken.
        public int EchoCharges { get; internal set; }
        // Set when a trap catches this unit, so the trapper can follow up harder.
        public PlayerId TrapMarkOwner { get; private set; }
        public int TrapMarkOwnerIndex { get; private set; } = -1;
        public int TrapMarkExpiresAfterOwnerTurn { get; private set; }
        public UnitDefinition Definition { get; }
        public int Shield { get; private set; }
        public int ShieldExpiresAfterOwnerTurn { get; private set; }
        public event Action Changed;

        public UnitState(PlayerId owner, int unitIndex, UnitDefinition definition, int x, int y)
            : this(owner, unitIndex, (definition ?? throw new ArgumentNullException(nameof(definition))).Role,
                definition.MaxHP, definition.AttackDamage, definition.AttackRange, x, y)
        {
            Definition = definition;
        }

        public UnitState(PlayerId owner, int unitIndex, UnitRole role,
            int maxHP, int attackDamage, int attackRange, int x, int y)
        {
            if (owner != PlayerId.PlayerOne && owner != PlayerId.PlayerTwo)
                throw new ArgumentOutOfRangeException(nameof(owner));
            if (unitIndex < 0 || unitIndex >= GameState.UnitsPerPlayer)
                throw new ArgumentOutOfRangeException(nameof(unitIndex));
            if (!Enum.IsDefined(typeof(UnitRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (maxHP <= 0) throw new ArgumentOutOfRangeException(nameof(maxHP));
            if (attackDamage < 0) throw new ArgumentOutOfRangeException(nameof(attackDamage));
            if (attackRange < 1) throw new ArgumentOutOfRangeException(nameof(attackRange));
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
            Owner = owner;
            UnitIndex = unitIndex;
            Role = role;
            MaxHP = maxHP;
            HP = maxHP;
            AttackDamage = attackDamage;
            AttackRange = attackRange;
            X = x;
            Y = y;
            Definition = new UnitDefinition(UnitKind.Test, "테스트", role, maxHP,
                attackDamage, attackRange, UnitSkill.None, "없음");
        }

        // Low-level mutation for effects/tests; gameplay attacks go through BattleSystem.
        public int TakeDamage(int amount)
        {
            return ApplyDamage(amount, true);
        }

        internal int ApplyDamage(int amount, bool notify)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!IsAlive) return 0;
            // Spatial Echo softens the hit before any shield spends itself on it.
            if (EchoCharges > 0 && amount > 0)
            {
                EchoCharges--;
                amount = Math.Max(0, amount - 1);
            }
            int absorbed = Math.Min(Shield, amount);
            Shield -= absorbed;
            amount -= absorbed;
            int applied = Math.Min(HP, amount);
            if (applied == 0 && absorbed == 0) return 0;
            HP -= applied;
            if (notify) Changed?.Invoke();
            return applied;
        }

        public int Heal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!IsAlive) return 0;
            int applied = Math.Min(MaxHP - HP, amount);
            if (applied == 0) return 0;
            HP += applied;
            Changed?.Invoke();
            return applied;
        }

        // Only a validated board action should change coordinates.
        internal void SetPosition(int x, int y, bool notify = true)
        {
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
            if (X == x && Y == y) return;
            X = x;
            Y = y;
            if (notify) Changed?.Invoke();
        }

        internal void GrantShield(int amount, int expiresAfterOwnerTurn)
        {
            Shield = amount;
            ShieldExpiresAfterOwnerTurn = expiresAfterOwnerTurn;
        }

        internal void ExpireShield(int completedOwnerTurn)
        {
            if (Shield == 0 || completedOwnerTurn < ShieldExpiresAfterOwnerTurn) return;
            Shield = 0;
            ShieldExpiresAfterOwnerTurn = 0;
        }

        // A standing shield from a held capture point: raised to at least amount and
        // given no expiry. A bigger skill shield is left alone with its own expiry, and
        // once that lapses the aura is restored at the owner's next turn start.
        internal void GrantAuraShield(int amount)
        {
            if (Shield >= amount) return;
            Shield = amount;
            ShieldExpiresAfterOwnerTurn = int.MaxValue;
        }

        internal void RemoveAuraShield(int amount)
        {
            Shield = Math.Max(0, Shield - amount);
            if (Shield == 0) ShieldExpiresAfterOwnerTurn = 0;
        }

        internal void MarkTrapped(PlayerId owner, int ownerIndex, int expiresAfterOwnerTurn)
        {
            TrapMarkOwner = owner;
            TrapMarkOwnerIndex = ownerIndex;
            TrapMarkExpiresAfterOwnerTurn = expiresAfterOwnerTurn;
        }

        internal void ExpireTrapMark(PlayerId owner, int completedOwnerTurn)
        {
            if (TrapMarkOwnerIndex < 0 || TrapMarkOwner != owner
                || completedOwnerTurn < TrapMarkExpiresAfterOwnerTurn) return;
            TrapMarkOwnerIndex = -1;
            TrapMarkExpiresAfterOwnerTurn = 0;
        }

        public bool IsTrapMarkedBy(UnitState hunter) => hunter != null && TrapMarkOwnerIndex >= 0
            && TrapMarkOwner == hunter.Owner && TrapMarkOwnerIndex == hunter.UnitIndex;

        public UnitState Clone()
        {
            var copy = (UnitState)MemberwiseClone();
            copy.Changed = null; // AI must not notify the live scene.
            return copy;
        }

        internal void NotifyChanged() => Changed?.Invoke();
    }
}
