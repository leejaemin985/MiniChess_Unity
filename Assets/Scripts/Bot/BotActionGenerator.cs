using System.Collections.Generic;
using System.Linq;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Skills.Areas;
using MiniChess.Core.State;

namespace MiniChess.Bot
{
    /// <summary>
    /// 지금 할 수 있는 행동 후보를 컨트롤러 조회만으로 나열한다(턴 종료 제외).
    /// 조회가 통과시킨 후보만 담으므로 실행하면 성공한다.
    /// </summary>
    public static class BotActionGenerator
    {
        private static readonly AreaRotation[] AllRotations =
        {
            AreaRotation.R0, AreaRotation.R90, AreaRotation.R180, AreaRotation.R270,
        };

        public static List<BotAction> Generate(GameController game, Team team)
        {
            var actions = new List<BotAction>();
            GameState state = game.State;
            if (state.Phase != GamePhase.Battle || state.CurrentTeam != team)
                return actions;

            foreach (Unit unit in state.GetPlayer(team).AliveUnits.Where(unit => unit.IsOnBoard).ToList())
            {
                foreach (Position destination in game.GetMoveDestinations(unit.Id))
                    actions.Add(BotAction.Move(team, unit.Id, destination));

                foreach (Unit target in game.GetAttackTargets(unit.Id))
                    actions.Add(BotAction.Attack(team, unit.Id, target.Id));

                foreach (Position obstacle in game.GetAttackableObstacles(unit.Id))
                    actions.Add(BotAction.AttackObstacle(team, unit.Id, obstacle));

                foreach (SkillInfo skill in game.GetSkills(unit.Id))
                {
                    if (skill.Usable != SkillFailReason.None || skill.Definition == null)
                        continue;

                    IReadOnlyList<AreaRotation> rotations = skill.CanRotate ? AllRotations : new[] { AreaRotation.R0 };
                    foreach (List<Position> targets in EnumerateTargets(game, unit.Id, skill.Id, skill.Definition.Targeting.TargetCount))
                    {
                        foreach (AreaRotation rotation in rotations)
                            actions.Add(BotAction.UseSkill(team, unit.Id, skill.Id, targets, rotation));
                    }
                }
            }

            return actions;
        }

        /// <summary>지정 칸 수만큼 GetSkillTargets 를 따라가며 가능한 지정 순서를 모두 만든다.</summary>
        private static IEnumerable<List<Position>> EnumerateTargets(GameController game, int unitId, string skillId, int count)
        {
            return Extend(game, unitId, skillId, count, new List<Position>());
        }

        private static IEnumerable<List<Position>> Extend(GameController game, int unitId, string skillId, int count, List<Position> chosen)
        {
            if (chosen.Count == count)
            {
                yield return chosen;
                yield break;
            }

            foreach (Position next in game.GetSkillTargets(unitId, skillId, chosen))
            {
                var extended = new List<Position>(chosen) { next };
                foreach (List<Position> targets in Extend(game, unitId, skillId, count, extended))
                    yield return targets;
            }
        }
    }
}
