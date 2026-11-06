using MiniChess.Bot.Engine;
using MiniChess.Bot.Search;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.State;
using Xunit;
using Xunit.Abstractions;

namespace MiniChess.Bot.Tests
{
    public class EngineTests
    {
        private readonly ITestOutputHelper _output;

        public EngineTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static GameController PlacedGame(int seed)
        {
            GameController game = BotMatchTests.CreatePresetGame(seed);
            BotMatch.Place(game, Team.Player1, new RandomBot(seed * 2));
            BotMatch.Place(game, Team.Player2, new RandomBot(seed * 2 + 1));
            return game;
        }

        [Fact]
        public void Hash_is_equal_for_copies_and_changes_after_an_action()
        {
            GameController game = PlacedGame(1);
            Team team = game.State.CurrentTeam;

            ulong original = StateHasher.Hash(game, team);
            Assert.Equal(original, StateHasher.Hash(game.Fork(), team));
            Assert.Equal(original, StateHasher.Hash(game.Fork(team), team));

            foreach (BotAction action in BotActionGenerator.Generate(game, team))
            {
                GameController copy = game.Fork();
                Assert.True(action.Execute(copy));
                Assert.NotEqual(original, StateHasher.Hash(copy, team));
            }
        }

        [Fact]
        public void Two_independent_moves_in_either_order_give_the_same_hash()
        {
            GameController game = PlacedGame(2);
            Team team = game.State.CurrentTeam;
            List<BotAction> moves = BotActionGenerator.Generate(game, team).Where(action => action.Kind == BotActionKind.Move).ToList();

            foreach (BotAction first in moves)
            foreach (BotAction second in moves.Where(move => move.UnitId != first.UnitId && !move.Cell.Equals(first.Cell)))
            {
                GameController ab = game.Fork();
                GameController ba = game.Fork();
                if (!first.Execute(ab) || !second.Execute(ab) || !second.Execute(ba) || !first.Execute(ba))
                    continue;

                Assert.Equal(StateHasher.Hash(ab, team), StateHasher.Hash(ba, team));
                return;
            }

            Assert.Fail("서로 다른 두 유닛의 이동 순서쌍을 찾지 못함");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Engine_bot_beats_RandomBot_without_rejected_actions(int seed)
        {
            GameController game = BotMatchTests.CreatePresetGame(seed);
            var engine = new EngineBot(seed, new EngineSettings { BeamWidth = 4 });

            BotMatch.Run(game, engine, new RandomBot(seed));

            _output.WriteLine($"승자 {game.State.Winner}, 턴 {game.State.TurnNumber}, 재계획 {engine.Replans}");
            Assert.Equal(Team.Player1, game.State.Winner);
        }

        private static EngineSettings SmallDeepSettings(bool parallel) => new EngineSettings
        {
            Depth = 3, MyPlans = 3, ReplyPlans = 2, BeamWidth = 2, MaxActions = 6, Parallel = parallel,
        };

        // 병렬 탐색(알파 공유 없음)과 순차 탐색(알파베타)이 같은 계획을 고른다.
        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        public void Parallel_and_sequential_search_choose_the_same_plan(int seed)
        {
            GameController game = PlacedGame(seed);
            for (int turn = 0; turn < 4 && !game.State.IsGameOver; turn++)
            {
                Team team = game.State.CurrentTeam;
                var sequential = new EngineBot(1, SmallDeepSettings(parallel: false));
                var parallel = new EngineBot(1, SmallDeepSettings(parallel: true));

                BotAction a = sequential.DecideNext(game, team);
                BotAction b = parallel.DecideNext(game, team);

                Assert.True(a.SameAs(b), $"턴 {game.State.TurnNumber}: 순차 {a}, 병렬 {b}");
                BotMatch.PlayTurn(game, BotFactory.Create(BotDifficulty.Easy, seed + turn));
            }
        }

        [Fact]
        public void Deep_search_finishes_a_match_without_rejected_actions()
        {
            GameController game = BotMatchTests.CreatePresetGame(6);
            var engine = new EngineBot(6, SmallDeepSettings(parallel: true));

            BotMatch.Run(game, engine, BotFactory.Create(BotDifficulty.Easy, 6));

            _output.WriteLine($"승자 {game.State.Winner}, 턴 {game.State.TurnNumber}, 재계획 {engine.Replans}");
            Assert.True(game.State.IsGameOver);
        }

        // 노드 예산과 취소가 지켜지고, 그래도 할 수 있는 행동을 돌려준다.
        [Fact]
        public void Node_budget_and_cancellation_limit_the_search()
        {
            GameController game = PlacedGame(3);
            Team team = game.State.CurrentTeam;
            var limited = new EngineBot(1, new EngineSettings { NodeBudget = 50 });
            var cancelled = new EngineBot(1);

            BotAction a = limited.DecideNext(game, team);
            BotAction b = cancelled.DecideNext(game, team, new CancellationToken(canceled: true));

            Assert.True(limited.LastNodesSearched <= 50, $"예산 50, 읽은 판 {limited.LastNodesSearched}");
            Assert.Equal(0, cancelled.LastNodesSearched);
            List<BotAction> legal = BotActionGenerator.Generate(game, team);
            legal.Add(BotAction.EndTurn(team));
            Assert.Contains(legal, candidate => candidate.SameAs(a));
            Assert.Contains(legal, candidate => candidate.SameAs(b));
        }
    }
}
