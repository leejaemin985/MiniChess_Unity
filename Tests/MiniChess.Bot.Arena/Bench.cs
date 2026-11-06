using System.Diagnostics;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot.Arena
{
    /// <summary>
    /// 탐색 비용 측정: 실제 경기(봇끼리)에서 국면을 뽑아
    ///   - 턴 시작의 합법 행동 수,
    ///   - 한 턴에 가능한 행동 순서 수(Knuth 추정: 무작위로 따라가며 분기 수를 곱한다, 대체로 적게 나온다),
    ///   - Fork / 합법 수 생성 / 행동 실행 1회 비용
    /// 을 잰다.
    /// </summary>
    public static class Bench
    {
        public static void Run(BotSpec player, int games, int seed, int samplesPerPosition)
        {
            var rng = new Random(seed);
            var branching = new List<int>();
            var logSequences = new List<double>();
            var steps = new List<int>();
            var fork = new Timer();
            var generate = new Timer();
            var execute = new Timer();

            for (int g = 0; g < games; g++)
            {
                GameController game = ArenaMatch.CreateGame(seed + g);
                BotPlayer p1 = player.Create(seed * 100 + g * 2 + 1);
                BotPlayer p2 = player.Create(seed * 100 + g * 2 + 2);
                BotMatch.Place(game, Team.Player1, p1);
                BotMatch.Place(game, Team.Player2, p2);

                while (!game.State.IsGameOver && game.State.TurnNumber < 80)
                {
                    if (game.State.TurnNumber % 3 == 0)
                        Sample(game, rng, samplesPerPosition, branching, logSequences, steps, fork, generate, execute);
                    BotMatch.PlayTurn(game, game.State.CurrentTeam == Team.Player1 ? p1 : p2);
                }
            }

            branching.Sort();
            logSequences.Sort();
            Console.WriteLine($"국면 {branching.Count}개 (봇 {player.Name}, 경기 {games})");
            Console.WriteLine($"턴 시작 합법 행동 수: 최소 {branching[0]} / 중간 {branching[branching.Count / 2]} / 최대 {branching[^1]} / 평균 {branching.Average():F1}");
            Console.WriteLine($"한 턴 행동 순서 수 log10: 최소 {logSequences[0]:F1} / 중간 {Pct(logSequences, 0.5):F1} / 상위10% {Pct(logSequences, 0.9):F1} / 최대 {logSequences[^1]:F1}");
            Console.WriteLine($"무작위 진행 한 턴 행동 수: 평균 {steps.Average():F1} / 최대 {steps.Max()}");
            Console.WriteLine($"1회 비용: Fork {fork.Micros:F1}us, 합법 수 생성 {generate.Micros:F1}us, 행동 실행 {execute.Micros:F1}us");
        }

        private static void Sample(GameController game, Random rng, int samples, List<int> branching, List<double> logSequences,
            List<int> steps, Timer fork, Timer generate, Timer execute)
        {
            Team team = game.State.CurrentTeam;
            branching.Add(BotActionGenerator.Generate(game, team).Count + 1);

            double sum = 0;
            for (int s = 0; s < samples; s++)
            {
                GameController copy = fork.Time(() => game.Fork());
                double product = 1, nodes = 1;
                int count = 0;
                while (!copy.State.IsGameOver && copy.State.CurrentTeam == team && count < 40)
                {
                    List<BotAction> actions = generate.Time(() => BotActionGenerator.Generate(copy, team));
                    int b = actions.Count + 1; // + 턴 종료
                    product *= b;
                    nodes += product;
                    int pick = rng.Next(b);
                    if (pick == actions.Count)
                        break;
                    execute.Time(() => actions[pick].Execute(copy));
                    count++;
                }

                steps.Add(count);
                sum += nodes;
            }

            logSequences.Add(Math.Log10(sum / samples));
        }

        private static double Pct(List<double> sorted, double q) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * q))];

        private sealed class Timer
        {
            private readonly Stopwatch _watch = new Stopwatch();
            private long _count;

            public double Micros => _watch.Elapsed.TotalMilliseconds * 1000 / Math.Max(1, _count);

            public T Time<T>(Func<T> action)
            {
                _watch.Start();
                T value = action();
                _watch.Stop();
                _count++;
                return value;
            }
        }
    }
}
