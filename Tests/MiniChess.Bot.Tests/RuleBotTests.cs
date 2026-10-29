using MiniChess.Bot.Rules;
using MiniChess.Core.Actions;
using MiniChess.Core.Characters.Pieces;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;
using MiniChess.Core.Events;
using MiniChess.Core.Presets;
using MiniChess.Core.Setup;
using MiniChess.Core.State;
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
        public void Uses_a_skill_and_a_basic_attack_in_the_same_turn()
        {
            // 기본 공격과 각 스킬은 턴에 한 번씩 함께 쓸 수 있다: 강타(3 AP) + 기본 공격(2 AP) 모두 AP 8 안.
            GameController game = CreateSmallBattle(
                new[]
                {
                    "...",
                    ".2.",
                    ".1.",
                },
                new[] { WarriorPiece.PieceId },
                new[] { GardenerPiece.PieceId });

            List<BotAction> played = BotMatch.PlayTurn(game, new RuleBot(1));

            Assert.Contains(played, action => action.Kind == BotActionKind.Attack);
            Assert.Contains(played, action => action.Kind == BotActionKind.UseSkill && action.SkillId == WarriorPiece.SmashId);
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

        // 자리 교정(고른 방향으로 밀기)은 도착 칸마다 점수가 갈리고, 내 덫 쪽으로 미는 수가 가장 높다.
        [Fact]
        public void Correction_scores_by_landing_cell_and_prefers_pushing_into_my_trap()
        {
            GameController game = CreateSmallBattle(
                new[]
                {
                    "2....",
                    ".....",
                    "..2..",
                    ".....",
                    ".11..",
                },
                new[] { SculptorPiece.PieceId, ChemistPiece.PieceId },
                new[] { WarriorPiece.PieceId, GardenerPiece.PieceId });
            Position enemy = game.State.GetPlayer(Team.Player2).Units.Single(unit => unit.Position.Value.Y == 2).Position.Value;
            int chemist = game.State.GetPlayer(Team.Player1).Units.Single(unit => unit.Stats.Base.Id == ChemistPiece.PieceId).Id;
            Position trap = enemy.Offset(1, 0);
            Assert.True(game.UseSkill(new UseSkillRequest(Team.Player1, chemist, ChemistPiece.RootTrapId, trap)).Success);

            var scorer = new ActionScorer(new BoardAnalysis(game, Team.Player1, new BotWeights()));
            List<(BotAction Action, double Score)> corrections = BotActionGenerator.Generate(game, Team.Player1)
                .Where(action => action.Kind == BotActionKind.UseSkill && action.SkillId == SculptorPiece.CorrectionId)
                .Select(action => (action, scorer.Score(action)))
                .OrderByDescending(pair => pair.Item2)
                .ToList();

            Assert.True(corrections.Select(pair => pair.Score).Distinct().Count() > 1, "모든 밀기 변형의 점수가 같음");
            Position best = corrections[0].Action.Targets[1];
            Assert.True(best.Y == enemy.Y && best.X > enemy.X, $"덫 쪽이 아닌 {best} 로 밈");
        }

        // 형세를 뒤집는 공격(비김 → 앞섬)도 판 점수를 올려야 한다. 신중함 배수를 판마다 다시 정하면 앞선 판만 위협도 감점이 커져 손해로 보인다.
        [Fact]
        public void Evaluation_with_the_search_start_caution_rewards_an_attack_that_takes_the_lead()
        {
            GameController game = CreateSmallBattle(
                new[]
                {
                    "22.",
                    "11.",
                    "...",
                },
                new[] { WarriorPiece.PieceId, ArcherPiece.PieceId },
                new[] { WarriorPiece.PieceId, ArcherPiece.PieceId });
            var weights = new BotWeights();
            Unit attacker = game.State.GetPlayer(Team.Player1).Units.Single(unit => unit.Stats.Base.Id == WarriorPiece.PieceId);
            Unit target = game.GetAttackTargets(attacker.Id).First();
            double caution = new BoardAnalysis(game, Team.Player1, weights).Caution;
            double before = Search.PositionEvaluator.Evaluate(game, Team.Player1, weights, stalled: false, caution);

            Assert.True(game.Attack(new AttackRequest(Team.Player1, attacker.Id, target.Id)).Success);

            double after = Search.PositionEvaluator.Evaluate(game, Team.Player1, weights, stalled: false, caution);
            double afterRecomputed = Search.PositionEvaluator.Evaluate(game, Team.Player1, weights, stalled: false);
            _output.WriteLine($"공격 전 {before:F2}, 공격 후(고정) {after:F2}, 공격 후(판마다 다시 정함) {afterRecomputed:F2}");
            Assert.Equal(1, new BoardAnalysis(game, Team.Player1, weights).Caution);
            Assert.True(after > before);
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

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Eliminates_a_passive_opponent(int seed)
        {
            // 앞서면 신중해지지만, 상대가 다가오지 않아 교착이면 들어가서 끝낸다.
            GameController game = BotMatchTests.CreatePresetGame(seed);

            BotMatch.Run(game, new RuleBot(seed), new PassiveBot(seed));

            Assert.Equal(Team.Player1, game.State.Winner);
            Assert.Equal(GameEndReason.Elimination.ToString(), LastEndReason(game));
        }

        [Fact]
        public void One_on_one_endgame_does_not_loop_on_shield_and_shot()
        {
            // 정원사가 매 턴 자기 보호막만 다시 걸고 궁수가 그 보호막만 깎는 반복으로 턴 제한까지 가지 않아야 한다.
            GameController game = CreateSmallBattle(
                new[]
                {
                    ".....2.",
                    ".......",
                    "...#...",
                    "#.....#",
                    "...#...",
                    ".......",
                    ".1.....",
                },
                new[] { GardenerPiece.PieceId },
                new[] { ArcherPiece.PieceId });

            BotMatch.Run(game, new RuleBot(1), new RuleBot(2));

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
