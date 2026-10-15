using System;
using System.Collections.Generic;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.State;

namespace MiniChess.Bot
{
    /// <summary>
    /// 연출 없이 봇의 결정을 바로 요청하는 헤드리스 실행기(테스트/봇끼리 대전용).
    /// 봇이 고른 행동이 거부되면 봇 버그이므로 예외를 던진다.
    /// </summary>
    public static class BotMatch
    {
        /// <summary>봇의 배치를 요청한다.</summary>
        public static void Place(GameController game, Team team, BotPlayer bot)
        {
            IReadOnlyList<UnitPlacement> placements = bot.DecidePlacement(game, team);
            var response = game.PlaceUnits(new PlaceUnitsRequest(team, placements));
            if (!response.Success)
                throw new InvalidOperationException($"{team} 봇 배치 거부: {response.Reason}");
        }

        /// <summary>현재 팀의 턴을 봇이 끝낼 때까지 진행한다. 실행한 행동(턴 종료 포함)을 돌려준다.</summary>
        public static List<BotAction> PlayTurn(GameController game, BotPlayer bot)
        {
            Team team = game.State.CurrentTeam;
            var played = new List<BotAction>();

            while (!game.State.IsGameOver && game.State.CurrentTeam == team)
            {
                BotAction action = bot.DecideNext(game, team);
                if (action == null)
                    break;

                if (!action.Execute(game))
                    throw new InvalidOperationException($"봇 행동 거부: {action}");

                played.Add(action);
                if (action.Kind == BotActionKind.EndTurn)
                    break;
            }

            return played;
        }

        /// <summary>배치부터 경기 종료까지 두 봇으로 진행한다. maxTurns 를 넘기면 중단한다(규칙에 턴 제한이 없을 때 대비).</summary>
        public static void Run(GameController game, BotPlayer player1, BotPlayer player2, int maxTurns = 1000)
        {
            if (game.State.Phase == GamePhase.Setup)
            {
                if (!game.State.GetPlayer(Team.Player1).HasPlaced) Place(game, Team.Player1, player1);
                if (!game.State.GetPlayer(Team.Player2).HasPlaced) Place(game, Team.Player2, player2);
            }

            while (!game.State.IsGameOver && game.State.TurnNumber <= maxTurns)
                PlayTurn(game, game.State.CurrentTeam == Team.Player1 ? player1 : player2);
        }
    }
}
