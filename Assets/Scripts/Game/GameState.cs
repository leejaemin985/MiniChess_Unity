using System.Collections.Generic;

namespace MiniChess
{
    // Pure data: no scene objects, so AI can simulate an independent copy.
    public sealed class GameState
    {
        public const int UnitsPerPlayer = 4;
        public const int MaxTrapsPerPlayer = 2;

        private UnitState[,] units = new UnitState[2, UnitsPerPlayer];
        private PlayerTurnState[] playerTurns;
        private List<AreaEffect> areas = new List<AreaEffect>();
        private List<TrapEffect> traps = new List<TrapEffect>();
        // Packed tiles nothing may stand on or move through. Fixed once the battle
        // starts, which is why Clone shares the list instead of copying it.
        private List<int> obstacles = new List<int>();
        public int BoardSize { get; }
        public BattleRules Rules { get; }
        public int PlayerOneCost { get; internal set; }
        public int PlayerTwoCost { get; internal set; }

        public GameState(int boardSize = 7, BattleRules rules = null)
        {
            if (boardSize <= 0) throw new System.ArgumentOutOfRangeException(nameof(boardSize));
            BoardSize = boardSize;
            Rules = rules ?? new BattleRules();
            PlayerOneCost = PlayerTwoCost = Rules.InitialCost;
            playerTurns = new[] { new PlayerTurnState(Rules.AP), new PlayerTurnState(Rules.AP) };
        }

        public int GetCost(PlayerId player) => player == PlayerId.PlayerOne ? PlayerOneCost
            : player == PlayerId.PlayerTwo ? PlayerTwoCost : 0;

        public int GetTurnCount(PlayerId player) => player == PlayerId.PlayerOne ? PlayerOneTurnCount
            : player == PlayerId.PlayerTwo ? PlayerTwoTurnCount : 0;

        public GamePhase Phase { get; internal set; } = GamePhase.Deployment;
        public PlayerId? CurrentPlayer { get; internal set; }
        public PlayerId? Winner { get; internal set; }
        public int TurnNumber { get; internal set; }
        public int PlayerOneTurnCount { get; internal set; }
        public int PlayerTwoTurnCount { get; internal set; }
        public PlayerTurnState GetPlayerTurn(PlayerId player)
        {
            if (player != PlayerId.PlayerOne && player != PlayerId.PlayerTwo)
                throw new System.ArgumentOutOfRangeException(nameof(player));
            return playerTurns[(int)player];
        }
        public int GetAP(PlayerId player) => GetPlayerTurn(player).CurrentAP;

        public IReadOnlyList<AreaEffect> Areas => areas;
        public IReadOnlyList<TrapEffect> Traps => traps;
        public IReadOnlyList<int> Obstacles => obstacles;

        // The single capture tile, packed, or -1 when the player designated none. Like
        // obstacles it is a pre-match layout choice and never moves afterwards.
        public int CaptureTile { get; private set; } = -1;
        public bool HasCapturePoint => CaptureTile >= 0;
        public int CaptureX => CaptureTile < 0 ? -1 : CaptureTile % BoardSize;
        public int CaptureY => CaptureTile < 0 ? -1 : CaptureTile / BoardSize;
        // The side standing on it and how many of its own turn starts it has held it.
        public PlayerId? CaptureHolder { get; internal set; }
        public int CaptureHeldRounds { get; internal set; }
        // Set once a hold completes. Null again if the rules hand the point back.
        public PlayerId? CaptureOwner { get; internal set; }

        public bool IsCaptureTile(int x, int y) => HasCapturePoint && CaptureX == x && CaptureY == y;

        // Designating replaces whatever was there, so the player always has one at most.
        public bool TrySetCapturePoint(int x, int y)
        {
            if (Phase != GamePhase.Deployment || x < 0 || y < 0 || x >= BoardSize || y >= BoardSize
                || HasObstacleAt(x, y)) return false;
            CaptureTile = y * BoardSize + x;
            return true;
        }

        public bool ClearCapturePoint()
        {
            if (Phase != GamePhase.Deployment || !HasCapturePoint) return false;
            CaptureTile = -1;
            CaptureHolder = null;
            CaptureHeldRounds = 0;
            CaptureOwner = null;
            return true;
        }

        public bool HasObstacleAt(int x, int y) => obstacles.Contains(y * BoardSize + x);

        // The one question movement should ask: may something occupy or cross this tile?
        // A living unit and an obstacle both say no; an obstacle also says no to skills.
        public bool IsBlocked(int x, int y) => HasObstacleAt(x, y) || GetUnitAt(x, y) != null;

        // Obstacles are a pre-match layout choice, so they may only be edited during
        // deployment. That is what lets a cloned state share the list safely.
        public bool TryAddObstacle(int x, int y)
        {
            if (Phase != GamePhase.Deployment || x < 0 || y < 0 || x >= BoardSize || y >= BoardSize
                || IsBlocked(x, y) || IsCaptureTile(x, y)) return false;
            obstacles.Add(y * BoardSize + x);
            return true;
        }

        public bool RemoveObstacle(int x, int y)
        {
            if (Phase != GamePhase.Deployment) return false;
            return obstacles.Remove(y * BoardSize + x);
        }

