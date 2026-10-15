using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;

namespace MiniChess.Bot
{
    /// <summary>배치 도우미. 배치 규칙(팀당 인원, 팀 안 중복 금지, 자기 시작 칸)에 맞는 후보를 만든다.</summary>
    public static class BotPlacement
    {
        /// <summary>필수 수치가 모두 채워져 경기에 쓸 수 있는 캐릭터.</summary>
        public static List<CharacterDefinition> GetPlayableCharacters(GameController game)
        {
            return game.GetCharacters().Where(character => character.GetMissingFields().Count == 0).ToList();
        }

        /// <summary>무작위 구성을 무작위 시작 칸에 둔다.</summary>
        public static List<UnitPlacement> Random(GameController game, Team team, Random random)
        {
            int count = game.State.Rules.Match.UnitsPerTeam;
            List<string> lineup = GetPlayableCharacters(game)
                .OrderBy(_ => random.Next())
                .Take(count)
                .Select(character => character.Id)
                .ToList();
            List<Position> cells = game.GetPlacementCells(team)
                .OrderBy(_ => random.Next())
                .ToList();

            if (lineup.Count < count || cells.Count < count)
                throw new InvalidOperationException($"{team} 배치 불가: 캐릭터 {lineup.Count}, 시작 칸 {cells.Count}, 필요 {count}");

            return lineup.Select((id, i) => new UnitPlacement(id, cells[i])).ToList();
        }
    }
}
