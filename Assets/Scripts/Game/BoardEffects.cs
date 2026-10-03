using System;

namespace MiniChess
{
    // Tiles a fire zone covers, owned by the caster that placed it. Pure data so the
    // bot can clone and simulate it exactly like the rest of GameState.
    public sealed class AreaEffect
    {
        public PlayerId Owner { get; }
        public int CasterIndex { get; }
        public int Damage { get; }
        // Removed at the caster's own turn start once their turn count reaches this.
        public int ExpiresAtOwnerTurn { get; internal set; }
        private int[] tilesX;
        private int[] tilesY;
        // Turn number this field last burned each unit, so one field cannot hit the
        // same unit twice in a turn however many of its tiles that unit crosses.
        private int[] burnedOnTurn = new int[2 * GameState.UnitsPerPlayer];

        internal AreaEffect(PlayerId owner, int casterIndex, int[] x, int[] y,
            int expiresAtOwnerTurn, int damage)
        {
            Owner = owner; CasterIndex = casterIndex;
            tilesX = x; tilesY = y;
            ExpiresAtOwnerTurn = expiresAtOwnerTurn; Damage = damage;
        }

        public int TileCount => tilesX.Length;
        public int GetTileX(int i) => tilesX[i];
        public int GetTileY(int i) => tilesY[i];

        public bool Covers(int x, int y)
        {
            for (int i = 0; i < tilesX.Length; i++)
                if (tilesX[i] == x && tilesY[i] == y) return true;
            return false;
        }

        internal bool TryBurn(UnitState unit, int turnNumber)
        {
            int slot = (int)unit.Owner * GameState.UnitsPerPlayer + unit.UnitIndex;
            if (burnedOnTurn[slot] == turnNumber) return false;
            burnedOnTurn[slot] = turnNumber;
            return true;
        }

        internal AreaEffect Clone()
        {
            var copy = (AreaEffect)MemberwiseClone();
            copy.tilesX = (int[])tilesX.Clone();
            copy.tilesY = (int[])tilesY.Clone();
            copy.burnedOnTurn = (int[])burnedOnTurn.Clone();
            return copy;
        }
    }

    // A single-use tile that hurts and halts the first enemy to step onto it.
    public sealed class TrapEffect
    {
        public PlayerId Owner { get; }
        public int OwnerIndex { get; }
        public int X { get; }
        public int Y { get; }
        public int Damage { get; }

        internal TrapEffect(PlayerId owner, int ownerIndex, int x, int y, int damage)
        {
            if (x < 0 || y < 0) throw new ArgumentOutOfRangeException(nameof(x));
            Owner = owner; OwnerIndex = ownerIndex; X = x; Y = y; Damage = damage;
        }

        internal TrapEffect Clone() => (TrapEffect)MemberwiseClone();
    }
}
