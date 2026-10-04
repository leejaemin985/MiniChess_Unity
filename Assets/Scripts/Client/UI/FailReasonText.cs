using MiniChess.Core.Actions;

namespace MiniChess.Client.UI
{
    /// <summary>코어 실패 이유를 화면에 띄울 문구로 바꾼다.</summary>
    public static class FailReasonText
    {
        public static string Describe(MoveFailReason reason)
        {
            switch (reason)
            {
                case MoveFailReason.NotBattlePhase: return "Not in battle";
                case MoveFailReason.NotYourTurn: return "Not your turn";
                case MoveFailReason.UnitNotOnBoard: return "Unit is not on the board";
                case MoveFailReason.AlreadyActed: return "This unit already acted";
                case MoveFailReason.NotStraightLine: return "Move in a straight line only";
                case MoveFailReason.PathBlocked: return "Path is blocked";
                case MoveFailReason.NotEnoughAp: return "Not enough AP";
                case MoveFailReason.ActionsEnded: return "This unit can't act this turn";
                case MoveFailReason.Rooted: return "This unit is rooted";
                case MoveFailReason.DistanceLimited: return "Movement limit reached";
                default: return reason.ToString();
            }
        }

        public static string Describe(AttackFailReason reason)
        {
            switch (reason)
            {
                case AttackFailReason.NotBattlePhase: return "Not in battle";
                case AttackFailReason.NotYourTurn: return "Not your turn";
                case AttackFailReason.AttackerNotOnBoard: return "Attacker is not on the board";
                case AttackFailReason.AlreadyActed: return "This unit already acted";
                case AttackFailReason.InvalidTarget: return "Invalid target";
                case AttackFailReason.TargetNotEnemy: return "Target is not an enemy";
                case AttackFailReason.OutOfRange: return "Out of range";
                case AttackFailReason.NotEnoughAp: return "Not enough AP";
                case AttackFailReason.ActionsEnded: return "This unit can't act this turn";
                default: return reason.ToString();
            }
        }

        public static string Describe(EndTurnFailReason reason)
        {
            switch (reason)
            {
                case EndTurnFailReason.NotBattlePhase: return "Not in battle";
                case EndTurnFailReason.NotYourTurn: return "Not your turn";
                default: return reason.ToString();
            }
        }
    }
}
