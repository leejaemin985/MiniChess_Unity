using System;

namespace MiniChess
{
    // Player One places the four roster slots; Player Two keeps its fixed formation.
    public sealed class DeploymentSystem
    {
        private readonly GameState state;
        private readonly UnitDefinition[] definitions;
        public bool Started { get; private set; }
        public DeploymentSystem(GameState state, UnitDefinition[] definitions = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.definitions = definitions == null ? new[] { UnitDefinitions.Warrior,
                UnitDefinitions.Guardian, UnitDefinitions.Ranger, UnitDefinitions.Controller }
                : (UnitDefinition[])definitions.Clone();
            if (this.definitions.Length != GameState.UnitsPerPlayer || Array.Exists(this.definitions, item => item == null))
                throw new ArgumentException("Exactly four unit definitions are required.", nameof(definitions));
        }

        public bool TryStart()
        {
            if (Started || state.Phase != GamePhase.Deployment) return false;
            Started = true;
            return true;
        }

        public UnitDefinition GetDefinition(int index)
        {
            return index >= 0 && index < definitions.Length ? definitions[index] : null;
        }

        public bool CanPlace(int index, int x, int y)
        {
            if (!Started || state.Phase != GamePhase.Deployment || GetDefinition(index) == null
                || x < 0 || x >= state.BoardSize || y < 0 || y > 1) return false;
            if (state.HasObstacleAt(x, y)) return false;
            UnitState occupant = state.GetUnitAt(x, y);
            return occupant == null || occupant == state.GetUnit(PlayerId.PlayerOne, index);
        }

        public bool TryPlace(int index, int x, int y)
        {
            if (!CanPlace(index, x, y)) return false;
            UnitState unit = state.GetUnit(PlayerId.PlayerOne, index);
            if (unit == null)
                return state.TryAddUnit(new UnitState(PlayerId.PlayerOne, index,
                    GetDefinition(index), x, y));
            unit.SetPosition(x, y);
            return true;
        }
    }
}
