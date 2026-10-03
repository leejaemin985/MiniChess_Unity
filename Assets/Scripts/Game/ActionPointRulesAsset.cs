using UnityEngine;

namespace MiniChess
{
    [CreateAssetMenu(fileName = "ActionPointRules", menuName = "Mini Chess/AP Rules")]
    public sealed class ActionPointRulesAsset : ScriptableObject
    {
        [SerializeField, Min(1)] private int maxAP = 6;
        [SerializeField, Min(0)] private int startAP = 4;
        [SerializeField, Min(0)] private int recoveryAP = 4;
        [SerializeField, Min(1)] private int moveAPPerTile = 1;
        [SerializeField, Min(1), Tooltip("Tiles one basic move may cover. Under EightWay a diagonal "
            + "step counts as one of them.")]
        private int maxMoveDistance = 2;
        [SerializeField, Min(0), Tooltip("Tiles a unit may cover across all its moves in one turn. "
            + "0 means only AP limits it, so chaining short moves equals one long move.")]
        private int maxMoveTilesPerTurn = 0;
        [SerializeField, Tooltip("What shape a basic move may take. FourWay allows one straight run "
            + "along a row or column, so no move ever ends diagonally. EightWay adds diagonals and "
            + "lets a move turn corners. Attack and skill ranges stay diagonal-inclusive either way.")]
        private MovePattern movePattern = MovePattern.FourWay;
        [SerializeField, Min(1)] private int attackAP = 2;
        [SerializeField, Min(1)] private int defaultSkillAP = 3;
        [SerializeField, Tooltip("Skills require both AP and the existing match-wide skill resource.")]
        private bool useSkillResource = true;
        public ActionPointRules CreateRules() => new ActionPointRules(Mathf.Max(1, maxAP),
            Mathf.Clamp(startAP, 0, Mathf.Max(1, maxAP)), Mathf.Max(0, recoveryAP),
            Mathf.Max(1, moveAPPerTile), Mathf.Max(1, maxMoveDistance),
            Mathf.Max(0, maxMoveTilesPerTurn), Mathf.Max(1, attackAP),
            Mathf.Max(1, defaultSkillAP), useSkillResource, movePattern);
    }
}
