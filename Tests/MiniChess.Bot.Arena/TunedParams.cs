using System.Globalization;
using System.Reflection;
using MiniChess.Bot.Eval;
using MiniChess.Bot.Rules;

namespace MiniChess.Bot.Arena
{
    /// <summary>
    /// 튜닝 대상 값 묶음: "w.이름" = BotWeights 의 실수 속성, "s.이름" = 판 평가 항목 배수(EvalScales).
    /// 파일은 한 줄에 "이름=값" 이다(# 으로 시작하면 주석).
    /// </summary>
    public sealed class TunedParams
    {
        public readonly SortedDictionary<string, double> Values = new SortedDictionary<string, double>(StringComparer.Ordinal);

        /// <summary>엔진 판 평가에 쓰이는 BotWeights 값(규칙 봇 행동 점수 전용 값은 뺀다).</summary>
        public static readonly string[] WeightNames =
        {
            nameof(BotWeights.ApCost), nameof(BotWeights.ShieldDamageRatio), nameof(BotWeights.KillBonus), nameof(BotWeights.KillThreatFactor),
            nameof(BotWeights.SummonKillBonus), nameof(BotWeights.Approach), nameof(BotWeights.Danger), nameof(BotWeights.CautionEven),
            nameof(BotWeights.CautionBehind), nameof(BotWeights.CautionStalled), nameof(BotWeights.DeathRisk), nameof(BotWeights.Capture),
            nameof(BotWeights.CaptureDenial), nameof(BotWeights.StrikeHitChance), nameof(BotWeights.StrikeHitChanceImmobile),
            nameof(BotWeights.DamageOverTime),
        };

        /// <summary>기본값(BotWeights 기본, 항목 배수 1).</summary>
        public static TunedParams Default()
        {
            var p = new TunedParams();
            var weights = new BotWeights();
            foreach (string name in WeightNames)
                p.Values["w." + name] = (double)Property(name).GetValue(weights);
            foreach (IEvalTerm term in Evaluator.CreateDefault().Terms)
                p.Values["s." + term.Name] = 1;
            return p;
        }

        public TunedParams Clone()
        {
            var copy = new TunedParams();
            foreach (KeyValuePair<string, double> kv in Values)
                copy.Values[kv.Key] = kv.Value;
            return copy;
        }

        public BotWeights ToWeights()
        {
            var weights = new BotWeights();
            foreach (KeyValuePair<string, double> kv in Values.Where(kv => kv.Key.StartsWith("w.")))
                Property(kv.Key.Substring(2)).SetValue(weights, kv.Value);
            return weights;
        }

        internal EvalScales ToScales()
        {
            var scales = new EvalScales();
            foreach (KeyValuePair<string, double> kv in Values.Where(kv => kv.Key.StartsWith("s.")))
                scales.Set(kv.Key.Substring(2), kv.Value);
            return scales;
        }

        public static TunedParams Load(string path)
        {
            TunedParams p = Default();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                string[] kv = line.Split('=');
                if (!p.Values.ContainsKey(kv[0].Trim()))
                    throw new ArgumentException($"알 수 없는 튜닝 값: {kv[0]} ({path})");
                p.Values[kv[0].Trim()] = double.Parse(kv[1].Trim(), CultureInfo.InvariantCulture);
            }

            return p;
        }

        public void Save(string path, string header)
        {
            var lines = new List<string> { "# " + header };
            lines.AddRange(Values.Select(kv => $"{kv.Key}={kv.Value.ToString("G6", CultureInfo.InvariantCulture)}"));
            File.WriteAllLines(path, lines);
        }

        public override string ToString() => string.Join(", ", Values.Select(kv => $"{kv.Key}={kv.Value:G3}"));

        private static PropertyInfo Property(string name)
        {
            return typeof(BotWeights).GetProperty(name) ?? throw new ArgumentException($"BotWeights 에 없는 값: {name}");
        }
    }
}
