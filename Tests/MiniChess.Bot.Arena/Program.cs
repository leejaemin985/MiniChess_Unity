using System.Diagnostics;
using MiniChess.Bot.Arena;

const string Usage = @"
봇 대전/측정 실행기 (항상 -c Release 로 실행)

  dotnet run -c Release --project Tests/MiniChess.Bot.Arena -- match <봇A> <봇B> [옵션]
      두 봇을 자리를 바꿔 가며 대전시키고 A 기준 승/무/패와 Elo 차이(95% 구간)를 낸다.
      --games N      경기 수(짝수로 맞춘다, 기본 40)
      --seed S       첫 경기 시드(기본 1)
      --threads T    동시에 돌릴 경기 수(기본 CPU 코어 수)
      --max-turns M  안전용 턴 상한(기본 400, 규칙상 상한은 따로 있다)
      --verbose      경기마다 결과를 찍는다

  dotnet run -c Release --project Tests/MiniChess.Bot.Arena -- bench [봇] [--games N] [--seed S] [--samples K]
      탐색 비용(분기 수, 한 턴 행동 순서 수, Fork/생성/실행 비용)을 잰다. 국면은 [봇](기본 easy)끼리 둔 경기에서 뽑는다.

  dotnet run -c Release --project Tests/MiniChess.Bot.Arena -- tune [--engine d=1,p=0] [--iterations N] [--games G] [--from 파일] [--out 파일]
      엔진 판 평가 값을 SPSA 로 튜닝한다(값을 흔든 두 봇끼리 반복당 G경기). 10회마다 --out(기본 tuned.txt)에 저장한다.
      결과는 engine:d=3@tuned.txt 처럼 봇 지정에 붙여 대전으로 검증한다.

  dotnet run -c Release --project Tests/MiniChess.Bot.Arena -- inspect <엔진봇> <상대봇> [--seed S] [--from T] [--to T]
      엔진봇(P1)과 상대봇(P2)의 경기를 진행하며 엔진봇의 턴마다 루트 계획과 탐색 값, 실제 실행한 행동을 찍는다.

봇: random | easy | normal | hard | rule | search:d=2,b=6,c=8,r=6,v=2,a=10,t=0
     | engine:d=2,m=4,r=4,s=3,b=4,a=10,k=2,n=0,t=0,p=1 | level:1~10   (engine/level 끝에 @튜닝파일)
";

if (args.Length == 0)
{
    Console.WriteLine(Usage);
    return 1;
}

var positional = new List<string>();
var options = new Dictionary<string, string>();
for (int i = 1; i < args.Length; i++)
{
    if (args[i].StartsWith("--"))
    {
        string key = args[i].Substring(2);
        bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--");
        options[key] = hasValue ? args[++i] : "true";
    }
    else
    {
        positional.Add(args[i]);
    }
}

int Option(string key, int fallback) => options.TryGetValue(key, out string value) ? int.Parse(value) : fallback;

