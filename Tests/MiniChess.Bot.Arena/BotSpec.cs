using System.Globalization;
using MiniChess.Bot.Search;

namespace MiniChess.Bot.Arena
{
    /// <summary>
    /// 명령줄의 봇 지정 문자열을 봇 생성기로 바꾼다. 경기마다 새 봇을 만든다(봇은 턴 계획 등 상태를 가진다).
    ///   random | easy | normal | hard | rule
    ///   search:d=2,b=6,c=8,r=6,v=2,a=10,t=0   (Depth, BeamWidth, CandidatesPerStep, ReplyCandidates, MaxVariantsPerSkill, MaxActions, TimeLimitMs)
    ///   engine:d=2,m=4,r=4,s=3,b=4,a=10,k=2,n=0,t=0,p=1,f=-1
    ///     (Depth, MyPlans, ReplyPlans, ChanceSamples, BeamWidth, MaxActions, MaxPerActionKey, NodeBudget, TimeLimitMs, Parallel, FullScoreCount)
    ///   level:1~10                           (BotLevels 표, 시간 상한 없이)
    ///   engine 과 level 은 끝에 @파일 을 붙이면 그 튜닝 값(TunedParams)을 쓴다.
    /// </summary>
    public sealed class BotSpec
    {
        public string Name { get; }
        private readonly Func<int, BotPlayer> _create;

        private BotSpec(string name, Func<int, BotPlayer> create)
        {
            Name = name;
            _create = create;
        }

        public BotPlayer Create(int seed) => _create(seed);

        public static BotSpec Parse(string text)
        {
            string spec = text.Trim().ToLowerInvariant();
            int colon = spec.IndexOf(':');
            string kind = colon < 0 ? spec : spec.Substring(0, colon);
            string args = colon < 0 ? "" : spec.Substring(colon + 1);

            switch (kind)
            {
                case "random":
                    return new BotSpec(spec, seed => new RandomBot(seed));
                case "easy":
                    return new BotSpec(spec, seed => BotFactory.Create(BotDifficulty.Easy, seed));
                case "normal":
                    return new BotSpec(spec, seed => BotFactory.Create(BotDifficulty.Normal, seed));
                case "hard":
                    return new BotSpec(spec, seed => BotFactory.Create(BotDifficulty.Hard, seed));
                case "rule":
                    return new BotSpec(spec, seed => new Rules.RuleBot(seed));
                case "search":
                {
                    Dictionary<string, int> values = ParseArgs(args);
                    return new BotSpec(spec, seed => new SearchBot(seed, ToSettings(values)));
                }
                case "engine":
                {
                    // engine:d=3,m=4@path/to/tuned.txt  — @ 뒤는 튜닝 값 파일(TunedParams).
                    int at = text.IndexOf('@');
                    string engineArgs = at < 0 ? args : args.Substring(0, args.IndexOf('@'));
                    TunedParams tuned = at < 0 ? null : TunedParams.Load(text.Substring(at + 1).Trim());
                    return ForEngine(engineArgs, tuned, spec);
                }
                case "level":
                {
                    // level:7 또는 level:7@tuned.txt. 대전 결과가 매번 같도록 시간 상한은 끈다(동시 경기로 느려져도 실력이 같게).
                    int at = text.IndexOf('@');
                    int level = int.Parse(at < 0 ? args : args.Substring(0, args.IndexOf('@')), CultureInfo.InvariantCulture);
                    TunedParams tuned = at < 0 ? null : TunedParams.Load(text.Substring(at + 1).Trim());
                    Engine.EngineSettings settings = Engine.BotLevels.Settings(level);
                    settings.TimeLimitMs = 0;
                    settings.Parallel = false;
                    return tuned == null
                        ? new BotSpec(spec, seed => new Engine.EngineBot(seed, settings))
                        : new BotSpec(spec, seed => new Engine.EngineBot(seed, settings, tuned.ToWeights(), tuned.ToScales()));
                }
                default:
                    throw new ArgumentException($"알 수 없는 봇: {text}");
            }
        }

        /// <summary>엔진 봇. tuned 가 있으면 그 판 평가 값으로(튜너가 흔든 값을 넣을 때도 쓴다).</summary>
        public static BotSpec ForEngine(string engineArgs, TunedParams tuned, string name)
        {
            Engine.EngineSettings settings = ToEngineSettings(ParseArgs(engineArgs));
            if (tuned == null)
                return new BotSpec(name, seed => new Engine.EngineBot(seed, settings));

            return new BotSpec(name, seed => new Engine.EngineBot(seed, settings, tuned.ToWeights(), tuned.ToScales()));
        }

        private static Dictionary<string, int> ParseArgs(string args)
        {
            var values = new Dictionary<string, int>();
            foreach (string pair in args.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = pair.Split('=');
                if (kv.Length != 2)
                    throw new ArgumentException($"잘못된 봇 인자: {pair}");
                values[kv[0].Trim()] = int.Parse(kv[1].Trim(), CultureInfo.InvariantCulture);
            }

            return values;
        }

        private static SearchSettings ToSettings(Dictionary<string, int> values)
        {
            var settings = new SearchSettings();
            foreach (KeyValuePair<string, int> kv in values)
            {
                switch (kv.Key)
                {
                    case "d": settings.Depth = kv.Value; break;
                    case "b": settings.BeamWidth = kv.Value; break;
                    case "c": settings.CandidatesPerStep = kv.Value; break;
                    case "r": settings.ReplyCandidates = kv.Value; break;
                    case "v": settings.MaxVariantsPerSkill = kv.Value; break;
                    case "a": settings.MaxActions = kv.Value; break;
                    case "t": settings.TimeLimitMs = kv.Value; break;
                    default: throw new ArgumentException($"알 수 없는 탐색 인자: {kv.Key}");
                }
            }

            return settings;
        }

        private static Engine.EngineSettings ToEngineSettings(Dictionary<string, int> values)
        {
            var settings = new Engine.EngineSettings();
            foreach (KeyValuePair<string, int> kv in values)
            {
                switch (kv.Key)
                {
                    case "d": settings.Depth = kv.Value; break;
                    case "m": settings.MyPlans = kv.Value; break;
                    case "r": settings.ReplyPlans = kv.Value; break;
                    case "s": settings.ChanceSamples = kv.Value; break;
                    case "b": settings.BeamWidth = kv.Value; break;
                    case "a": settings.MaxActions = kv.Value; break;
                    case "k": settings.MaxPerActionKey = kv.Value; break;
                    case "n": settings.NodeBudget = kv.Value; break;
                    case "t": settings.TimeLimitMs = kv.Value; break;
                    case "p": settings.Parallel = kv.Value != 0; break;
                    case "f": settings.FullScoreCount = kv.Value; break;
                    default: throw new ArgumentException($"알 수 없는 엔진 인자: {kv.Key}");
                }
            }

            return settings;
        }
    }
}
