using System.Collections.Generic;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot.Tests
{
    /// <summary>배치만 하고 아무 행동도 하지 않는 상대(봇이 끝까지 공격하는지 확인용).</summary>
    public class PassiveBot : BotPlayer
    {
        private readonly RandomBot _placer;

        public PassiveBot(int seed)
        {
            _placer = new RandomBot(seed);
        }

        public override IReadOnlyList<UnitPlacement> DecidePlacement(GameController game, Team team)
        {
            return _placer.DecidePlacement(game, team);
        }

        protected override BotAction Choose(GameController game, Team team)
        {
            return null;
        }
    }
}
