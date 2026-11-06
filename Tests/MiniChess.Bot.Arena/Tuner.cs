using System.Diagnostics;

namespace MiniChess.Bot.Arena
{
    /// <summary>
    /// 판 평가 값 자동 튜닝(SPSA). 매 반복마다 모든 값을 같이 ±c 만큼(로그 공간, 즉 비율로) 흔든 두 봇을
    /// 같은 시드에서 자리를 바꿔 가며 대전시키고, 이긴 쪽 방향으로 값을 조금 옮긴다.
    /// 값마다 따로 실험하지 않아 값 수가 늘어도(새 평가 항목) 반복 한 번의 비용은 같다.
    /// 값은 기본값의 1/8 ~ 8 배, 확률(…Chance)은 1 이하로 묶는다.
    /// </summary>
    public static class Tuner
    {
        public static void Run(string engineArgs, TunedParams start, int iterations, int gamesPerIteration, int seed, int threads, string outPath)
        {
            TunedParams defaults = TunedParams.Default();
            List<string> names = start.Values.Keys.ToList();
            var x = names.ToDictionary(name => name, name => Math.Log(start.Values[name]));
            var rng = new Random(seed);
            const double a = 0.4, c = 0.15, stability = 50;
            int pairs = Math.Max(1, gamesPerIteration / 2);
            var watch = Stopwatch.StartNew();
            double recentScore = 0;

            Console.WriteLine($"튜닝: {names.Count}개 값, 반복 {iterations}, 반복당 {pairs * 2}경기, 봇 engine:{engineArgs}");
            for (int k = 0; k < iterations; k++)
            {
                double ak = a / Math.Pow(k + 1 + stability, 0.602);
                double ck = c / Math.Pow(k + 1, 0.101);
                Dictionary<string, int> delta = names.ToDictionary(name => name, _ => rng.Next(2) == 0 ? -1 : 1);

                TunedParams plus = FromLog(names, name => x[name] + ck * delta[name], defaults);
                TunedParams minus = FromLog(names, name => x[name] - ck * delta[name], defaults);
                BotSpec plusSpec = BotSpec.ForEngine(engineArgs, plus, "plus");
                BotSpec minusSpec = BotSpec.ForEngine(engineArgs, minus, "minus");

                // 반복마다 새 시드(같은 국면에 맞춰지지 않게).
                int baseSeed = seed * 100000 + k * pairs;
                var results = new GameResult[pairs * 2];
                Parallel.For(0, pairs * 2, new ParallelOptions { MaxDegreeOfParallelism = threads }, index =>
                    results[index] = ArenaMatch.Play(plusSpec, minusSpec, baseSeed + index / 2, aIsPlayer1: index % 2 == 0, maxTurns: 400));

                double score = results.Where(r => r.Error == null).Select(r => r.ScoreA).DefaultIfEmpty(0.5).Average();
                recentScore += score - 0.5;
                double gradientScale = (2 * score - 1) / (2 * ck);
                foreach (string name in names)
                    x[name] = Clamp(name, x[name] + ak * gradientScale * delta[name], defaults);

                if ((k + 1) % 10 == 0 || k + 1 == iterations)
                {
                    TunedParams current = FromLog(names, name => x[name], defaults);
                    current.Save(outPath, $"SPSA 반복 {k + 1}/{iterations}, engine:{engineArgs}, 반복당 {pairs * 2}경기");
                    Console.WriteLine($"[{k + 1}/{iterations}] {watch.Elapsed.TotalMinutes:F1}분, 최근 10회 |plus-0.5| 평균 {Math.Abs(recentScore) / 10:F3}");
                    Console.WriteLine("  " + current);
                    recentScore = 0;
                }
            }
        }

        private static TunedParams FromLog(List<string> names, Func<string, double> log, TunedParams defaults)
        {
            var p = new TunedParams();
            foreach (string name in names)
                p.Values[name] = Math.Exp(Clamp(name, log(name), defaults));
            return p;
        }

        private static double Clamp(string name, double logValue, TunedParams defaults)
        {
            double center = Math.Log(defaults.Values[name]);
            double low = center - Math.Log(8), high = center + Math.Log(8);
            if (name.EndsWith("Chance"))
                high = Math.Min(high, 0);
            return Math.Min(high, Math.Max(low, logValue));
        }
    }
}
