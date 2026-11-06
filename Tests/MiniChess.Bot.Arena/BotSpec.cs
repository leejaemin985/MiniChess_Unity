using System.Globalization;
using MiniChess.Bot.Search;

namespace MiniChess.Bot.Arena
{
    /// <summary>
    /// 명령줄의 봇 지정 문자열을 봇 생성기로 바꾼다. 경기마다 새 봇을 만든다(봇은 턴 계획 등 상태를 가진다).
    ///   random | easy | normal | hard | rule
    ///   search:d=2,b=6,c=8,r=6,v=2,a=10,t=0   (Depth, BeamWidth, CandidatesPerStep, ReplyCandidates, MaxVariantsPerSkill, MaxActions, TimeLimitMs)
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
                default:
                    throw new ArgumentException($"알 수 없는 봇: {text}");
            }
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
    }
}
