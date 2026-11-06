using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Presets;

namespace MiniChess.Bot.Engine
{
    /// <summary>벤치마크 한 턴의 결과.</summary>
    public sealed class BenchmarkTurn
    {
        public int Level;
        public int TurnNumber;
        public double Seconds;
        public long Nodes;

        /// <summary>이 턴의 단계별 비용(SearchProfile).</summary>
        internal List<(ProfileStep Step, double Seconds, long Calls)> Steps;

        public override string ToString() => $"L{Level} turn {TurnNumber}: {Seconds:F1}s ({Nodes} nodes)";
    }

    /// <summary>
    /// 레벨별 한 턴 판단 시간 측정(기기 성능 확인용). 레벨마다 프리셋 경기를 새로 만들어, 측정할 레벨 봇(선공)과
    /// 가벼운 상대(레벨 6, 후공)로 진행하며 측정 봇의 턴마다 판단 시간을 잰다. 시드가 같으면 어느 기기에서나 같은 국면이다.
    /// 시간 상한(BotLevels.TimeLimitMs)은 끈다(상한에 걸려 실제 시간이 가려지지 않게).
    /// Unity 에 의존하지 않으므로 기기(개발 빌드의 BotBenchmarkPanel)와 Arena 어디서나 같은 결과를 낸다.
    /// </summary>
    public static class BotBenchmark
    {
        public const int DefaultSeed = 31;

        /// <param name="ownTurns">레벨마다 잴 측정 봇의 턴 수(경기가 먼저 끝나면 그만큼만).</param>
        /// <param name="onTurn">한 턴을 잴 때마다 불린다(측정 스레드에서).</param>
        public static List<BenchmarkTurn> Run(IReadOnlyList<int> levels, int ownTurns, int seed, Action<BenchmarkTurn> onTurn, CancellationToken cancellation)
        {
            var results = new List<BenchmarkTurn>();
            foreach (int level in levels)
            {
                GameController game = GameController.Create(new GameConfig
                {
                    Rules = PrototypeTestPreset.CreateRules(),
                    Map = PrototypeTestPreset.CreateTestMap(),
                    Characters = PrototypeTestPreset.CreateCharacters(),
                    Skills = PrototypeTestPreset.CreateSkills(),
                    Seed = seed,
                });

                EngineSettings settings = BotLevels.Settings(level);
                settings.TimeLimitMs = 0;
                var measured = new EngineBot(seed * 2 + 1, settings);
                var opponent = new EngineBot(seed * 2 + 2, BotLevels.Settings(6));
                BotMatch.Place(game, Team.Player1, measured);
                BotMatch.Place(game, Team.Player2, opponent);

                int measuredTurns = 0;
                var watch = new Stopwatch();
                while (!game.State.IsGameOver && measuredTurns < ownTurns && !cancellation.IsCancellationRequested)
                {
                    Team team = game.State.CurrentTeam;
                    bool isMeasured = team == Team.Player1;
                    int turnNumber = game.State.TurnNumber;

                    SearchProfile.Reset();
                    SearchProfile.Enabled = isMeasured;
                    watch.Restart();
                    long nodes;
                    try
                    {
                        nodes = PlayTurn(game, isMeasured ? (BotPlayer)measured : opponent, team, isMeasured ? measured : null, cancellation);
                    }
                    finally
                    {
                        watch.Stop();
                        SearchProfile.Enabled = false;
                    }

                    if (!isMeasured || cancellation.IsCancellationRequested)
                        continue;

                    measuredTurns++;
                    var turn = new BenchmarkTurn
                    {
                        Level = level, TurnNumber = turnNumber, Seconds = watch.Elapsed.TotalSeconds, Nodes = nodes, Steps = SearchProfile.Read(),
                    };
                    results.Add(turn);
                    onTurn?.Invoke(turn);
                }
            }

            return results;
        }

        /// <summary>
        /// 레벨별 평균/최대 시간 요약과, 그 아래 단계별 비용 분해(전체 시간 중 비율, 1회 평균 us, 횟수). other = 단계 밖(정렬, 할당 등).
        /// </summary>
        public static IEnumerable<string> Summary(IEnumerable<BenchmarkTurn> turns)
        {
            foreach (IGrouping<int, BenchmarkTurn> group in turns.GroupBy(turn => turn.Level).OrderBy(group => group.Key))
            {
                double total = group.Sum(t => t.Seconds);
                yield return $"L{group.Key}: avg {group.Average(t => t.Seconds):F1}s, max {group.Max(t => t.Seconds):F1}s ({group.Count()} turns)";

                List<(ProfileStep Step, double Seconds, long Calls)> steps = group.Where(t => t.Steps != null).SelectMany(t => t.Steps)
                    .GroupBy(s => s.Step).Select(g => (g.Key, g.Sum(s => s.Seconds), g.Sum(s => s.Calls))).ToList();
                if (steps.Count == 0 || total <= 0)
                    continue;

                foreach ((ProfileStep step, double seconds, long calls) in steps.OrderByDescending(s => s.Seconds))
                    yield return $"    {step,-9} {100 * seconds / total,5:F1}%  {seconds * 1e6 / Math.Max(1, calls),8:F1}us x {calls}";
                double other = total - steps.Sum(s => s.Seconds);
                yield return $"    {"other",-9} {100 * other / total,5:F1}%";
            }
        }

        /// <summary>한 턴을 끝까지 진행하고, 측정 봇이면 그 턴에 읽은 판 수 합을 돌려준다.</summary>
        private static long PlayTurn(GameController game, BotPlayer bot, Team team, EngineBot measured, CancellationToken cancellation)
        {
            long nodes = 0;
            while (!game.State.IsGameOver && game.State.CurrentTeam == team)
            {
                BotAction action = bot.DecideNext(game, team, cancellation) ?? BotAction.EndTurn(team);
                if (measured != null)
                    nodes = Math.Max(nodes, measured.LastNodesSearched);
                if (!action.Execute(game))
                    throw new InvalidOperationException($"봇 행동 거부: {action}");
                if (action.Kind == BotActionKind.EndTurn)
                    break;
            }

            return nodes;
        }
    }
}
