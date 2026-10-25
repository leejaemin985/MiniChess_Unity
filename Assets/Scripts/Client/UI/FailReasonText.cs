using System;
using MiniChess.Core.Actions;
using MiniChess.Protocol;

namespace MiniChess.Client.UI
{
    /// <summary>코어 실패 이유를 화면에 띄울 문구로 바꾼다.</summary>
    public static class FailReasonText
    {
        /// <summary>
        /// 명령 결과의 실패 사유(코어 실패 enum 이름 또는 서버 사유)를 문구로 바꾼다. 어떤 enum 인지는 명령 종류로 정한다.
        /// </summary>
        public static string Describe(CommandMessage command, string reason)
        {
            switch (reason)
            {
                case CommandOutcome.StaleTurn: return "이미 지난 턴의 요청입니다";
                case CommandOutcome.InvalidRequest: return "잘못된 요청입니다";
                case CommandOutcome.GameOver: return "경기가 끝났습니다";
                case CommandOutcome.ServerError: return "서버 오류로 경기가 무효 처리되었습니다";
            }

            switch (command)
            {
                case PlaceUnitsMessage _ when Enum.TryParse(reason, out PlaceFailReason place): return Describe(place);
                case MoveMessage _ when Enum.TryParse(reason, out MoveFailReason move): return Describe(move);
                case AttackMessage _ when Enum.TryParse(reason, out AttackFailReason attack): return Describe(attack);
                case AttackObstacleMessage _ when Enum.TryParse(reason, out AttackFailReason attack): return Describe(attack);
                case UseSkillMessage _ when Enum.TryParse(reason, out SkillFailReason skill): return Describe(skill);
                case EndTurnMessage _ when Enum.TryParse(reason, out EndTurnFailReason endTurn): return Describe(endTurn);
                default: return reason;
            }
        }

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
                case MoveFailReason.Stunned: return "Stunned units can't move";
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
                case AttackFailReason.AlreadyUsedThisTurn: return "Already attacked this turn";
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
                case SkillFailReason.AlreadyUsedThisTurn: return "Skill already used this turn";
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
