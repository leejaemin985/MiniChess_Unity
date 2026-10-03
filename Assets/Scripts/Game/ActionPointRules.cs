using System;

namespace MiniChess
{
    // What shape a single basic move may take. Ranges and targeting stay diagonal-
    // inclusive either way; this only governs how a unit walks.
    public enum MovePattern
    {
        EightWay, // Orthogonal and diagonal steps, and one move may turn corners.
        FourWay   // One straight run along a row or column, so no net diagonal at all.
    }

    public sealed class ActionPointRules
    {
        public int MaxAP { get; }
        public int StartAP { get; }
        public int RecoveryAP { get; }
        public int MoveAPPerTile { get; }
        // Tiles one basic move action may cover. Longer moves need a movement skill.
        public int MaxMoveDistance { get; }
        // Tiles a unit may cover across all of its basic moves in one turn. 0 leaves
        // only AP as the bound, so chaining short moves matches one long move.
        public int MaxMoveTilesPerTurn { get; }
        public int AttackAP { get; }
        public int DefaultSkillAP { get; }
        public bool UseSkillResource { get; }

        public MovePattern MovePattern { get; }

        public ActionPointRules(int maxAP = 6, int startAP = 4, int recoveryAP = 4,
            int moveAPPerTile = 1, int maxMoveDistance = 2, int maxMoveTilesPerTurn = 0,
            int attackAP = 2, int defaultSkillAP = 3, bool useSkillResource = true,
            MovePattern movePattern = MovePattern.EightWay)
        {
            if (maxAP < 1 || startAP < 0 || startAP > maxAP || recoveryAP < 0
                || moveAPPerTile < 1 || maxMoveDistance < 1 || maxMoveTilesPerTurn < 0
                || attackAP < 1 || defaultSkillAP < 1)
                throw new ArgumentOutOfRangeException(nameof(maxAP), "Invalid AP settings.");
            if (!Enum.IsDefined(typeof(MovePattern), movePattern))
                throw new ArgumentOutOfRangeException(nameof(movePattern));
            MaxAP = maxAP; StartAP = startAP; RecoveryAP = recoveryAP;
            MoveAPPerTile = moveAPPerTile; MaxMoveDistance = maxMoveDistance;
            MaxMoveTilesPerTurn = maxMoveTilesPerTurn;
            AttackAP = attackAP; DefaultSkillAP = defaultSkillAP;
            UseSkillResource = useSkillResource;
            MovePattern = movePattern;
        }

        // Tiles one basic move between these squares would cover on an empty board,
        // or -1 when the pattern rules that shape out. FourWay rejects every offset
        // that is not a straight run, so a diagonal tile is unreachable in one move
        // rather than merely expensive.
        public int MoveSteps(int fromX, int fromY, int toX, int toY)
        {
            int dx = Math.Abs(fromX - toX), dy = Math.Abs(fromY - toY);
            if (MovePattern != MovePattern.FourWay) return Math.Max(dx, dy);
            return dx != 0 && dy != 0 ? -1 : dx + dy;
        }

        // True when a single step of this offset is a legal walk.
        public bool IsMoveStep(int dx, int dy)
        {
            if ((dx == 0 && dy == 0) || Math.Abs(dx) > 1 || Math.Abs(dy) > 1) return false;
            return MovePattern != MovePattern.FourWay || dx == 0 || dy == 0;
        }

        public string GetMovePatternName() =>
            MovePattern == MovePattern.FourWay ? "상하좌우 직선" : "상하좌우/대각선";

        // Tiles this unit may still cover with basic moves, bounded by both limits.
        public int RemainingMoveDistance(int tilesMovedThisTurn)
        {
            if (MaxMoveTilesPerTurn <= 0) return MaxMoveDistance;
            return Math.Max(0, Math.Min(MaxMoveDistance, MaxMoveTilesPerTurn - tilesMovedThisTurn));
        }
    }

    public sealed class PlayerTurnState
    {
        public int CurrentAP { get; internal set; }
        public int MaxAP { get; }
        public int RecoveryAP { get; }
        internal PlayerTurnState(ActionPointRules rules)
        { CurrentAP = rules.StartAP; MaxAP = rules.MaxAP; RecoveryAP = rules.RecoveryAP; }
        internal PlayerTurnState Clone() => (PlayerTurnState)MemberwiseClone();
    }
}