        public void ClearObstacles()
        {
            if (Phase == GamePhase.Deployment) obstacles.Clear();
        }

        internal void AddArea(AreaEffect area) => areas.Add(area);

        // Oldest trap makes way once a side is already at its cap.
        internal void AddTrap(TrapEffect trap)
        {
            int owned = 0;
            foreach (TrapEffect existing in traps) if (existing.Owner == trap.Owner) owned++;
            while (owned >= MaxTrapsPerPlayer)
            {
                for (int i = 0; i < traps.Count; i++)
                    if (traps[i].Owner == trap.Owner) { traps.RemoveAt(i); break; }
                owned--;
            }
            traps.Add(trap);
        }

        internal void RemoveTrap(TrapEffect trap) => traps.Remove(trap);

        // A trap is hidden information: only the side that laid it knows it is there.
        // The bot searches one of these so it blunders into the opponent's traps the
        // way a player does, instead of steering around what it should not see. Traps
        // it lays inside the search stay visible, so it can still judge laying one.
        public GameState CloneAsSeenBy(PlayerId viewer)
        {
            GameState copy = Clone();
            copy.traps.RemoveAll(trap => trap.Owner != viewer);
            return copy;
        }

        internal void ExpireAreas(PlayerId owner)
        {
            int turn = GetTurnCount(owner);
            areas.RemoveAll(area => area.Owner == owner && turn >= area.ExpiresAtOwnerTurn);
        }

        public TrapEffect GetTrapAt(int x, int y)
        {
            foreach (TrapEffect trap in traps)
                if (trap.X == x && trap.Y == y) return trap;
            return null;
        }

        public bool HasAreaAt(int x, int y)
        {
            foreach (AreaEffect area in areas)
                if (area.Covers(x, y)) return true;
            return false;
        }

        public bool HasAreaAt(int x, int y, PlayerId owner)
        {
            foreach (AreaEffect area in areas)
                if (area.Owner == owner && area.Covers(x, y)) return true;
            return false;
        }

        // Fire laid by the given caster specifically, which is what Ember checks.
        public bool HasAreaFrom(int x, int y, UnitState caster)
        {
            foreach (AreaEffect area in areas)
                if (area.Owner == caster.Owner && area.CasterIndex == caster.UnitIndex
                    && area.Covers(x, y)) return true;
            return false;
        }

        // A side is defeated once every unit it owns is dead.
        public bool IsWipedOut(PlayerId player)
        {
            for (int index = 0; index < UnitsPerPlayer; index++)
            {
                UnitState unit = GetUnit(player, index);
                if (unit != null && unit.IsAlive) return false;
            }
            return true;
        }

        public bool IsDeploymentReady()
        {
            if (Phase != GamePhase.Deployment || BoardSize < 4) return false;
            for (int player = 0; player < 2; player++)
                for (int index = 0; index < UnitsPerPlayer; index++)
                {
                    UnitState unit = units[player, index];
                    if (unit == null || !unit.IsAlive || unit.X < 0 || unit.X >= BoardSize
                        || unit.Y < 0 || unit.Y >= BoardSize) return false;
                    if (player == 0 ? unit.Y > 1 : unit.Y < BoardSize - 2) return false;
                    if (HasObstacleAt(unit.X, unit.Y)) return false;
                    foreach (UnitState other in units)
                        if (other != null && other != unit && other.IsAlive
                            && other.X == unit.X && other.Y == unit.Y) return false;
                }
            return true;
        }

        public GameState Clone()
        {
            var copy = (GameState)MemberwiseClone();
            copy.playerTurns = new[] { playerTurns[0].Clone(), playerTurns[1].Clone() };
            copy.units = new UnitState[2, UnitsPerPlayer];
            for (int player = 0; player < 2; player++)
                for (int index = 0; index < UnitsPerPlayer; index++)
                    copy.units[player, index] = units[player, index]?.Clone();
            copy.areas = new List<AreaEffect>(areas.Count);
            foreach (AreaEffect area in areas) copy.areas.Add(area.Clone());
            copy.traps = new List<TrapEffect>(traps.Count);
            foreach (TrapEffect trap in traps) copy.traps.Add(trap.Clone());
            return copy;
        }

        // Registration checks occupancy; IsDeploymentReady checks the completed formation.
        public bool TryAddUnit(UnitState unit)
        {
            if (Phase != GamePhase.Deployment || unit == null || !unit.IsAlive
                || unit.X >= BoardSize || unit.Y >= BoardSize
                || GetUnit(unit.Owner, unit.UnitIndex) != null
                || IsBlocked(unit.X, unit.Y))
                return false;
            units[(int)unit.Owner, unit.UnitIndex] = unit;
            return true;
        }

        // Dead units keep their stable roster slots, but no longer occupy a tile.
        public UnitState GetUnit(PlayerId player, int unitIndex)
        {
            if ((player != PlayerId.PlayerOne && player != PlayerId.PlayerTwo)
                || unitIndex < 0 || unitIndex >= UnitsPerPlayer)
                return null;
            return units[(int)player, unitIndex];
        }

        public UnitState GetUnitAt(int x, int y)
        {
            foreach (UnitState unit in units)
                if (unit != null && unit.IsAlive && unit.X == x && unit.Y == y)
                    return unit;
            return null;
        }
    }
}
