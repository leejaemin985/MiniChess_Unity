using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Skills.Areas;

namespace MiniChess.Bot
{
    public enum BotActionKind
    {
        Move,
        Attack,
        AttackObstacle,
        UseSkill,
        EndTurn,
    }

    /// <summary>
    /// 봇이 고른 행동 하나. 컨트롤러 요청 하나에 대응한다.
    /// 클라는 이 값을 보고 자기 경로(뷰 동기화 등)로 요청하고, 헤드리스 실행은 Execute 를 쓴다.
    /// </summary>
    public class BotAction
    {
        public BotActionKind Kind { get; }
        public Team Team { get; }

        /// <summary>행동하는 유닛. EndTurn 이면 0.</summary>
        public int UnitId { get; }

        /// <summary>Move 의 도착 칸, AttackObstacle 의 장애물 칸.</summary>
        public Position Cell { get; }

        /// <summary>Attack 의 대상 유닛.</summary>
        public int TargetUnitId { get; }

        public string SkillId { get; }

        /// <summary>UseSkill 의 지정 칸(지정 순).</summary>
        public IReadOnlyList<Position> Targets { get; }

        public AreaRotation Rotation { get; }

        private BotAction(BotActionKind kind, Team team, int unitId = 0, Position cell = default, int targetUnitId = 0,
            string skillId = null, IReadOnlyList<Position> targets = null, AreaRotation rotation = AreaRotation.R0)
        {
            Kind = kind;
            Team = team;
            UnitId = unitId;
            Cell = cell;
            TargetUnitId = targetUnitId;
            SkillId = skillId;
            Targets = targets ?? Array.Empty<Position>();
            Rotation = rotation;
        }

        public static BotAction Move(Team team, int unitId, Position destination) =>
            new BotAction(BotActionKind.Move, team, unitId, cell: destination);

        public static BotAction Attack(Team team, int unitId, int targetUnitId) =>
            new BotAction(BotActionKind.Attack, team, unitId, targetUnitId: targetUnitId);

        public static BotAction AttackObstacle(Team team, int unitId, Position obstacle) =>
            new BotAction(BotActionKind.AttackObstacle, team, unitId, cell: obstacle);

        public static BotAction UseSkill(Team team, int unitId, string skillId, IReadOnlyList<Position> targets, AreaRotation rotation = AreaRotation.R0) =>
            new BotAction(BotActionKind.UseSkill, team, unitId, skillId: skillId, targets: targets, rotation: rotation);

        public static BotAction EndTurn(Team team) => new BotAction(BotActionKind.EndTurn, team);

        /// <summary>같은 요청인지(종류, 팀, 유닛, 칸, 대상, 스킬, 지정 칸 순서, 회전이 모두 같다). 복사한 경기에서 고른 행동을 실제 경기 후보와 맞춰 볼 때 쓴다.</summary>
        public bool SameAs(BotAction other)
        {
            return other != null
                && Kind == other.Kind
                && Team == other.Team
                && UnitId == other.UnitId
                && Cell == other.Cell
                && TargetUnitId == other.TargetUnitId
                && SkillId == other.SkillId
                && Rotation == other.Rotation
                && Targets.SequenceEqual(other.Targets);
        }

        /// <summary>컨트롤러에 요청한다. 성공 여부만 돌려준다(실패하면 상태는 바뀌지 않는다).</summary>
        public bool Execute(GameController game)
        {
            switch (Kind)
            {
                case BotActionKind.Move:
                    return game.Move(new MoveRequest(Team, UnitId, Cell)).Success;
                case BotActionKind.Attack:
                    return game.Attack(new AttackRequest(Team, UnitId, TargetUnitId)).Success;
                case BotActionKind.AttackObstacle:
                    return game.AttackObstacle(new AttackObstacleRequest(Team, UnitId, Cell)).Success;
                case BotActionKind.UseSkill:
                    return game.UseSkill(new UseSkillRequest(Team, UnitId, SkillId, Targets, Rotation)).Success;
                case BotActionKind.EndTurn:
                    return game.EndTurn(new EndTurnRequest(Team)).Success;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null);
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case BotActionKind.Move:
                    return $"{Team} #{UnitId} 이동 → {Cell}";
                case BotActionKind.Attack:
                    return $"{Team} #{UnitId} 공격 → #{TargetUnitId}";
                case BotActionKind.AttackObstacle:
                    return $"{Team} #{UnitId} 장애물 공격 → {Cell}";
                case BotActionKind.UseSkill:
                    return $"{Team} #{UnitId} {SkillId} → {string.Join(", ", Targets.Select(t => t.ToString()))} ({Rotation})";
                default:
                    return $"{Team} 턴 종료";
            }
        }
    }
}
