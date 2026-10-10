using System.Collections.Generic;
using System.Linq;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.State;

namespace MiniChess.Client.Interaction
{
    /// <summary>
    /// 표시용으로 "지금 실행 가능한 행동"을 찾는다. 판정은 전부 코어 Action.Validate 에 맡기고,
    /// 여기서는 후보 칸/대상만 모은다.
    /// </summary>
    public static class ActionQueries
    {
        private static readonly (int X, int Y)[] Directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>유닛이 지금 이동할 수 있는 칸(상하좌우 직선 위의 칸 중 Validate 통과).</summary>
        public static List<Position> GetMoveDestinations(GameState state, Unit unit)
        {
            var destinations = new List<Position>();
            if (!unit.IsPlaced)
                return destinations;

            Position from = unit.Position.Value;
            foreach ((int dx, int dy) in Directions)
            {
                var position = new Position(from.X + dx, from.Y + dy);
                while (state.Board.IsInBounds(position))
                {
                    if (new MoveAction(unit, position).Validate(state) == MoveFailReason.None)
                        destinations.Add(position);

                    position = new Position(position.X + dx, position.Y + dy);
                }
            }

            return destinations;
        }

        /// <summary>유닛이 지금 공격할 수 있는 적(사거리 안 + Validate 통과).</summary>
        public static List<Unit> GetAttackableTargets(GameState state, Unit unit)
        {
            return AttackAction.GetTargetsInRange(state, unit)
                .Where(target => new AttackAction(unit, target).Validate(state) == AttackFailReason.None)
                .ToList();
        }

        /// <summary>유닛이 지금 기본 공격으로 칠 수 있는 장애물 칸(사거리 안 + Validate 통과).</summary>
        public static List<Position> GetAttackableObstacles(GameState state, Unit unit)
        {
            return AttackObstacleAction.GetObstaclesInRange(state, unit)
                .Where(position => new AttackObstacleAction(unit, position).Validate(state) == AttackFailReason.None)
                .ToList();
        }
    }
}
