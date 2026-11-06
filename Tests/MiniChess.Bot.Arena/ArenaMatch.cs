using System.Diagnostics;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Events;
using MiniChess.Core.Presets;
using MiniChess.Core.State;

namespace MiniChess.Bot.Arena
{
    /// <summary>한 경기의 결과. A/B 는 대전시킨 두 봇(어느 쪽이 선공이었는지는 AIsPlayer1).</summary>
    public sealed class GameResult
    {
        public int Seed;
        public bool AIsPlayer1;
        /// <summary>A 기준 점수: 승 1, 무 0.5, 패 0.</summary>
        public double ScoreA;
        public int Turns;
        public string EndReason;
        /// <summary>봇이 거부되는 행동을 고르는 등 경기가 비정상 종료됐을 때의 사유.</summary>
        public string Error;
        public readonly SideTiming TimingA = new SideTiming();
        public readonly SideTiming TimingB = new SideTiming();
    }

    /// <summary>한 봇의 판단 시간(턴 단위).</summary>
    public sealed class SideTiming
    {
        public int TurnCount;
        public double TotalMs;
        public double MaxTurnMs;

        public void AddTurn(double ms)
        {
            TurnCount++;
            TotalMs += ms;
            MaxTurnMs = Math.Max(MaxTurnMs, ms);
        }
    }

    /// <summary>
    /// 밸런스 실험용 후공(Player2) AP 보정. 규칙을 바꾸기 전에 실험만 해 보려고 Arena 에서 AP 를 직접 더한다
    /// (봇의 수 읽기는 이 보정을 모르고 규칙대로 앞 턴을 예상한다). 값이 정해지면 Core 규칙으로 옮긴다.
    /// 프리셋 규칙의 선공 보정(ApRuleData.SecondPlayerStartBonus, 값과 근거는 Core ASSUMPTIONS.md "선공 보정") 위에 더해진다.
    /// </summary>
    public static class ApHandicap
    {
        /// <summary>배치가 끝나 전투가 시작될 때 후공에게 한 번 더하는 AP.</summary>
        public static int SecondStartBonus;

        /// <summary>후공의 턴이 시작될 때마다 더하는 AP(최대치까지).</summary>
        public static int SecondTurnBonus;

        public static bool Active => SecondStartBonus > 0 || SecondTurnBonus > 0;

        private static readonly System.Reflection.MethodInfo Recover =
            typeof(ApPool).GetMethod("Recover", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public static void Add(GameController game, Team team, int amount)
        {
            if (amount > 0)
                Recover.Invoke(game.State.GetPlayer(team).Ap, new object[] { amount });
        }
    }

    /// <summary>프리셋 규칙/맵으로 두 봇의 경기를 끝까지 진행한다. 봇의 판단 시간을 턴 단위로 잰다.</summary>
    public static class ArenaMatch
    {
        public static GameController CreateGame(int seed)
        {
            return GameController.Create(new GameConfig
            {
                Rules = PrototypeTestPreset.CreateRules(),
                Map = PrototypeTestPreset.CreateTestMap(),
                Characters = PrototypeTestPreset.CreateCharacters(),
                Skills = PrototypeTestPreset.CreateSkills(),
                Seed = seed,
            });
        }

        public static GameResult Play(BotSpec a, BotSpec b, int seed, bool aIsPlayer1, int maxTurns)
        {
            var result = new GameResult { Seed = seed, AIsPlayer1 = aIsPlayer1 };
            GameController game = CreateGame(seed);

            // 봇 시드는 경기 시드와 자리에서만 정한다(같은 seed 의 두 판에서 각 봇은 같은 시드를 받는다).
            BotPlayer botA = a.Create(seed * 2 + 1);
            BotPlayer botB = b.Create(seed * 2 + 2);
            BotPlayer player1 = aIsPlayer1 ? botA : botB;
            BotPlayer player2 = aIsPlayer1 ? botB : botA;
            Team teamA = aIsPlayer1 ? Team.Player1 : Team.Player2;

            try
            {
                BotMatch.Place(game, Team.Player1, player1);
                BotMatch.Place(game, Team.Player2, player2);
                ApHandicap.Add(game, Team.Player2, ApHandicap.SecondStartBonus);

                var watch = new Stopwatch();
                while (!game.State.IsGameOver && game.State.TurnNumber <= maxTurns)
                {
                    Team team = game.State.CurrentTeam;
                    if (team == Team.Player2 && game.State.TurnNumber > 1)
                        ApHandicap.Add(game, Team.Player2, ApHandicap.SecondTurnBonus);
                    watch.Restart();
                    PlayTurn(game, team == Team.Player1 ? player1 : player2, team);
                    watch.Stop();
                    (team == teamA ? result.TimingA : result.TimingB).AddTurn(watch.Elapsed.TotalMilliseconds);
                }
            }
            catch (Exception e)
            {
                result.Error = $"{game.State.CurrentTeam}({(game.State.CurrentTeam == teamA ? a.Name : b.Name)}) 턴 {game.State.TurnNumber}: {e.Message}";
            }

            result.Turns = game.State.TurnNumber;
            result.EndReason = game.State.IsGameOver ? EndReason(game) : "TurnCap";
            Team? winner = game.State.IsGameOver ? game.State.Winner : null;
            result.ScoreA = winner == null ? 0.5 : winner == teamA ? 1 : 0;
            return result;
        }

        /// <summary>BotMatch.PlayTurn 과 같되, 판단 시간에 실행 시간이 섞이지 않도록 행동 실행을 따로 한다.</summary>
        private static void PlayTurn(GameController game, BotPlayer bot, Team team)
        {
            while (!game.State.IsGameOver && game.State.CurrentTeam == team)
            {
                BotAction action = bot.DecideNext(game, team) ?? BotAction.EndTurn(team);
                if (!action.Execute(game))
                    throw new InvalidOperationException($"봇 행동 거부: {action}");
                if (action.Kind == BotActionKind.EndTurn)
                    break;
            }
        }

        private static string EndReason(GameController game)
        {
            foreach (GameEvent e in game.State.Events.All)
            {
                if (e is GameEndedEvent ended)
                    return ended.Reason.ToString();
            }

            return "-";
        }
    }
}
