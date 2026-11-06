using MiniChess.Bot.Engine;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot.Arena
{
    /// <summary>
    /// 판단 들여다보기: 두 봇의 경기를 진행하면서 엔진 봇의 턴마다 루트 계획(행동 순서, 계획 점수, 탐색 값)과 고른 계획을 찍는다.
    /// 봇이 왜 그 수를 골랐는지(또는 왜 이상한 수를 골랐는지) 보는 용도.
    /// </summary>
    public static class Inspect
    {
        public static void Run(BotSpec engineSpec, BotSpec opponentSpec, int seed, int fromTurn, int toTurn)
        {
            GameController game = ArenaMatch.CreateGame(seed);
            BotPlayer engine = engineSpec.Create(seed * 2 + 1);
            BotPlayer opponent = opponentSpec.Create(seed * 2 + 2);
            if (!(engine is EngineBot engineBot))
                throw new ArgumentException("inspect 의 첫 봇은 engine 이어야 한다");

            BotMatch.Place(game, Team.Player1, engine);
            BotMatch.Place(game, Team.Player2, opponent);

            while (!game.State.IsGameOver && game.State.TurnNumber <= toTurn)
            {
                Team team = game.State.CurrentTeam;
                BotPlayer bot = team == Team.Player1 ? engine : opponent;
                bool watch = bot == engine && game.State.TurnNumber >= fromTurn;
                bool first = true;

                while (!game.State.IsGameOver && game.State.CurrentTeam == team)
                {
                    BotAction action = bot.DecideNext(game, team) ?? BotAction.EndTurn(team);
                    if (watch && first)
                    {
                        Console.WriteLine($"== 턴 {game.State.TurnNumber} ({team}, AP {game.State.GetPlayer(team).Ap.Current}) " +
                                          $"판 {engineBot.LastNodesSearched}, 계획 생성 {engineBot.LastPlannerCalls}회, {engineBot.LastSearchMilliseconds:F0} ms");
                        foreach ((TurnPlan plan, double value) in engineBot.LastRootValues)
                            Console.WriteLine($"  값 {value,9:F2}  계획점수 {plan.Score,8:F2}  [{string.Join(" → ", plan.Actions)}]");
                        first = false;
                    }

                    if (bot != engine)
                        first = true;
                    if (!action.Execute(game))
                        throw new InvalidOperationException($"행동 거부: {action}");
                    if (watch)
                        Console.WriteLine($"    실행: {action}");
                    if (action.Kind == BotActionKind.EndTurn)
                        break;
                }
            }

            Console.WriteLine($"종료: 턴 {game.State.TurnNumber}, 승자 {game.State.Winner?.ToString() ?? "-"}");
        }
    }
}
