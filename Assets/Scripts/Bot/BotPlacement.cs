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

        /// <summary>
        /// 역할을 섞은 구성(전투 2, 수호 1, 제어 1 을 우선, 모자라면 아무 역할)을 고르고,
        /// 근접(사거리 1) 유닛은 체력 높은 순으로 상대 쪽 앞줄 가운데에, 원거리 유닛은 뒷줄 가운데에 둔다.
        /// lineup 을 주면 그 구성을 쓴다.
        /// </summary>
        public static List<UnitPlacement> ByRole(GameController game, Team team, Random random, IReadOnlyList<string> lineup = null)
        {
            int count = game.State.Rules.Match.UnitsPerTeam;
            List<CharacterDefinition> playable = GetPlayableCharacters(game);
            List<CharacterDefinition> chosen = lineup != null && lineup.Count > 0
                ? lineup.Select(id => playable.FirstOrDefault(character => character.Id == id)).Where(character => character != null).ToList()
                : PickByRole(playable, count, random);

            List<Position> cells = game.GetPlacementCells(team).ToList();
            if (chosen.Count < count || cells.Count < count)
                throw new InvalidOperationException($"{team} 배치 불가: 캐릭터 {chosen.Count}, 시작 칸 {cells.Count}, 필요 {count}");

            List<Position> enemyCells = game.GetPlacementCells(team == Team.Player1 ? Team.Player2 : Team.Player1).ToList();
            double centerX = (game.State.Board.Width - 1) / 2.0;
            double enemyY = enemyCells.Count > 0 ? enemyCells.Average(cell => cell.Y) : game.State.Board.Height / 2.0;

            // 앞줄 = 상대 시작 위치에 가까운 줄. 같은 줄이면 가운데부터.
            List<Position> front = cells.OrderBy(cell => Math.Abs(cell.Y - enemyY)).ThenBy(cell => Math.Abs(cell.X - centerX)).ToList();
            List<Position> back = cells.OrderByDescending(cell => Math.Abs(cell.Y - enemyY)).ThenBy(cell => Math.Abs(cell.X - centerX)).ToList();

            var used = new HashSet<Position>();
            var placements = new List<UnitPlacement>();
            foreach (CharacterDefinition character in chosen.OrderBy(c => c.AttackRange).ThenByDescending(c => c.MaxHp))
            {
                List<Position> order = character.AttackRange <= 1 ? front : back;
                Position cell = order.First(position => !used.Contains(position));
                used.Add(cell);
                placements.Add(new UnitPlacement(character.Id, cell));
            }

            return placements;
        }

        private static List<CharacterDefinition> PickByRole(List<CharacterDefinition> playable, int count, Random random)
        {
            var wanted = new List<CharacterRole> { CharacterRole.Combat, CharacterRole.Combat, CharacterRole.Guardian, CharacterRole.Control };
            List<CharacterDefinition> pool = playable.OrderBy(_ => random.Next()).ToList();
            var chosen = new List<CharacterDefinition>();

            foreach (CharacterRole role in wanted.Take(count))
            {
                CharacterDefinition pick = pool.FirstOrDefault(character => character.Role == role);
                if (pick == null)
                    continue;
                chosen.Add(pick);
                pool.Remove(pick);
            }

            chosen.AddRange(pool.Take(count - chosen.Count));
            return chosen;
        }
    }
}
