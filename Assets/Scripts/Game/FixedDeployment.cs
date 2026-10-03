using System;

namespace MiniChess
{
    public static class FixedDeployment
    {
        public static GameState Create(int boardSize = 7, BattleRules rules = null, bool includePlayerOne = true,
            UnitDefinition[] definitions = null)
        {
            if (boardSize < 4) throw new ArgumentOutOfRangeException(nameof(boardSize));
            var state = new GameState(boardSize, rules);
            definitions = definitions ?? new[] { UnitDefinitions.Warrior, UnitDefinitions.Guardian,
                UnitDefinitions.Ranger, UnitDefinitions.Controller };
            if (definitions.Length != GameState.UnitsPerPlayer || Array.Exists(definitions, item => item == null))
                throw new ArgumentException("Exactly four unit definitions are required.", nameof(definitions));
            for (int player = includePlayerOne ? 0 : 1; player < 2; player++)
                for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                {
                    int x = (boardSize - 2) / 2 + index % 2;
                    int y = index < 2 ? 1 : 0;
                    if (player == 1) { x = boardSize - 1 - x; y = boardSize - 1 - y; }
                    var unit = new UnitState((PlayerId)player, index, definitions[index], x, y);
                    if (!state.TryAddUnit(unit)) throw new InvalidOperationException("Invalid fixed deployment.");
                }
            return state;
        }
    }
}