switch (args[0])
{
    case "match":
    {
        if (positional.Count != 2)
        {
            Console.WriteLine(Usage);
            return 1;
        }

        BotSpec a = BotSpec.Parse(positional[0]);
        BotSpec b = BotSpec.Parse(positional[1]);
        int pairs = Math.Max(1, Option("games", 40) / 2);
        int seed = Option("seed", 1);
        int threads = Option("threads", Environment.ProcessorCount);
        int maxTurns = Option("max-turns", 400);
        bool verbose = options.ContainsKey("verbose");

        Console.WriteLine($"{a.Name} vs {b.Name}: {pairs * 2}경기, 시드 {seed}~{seed + pairs - 1}, 동시 {threads}");
        var results = new GameResult[pairs * 2];
        int done = 0;
        var watch = Stopwatch.StartNew();

        Parallel.For(0, pairs * 2, new ParallelOptions { MaxDegreeOfParallelism = threads }, index =>
        {
            // 같은 시드로 두 판: A 선공 한 번, B 선공 한 번(선공 이점과 배치 운을 상쇄).
            int gameSeed = seed + index / 2;
            GameResult r = ArenaMatch.Play(a, b, gameSeed, aIsPlayer1: index % 2 == 0, maxTurns);
            results[index] = r;
            int finished = Interlocked.Increment(ref done);

            if (verbose || r.Error != null)
            {
                string outcome = r.Error != null ? $"오류 {r.Error}" : r.ScoreA == 1 ? "A 승" : r.ScoreA == 0 ? "B 승" : "무";
                Console.WriteLine($"  [{finished}/{results.Length}] seed {gameSeed} A={(r.AIsPlayer1 ? "P1" : "P2")}: {outcome}, 턴 {r.Turns}, {r.EndReason}");
            }
        });

        watch.Stop();
        MatchStats stats = MatchStats.From(results);
        (double elo, double low, double high) = stats.Elo();
        Console.WriteLine();
        Console.WriteLine($"A({a.Name}) 승 {stats.Wins} / 무 {stats.Draws} / 패 {stats.Losses}" + (stats.Errors > 0 ? $" / 오류 {stats.Errors}" : ""));
        Console.WriteLine($"점수율 {stats.Score:P1}, Elo 차이 {elo:+0;-0;0} (95% {low:+0;-0;0} ~ {high:+0;-0;0})");
        Console.WriteLine($"A 선공일 때 점수율 {results.Where(r => r.AIsPlayer1 && r.Error == null).Select(r => r.ScoreA).DefaultIfEmpty(0.5).Average():P1}, " +
                          $"B 선공일 때 {results.Where(r => !r.AIsPlayer1 && r.Error == null).Select(r => r.ScoreA).DefaultIfEmpty(0.5).Average():P1}");
        Console.WriteLine($"평균 턴 {results.Average(r => r.Turns):F1}, 종료 사유: {string.Join(", ", results.GroupBy(r => r.EndReason).Select(g => $"{g.Key} {g.Count()}"))}");
        PrintTiming("A", a.Name, results.Select(r => r.TimingA));
        PrintTiming("B", b.Name, results.Select(r => r.TimingB));
        Console.WriteLine($"총 {watch.Elapsed.TotalSeconds:F1}초");
        return stats.Errors > 0 ? 2 : 0;
    }

    case "bench":
    {
        BotSpec player = BotSpec.Parse(positional.Count > 0 ? positional[0] : "easy");
        Bench.Run(player, Option("games", 6), Option("seed", 100), Option("samples", 40));
        return 0;
    }

    case "tune":
    {
        string engineArgs = options.TryGetValue("engine", out string value) ? value : "d=1,p=0";
        TunedParams start = options.TryGetValue("from", out string from) ? TunedParams.Load(from) : TunedParams.Default();
        string outPath = options.TryGetValue("out", out string path) ? path : "tuned.txt";
        Tuner.Run(engineArgs, start, Option("iterations", 200), Option("games", 24), Option("seed", 1), Option("threads", Environment.ProcessorCount), outPath);
        return 0;
    }

    case "inspect":
    {
        if (positional.Count != 2)
        {
            Console.WriteLine(Usage);
            return 1;
        }

        Inspect.Run(BotSpec.Parse(positional[0]), BotSpec.Parse(positional[1]), Option("seed", 1), Option("from", 1), Option("to", 9));
        return 0;
    }

    default:
        Console.WriteLine(Usage);
        return 1;
}

static void PrintTiming(string label, string name, IEnumerable<SideTiming> timings)
{
    List<SideTiming> list = timings.ToList();
    int turns = list.Sum(t => t.TurnCount);
    double avg = turns == 0 ? 0 : list.Sum(t => t.TotalMs) / turns;
    double max = list.Count == 0 ? 0 : list.Max(t => t.MaxTurnMs);
    Console.WriteLine($"{label}({name}) 턴당 판단 시간: 평균 {avg:F1} ms, 최대 {max:F1} ms");
}
