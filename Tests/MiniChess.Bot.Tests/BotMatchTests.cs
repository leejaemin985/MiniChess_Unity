using MiniChess.Bot;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Presets;
using MiniChess.Core.State;
using Xunit;

namespace MiniChess.Bot.Tests
{
    public class BotMatchTests
    {
        /// <summary>플레이 테스트 프리셋으로 배치 단계부터 시작하는 경기.</summary>
        internal static GameController CreatePresetGame(int seed)
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

        [Fact]
        public void RandomBot_placement_is_accepted_and_battle_starts()
        {
            GameController game = CreatePresetGame(1);

            BotMatch.Place(game, Team.Player1, new RandomBot(1));
            BotMatch.Place(game, Team.Player2, new RandomBot(2));

            Assert.Equal(GamePhase.Battle, game.State.Phase);
            Assert.Equal(game.State.Rules.Match.UnitsPerTeam, game.State.GetPlayer(Team.Player1).Units.Count);
        }

        [Fact]
        public void Generated_actions_all_succeed_when_executed_on_a_fresh_copy()
        {
            // 같은 시드와 같은 요청 순서면 같은 경기라서, 같은 지점까지 다시 진행한 복사본에서 후보를 하나씩 실행해 본다.
            GameController game = CreatePresetGame(7);
            var bot1 = new RandomBot(11);
            var bot2 = new RandomBot(12);
            BotMatch.Place(game, Team.Player1, bot1);
            BotMatch.Place(game, Team.Player2, bot2);

            var candidates = BotActionGenerator.Generate(game, game.State.CurrentTeam);
            Assert.NotEmpty(candidates);

            foreach (BotAction action in candidates)
            {
                GameController copy = CreatePresetGame(7);
                BotMatch.Place(copy, Team.Player1, new RandomBot(11));
                BotMatch.Place(copy, Team.Player2, new RandomBot(12));

                Assert.True(action.Execute(copy), $"후보 실행 실패: {action}");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void RandomBots_play_a_full_match_without_rejected_actions(int seed)
        {
            GameController game = CreatePresetGame(seed);

            BotMatch.Run(game, new RandomBot(seed * 100 + 1), new RandomBot(seed * 100 + 2));

            Assert.True(game.State.IsGameOver);
        }
    }
}
