using System.Diagnostics;
using System.Threading;
using MiniChess.Bot.Search;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using Xunit;
using Xunit.Abstractions;

namespace MiniChess.Bot.Tests
{
    /// <summary>난이도(읽는 깊이)가 높을수록 이긴다. 선공 이점을 지우려고 판마다 자리를 바꾼다.</summary>
    public class DifficultyTests
    {
        private readonly ITestOutputHelper _output;

        public DifficultyTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>stronger 가 이긴 판 수. 무승부는 세지 않는다.</summary>
        private int Wins(BotDifficulty stronger, BotDifficulty weaker, int matches)
        {
            int wins = 0;
            var watch = new Stopwatch();
            int turns = 0;

            for (int seed = 1; seed <= matches; seed++)
            {
                GameController game = BotMatchTests.CreatePresetGame(seed);
                bool strongIsPlayer1 = seed % 2 == 0;
                BotPlayer strong = BotFactory.Create(stronger, seed * 10 + 1);
                BotPlayer weak = BotFactory.Create(weaker, seed * 10 + 2);

                watch.Start();
                BotMatch.Run(game, strongIsPlayer1 ? strong : weak, strongIsPlayer1 ? weak : strong);
                watch.Stop();
                turns += game.State.TurnNumber;

                Team strongTeam = strongIsPlayer1 ? Team.Player1 : Team.Player2;
                if (game.State.Winner == strongTeam)
                    wins++;

                _output.WriteLine($"seed {seed}: 승자 {game.State.Winner?.ToString() ?? "무승부"} ({stronger} = {strongTeam}), 턴 {game.State.TurnNumber}, 사유 {RuleBotTests.LastEndReason(game)}");
            }

            _output.WriteLine($"{stronger} 승리 {wins}/{matches}, 턴당 평균 {watch.Elapsed.TotalMilliseconds / turns:F1} ms");
            return wins;
        }

        [Fact]
        public void Normal_beats_Easy_in_most_matches()
        {
            const int matches = 20;
            int wins = Wins(BotDifficulty.Normal, BotDifficulty.Easy, matches);
            Assert.True(wins >= matches * 0.65, $"보통 승리 {wins}/{matches}");
        }

        [Fact]
        public void Hard_beats_Normal_in_most_matches()
        {
            const int matches = 20;
            int wins = Wins(BotDifficulty.Hard, BotDifficulty.Normal, matches);
            Assert.True(wins >= matches * 0.55, $"어려움 승리 {wins}/{matches}");
        }

        // 깊이 3 과 시간 제한(읽다 멈춘 결과로 고르기)에서도 거부되는 행동 없이 경기를 끝낸다(BotMatch 는 거부되면 예외).
        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public void Depth3_search_finishes_a_match_without_rejected_actions(int timeLimitMs)
        {
            GameController game = BotMatchTests.CreatePresetGame(3);
            var deep = new SearchBot(1, new SearchSettings { Depth = 3, TimeLimitMs = timeLimitMs });

            BotMatch.Run(game, deep, BotFactory.Create(BotDifficulty.Normal, 2));

            Assert.True(game.State.IsGameOver);
        }

        // 판단이 취소되면(경기를 나감 등) 깊이 3 이라도 거의 읽지 않고 바로 끝내며, 그래도 할 수 있는 행동을 돌려준다.
        [Fact]
        public void Cancelled_search_returns_early_with_a_legal_action()
        {
            GameController game = BotMatchTests.CreatePresetGame(3);
            BotMatch.Place(game, Team.Player1, new RandomBot(1));
            BotMatch.Place(game, Team.Player2, new RandomBot(2));
            BotMatch.PlayTurn(game, BotFactory.Create(BotDifficulty.Normal, 1));
            var full = new SearchBot(1, new SearchSettings { Depth = 3 });
            var cancelled = new SearchBot(1, new SearchSettings { Depth = 3 });

            full.DecideNext(game, game.State.CurrentTeam);
            BotAction action = cancelled.DecideNext(game, game.State.CurrentTeam, new CancellationToken(canceled: true));

            Assert.NotNull(action);
            Assert.Contains(BotActionGenerator.Generate(game, game.State.CurrentTeam).Append(BotAction.EndTurn(game.State.CurrentTeam)), candidate => candidate.SameAs(action));
            Assert.True(cancelled.LastNodesSearched * 10 < full.LastNodesSearched,
                $"취소: {cancelled.LastNodesSearched}, 전체: {full.LastNodesSearched}");
        }

        [Fact]
        public void Search_bot_beats_RandomBot()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                GameController game = BotMatchTests.CreatePresetGame(seed);
                BotMatch.Run(game, new SearchBot(seed), new RandomBot(seed));
                Assert.Equal(Team.Player1, game.State.Winner);
            }
        }
    }
}
