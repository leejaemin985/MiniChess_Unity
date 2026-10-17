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
                case MoveFailReason.Installed: return "Installed units can't move";
                case MoveFailReason.Sealed: return "Sealed units can't move";
                case MoveFailReason.UnknownUnit: return "Unknown unit";
                case MoveFailReason.NotYourUnit: return "Not your unit";
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
                case AttackFailReason.BlockedByStatus: return "This unit can't attack now";
                case AttackFailReason.UnknownUnit: return "Unknown unit";
                case AttackFailReason.NotYourUnit: return "Not your unit";
                default: return reason.ToString();
            }
        }

        public static string Describe(SkillFailReason reason)
        {
            switch (reason)
            {
                case SkillFailReason.NotBattlePhase: return "Not in battle";
                case SkillFailReason.NotYourTurn: return "Not your turn";
                case SkillFailReason.CasterNotOnBoard: return "Caster is not on the board";
                case SkillFailReason.SkillNotOwned: return "Skill not available";
                case SkillFailReason.ConfigMissing: return "Skill values not set (TBD)";
                case SkillFailReason.ActionsEnded: return "This unit can't act this turn";
                case SkillFailReason.AlreadyActed: return "This unit already acted";
                case SkillFailReason.MoveLocked: return "Can't move after acting";
                case SkillFailReason.ConditionNotMet: return "Skill condition not met";
                case SkillFailReason.InvalidTarget: return "No valid target";
                case SkillFailReason.NotEnoughAp: return "Not enough AP";
                case SkillFailReason.BlockedByStatus: return "This unit can't use skills now";
                case SkillFailReason.UnknownUnit: return "Unknown unit";
                case SkillFailReason.NotYourUnit: return "Not your unit";
                default: return reason.ToString();
            }
        }

        public static string Describe(PlaceFailReason reason)
        {
            switch (reason)
            {
                case PlaceFailReason.NotSetupPhase: return "배치 단계가 아닙니다";
                case PlaceFailReason.AlreadyPlaced: return "이미 배치를 확정했습니다";
                case PlaceFailReason.WrongUnitCount: return "팀 인원 수를 맞춰야 합니다";
                case PlaceFailReason.UnknownCharacter: return "알 수 없는 캐릭터";
                case PlaceFailReason.CharacterNotConfigured: return "수치가 설정되지 않은 캐릭터";
                case PlaceFailReason.DuplicateCharacter: return "같은 캐릭터는 한 번만 배치할 수 있습니다";
                case PlaceFailReason.NotSpawnCell: return "자기 영역(시작 칸)에만 배치할 수 있습니다";
                case PlaceFailReason.DuplicatePosition: return "한 칸에 하나만 배치할 수 있습니다";
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
