using System;
using System.Collections.Generic;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot
{
    /// <summary>
    /// 가능한 행동 중 하나를 무작위로 고르는 봇. 행동 나열 검증과 다른 봇의 비교 기준(하한)으로 쓴다.
    /// 매 행동마다 EndTurnChance 확률로 턴을 끝낸다.
    /// </summary>
    public class RandomBot : BotPlayer
    {
        private readonly Random _random;

        public double EndTurnChance { get; set; } = 0.15;

        public RandomBot(int seed)
        {
            _random = new Random(seed);
        }

        public override IReadOnlyList<UnitPlacement> DecidePlacement(GameController game, Team team)
        {
            return BotPlacement.Random(game, team, _random);
        }

        protected override BotAction Choose(GameController game, Team team)
        {
            List<BotAction> actions = BotActionGenerator.Generate(game, team);
            if (actions.Count == 0 || _random.NextDouble() < EndTurnChance)
                return null;

            return actions[_random.Next(actions.Count)];
        }
    }
}
