using MiniChess.Bot.Rules;
using MiniChess.Core.Characters.Pieces;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;
using MiniChess.Core.Events;
using MiniChess.Core.Presets;
using MiniChess.Core.Setup;
using Xunit;
using Xunit.Abstractions;

namespace MiniChess.Bot.Tests
{
    public class RuleBotTests
    {
        private readonly ITestOutputHelper _output;

        public RuleBotTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>작은 맵에서 바로 Battle 로 시작하는 경기(프리셋 규칙/캐릭터/스킬).</summary>
        private static GameController CreateSmallBattle(string[] rows, string[] p1, string[] p2)
        {
            return QuickBattleFactory.Create(
                SmallRules(p1.Length),
                new MapData { Name = "BotTest", Rows = rows },
                PrototypeTestPreset.CreateCharacters(),
                p1,
                p2,
                PrototypeTestPreset.CreateSkills());
        }

        private static GameRuleData SmallRules(int unitsPerTeam)
        {
            GameRuleData rules = PrototypeTestPreset.CreateRules();
            rules.Match.UnitsPerTeam = unitsPerTeam;
            return rules;
        }

        [Fact]
        public void Attacks_an_adjacent_enemy_instead_of_ending_the_turn()
        {
            GameController game = CreateSmallBattle(
                new[]
                {
                    "...",
                    ".2.",
                    ".1.",
                },
                new[] { WarriorPiece.PieceId },
                new[] { ArcherPiece.PieceId });

            var bot = new RuleBot(1);
            BotAction action = bot.DecideNext(game, Team.Player1);

            Assert.NotEqual(BotActionKind.EndTurn, action.Kind);
            Assert.NotEqual(BotActionKind.Move, action.Kind);
        }

        [Fact]
        public void Approaches_a_distant_enemy()
        {
            GameController game = CreateSmallBattle(
                new[]
                {
                    "..2..",
                    ".....",
                    ".....",
                    ".....",
                    ".....",
                    "..1..",
                },
                new[] { WarriorPiece.PieceId },
                new[] { GardenerPiece.PieceId });

            var bot = new RuleBot(1);
            BotMatch.PlayTurn(game, bot);

            var warrior = game.State.GetPlayer(Team.Player1).Units[0];
            Assert.True(warrior.Position.Value.Y < 5, $"전사가 다가가지 않음: {warrior.Position}");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void RuleBots_finish_a_match_by_elimination_without_stalling(int seed)
        {
            // 양쪽이 서로의 사거리 앞에서 버티기만 하면 턴 제한까지 간다. 형세가 같거나 뒤지면 들어가야 한다.
            GameController game = BotMatchTests.CreatePresetGame(seed);

            BotMatch.Run(game, new RuleBot(seed * 10 + 1), new RuleBot(seed * 10 + 2));

            Assert.True(game.State.IsGameOver);
            Assert.Equal(GameEndReason.Elimination.ToString(), LastEndReason(game));
            _output.WriteLine($"승자 {game.State.Winner}, 턴 {game.State.TurnNumber}");
        }

        [Fact]
        public void RuleBot_beats_RandomBot_in_most_matches()
        {
            int ruleWins = 0;
            const int matches = 20;

            for (int seed = 1; seed <= matches; seed++)
            {
                GameController game = BotMatchTests.CreatePresetGame(seed);
                // 선공 이점을 지우려고 판마다 자리를 바꾼다.
                bool ruleIsPlayer1 = seed % 2 == 0;
                BotPlayer rule = new RuleBot(seed);
                BotPlayer random = new RandomBot(seed);
                BotMatch.Run(game, ruleIsPlayer1 ? rule : random, ruleIsPlayer1 ? random : rule);

                Team ruleTeam = ruleIsPlayer1 ? Team.Player1 : Team.Player2;
                if (game.State.Winner == ruleTeam)
                    ruleWins++;

                _output.WriteLine($"seed {seed}: 승자 {game.State.Winner?.ToString() ?? "무승부"} (규칙 봇 {ruleTeam}), 턴 {game.State.TurnNumber}, 사유 {LastEndReason(game)}");
            }

            _output.WriteLine($"규칙 봇 승리 {ruleWins}/{matches}");
            Assert.True(ruleWins >= matches * 0.8, $"규칙 봇 승리 {ruleWins}/{matches}");
        }

        internal static string LastEndReason(GameController game)
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
