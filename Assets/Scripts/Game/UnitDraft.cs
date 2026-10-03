using System;
using System.Collections.Generic;

namespace MiniChess
{
    // Builds a roster by drawing one unit for each position, and scatters a side
    // across its home rows. Pure C#, so the bot and the headless checks use it too.
    public static class UnitDraft
    {
        public static readonly UnitPosition[] Positions =
        {
            UnitPosition.Melee, UnitPosition.Guard, UnitPosition.Ranged, UnitPosition.Control
        };

        // One random candidate per position. Positions with no candidate in the pool
        // fall back to the built-in unit for that slot, so a roster is always complete.
        public static UnitDefinition[] Draft(IReadOnlyList<UnitDefinition> pool, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var roster = new UnitDefinition[GameState.UnitsPerPlayer];
            var candidates = new List<UnitDefinition>();
            for (int slot = 0; slot < Positions.Length; slot++)
            {
                candidates.Clear();
                if (pool != null)
                    foreach (UnitDefinition definition in pool)
                        if (definition != null && definition.Position == Positions[slot])
                            candidates.Add(definition);
                roster[slot] = candidates.Count > 0
                    ? candidates[random.Next(candidates.Count)]
                    : GetFallback(Positions[slot]);
            }
            return roster;
        }

        private static UnitDefinition GetFallback(UnitPosition position)
        {
            switch (position)
            {
                case UnitPosition.Guard: return UnitDefinitions.Guardian;
                case UnitPosition.Ranged: return UnitDefinitions.Ranger;
                case UnitPosition.Control: return UnitDefinitions.Controller;
                default: return UnitDefinitions.Warrior;
            }
        }

        // Distinct random tiles inside the player's two home rows.
        public static void PlaceRandomly(GameState state, PlayerId player,
            UnitDefinition[] roster, Random random)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (roster == null || roster.Length != GameState.UnitsPerPlayer)
                throw new ArgumentException("Exactly four unit definitions are required.", nameof(roster));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var tiles = new List<int>();
            int homeStart = player == PlayerId.PlayerOne ? 0 : state.BoardSize - 2;
            for (int y = homeStart; y < homeStart + 2; y++)
                for (int x = 0; x < state.BoardSize; x++)
                    if (!state.IsBlocked(x, y)) tiles.Add(y * state.BoardSize + x);
            if (tiles.Count < GameState.UnitsPerPlayer)
                throw new InvalidOperationException("Not enough free home tiles to deploy.");
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                int pick = random.Next(tiles.Count);
                int tile = tiles[pick];
                tiles.RemoveAt(pick);
                if (!state.TryAddUnit(new UnitState(player, index, roster[index],
                    tile % state.BoardSize, tile / state.BoardSize)))
                    throw new InvalidOperationException("Random deployment produced an invalid unit.");
            }
        }

        // A full random match: both sides drafted and placed.
        public static GameState CreateRandomMatch(int boardSize, BattleRules rules,
            IReadOnlyList<UnitDefinition> pool, Random random, bool includePlayerOne = true)
        {
            var state = new GameState(boardSize, rules);
            if (includePlayerOne)
                PlaceRandomly(state, PlayerId.PlayerOne, Draft(pool, random), random);
            PlaceRandomly(state, PlayerId.PlayerTwo, Draft(pool, random), random);
            return state;
        }
    }
}
