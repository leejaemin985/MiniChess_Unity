using System.Collections.Generic;
using MiniChess.Bot.Eval.Terms;
using MiniChess.Bot.Rules;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.State;

namespace MiniChess.Bot.Eval
{
    /// <summary>
    /// 판 하나의 점수(team 기준, 높을수록 좋다). 탐색 봇이 복사한 경기에서 행동을 실행해 본 뒤의 판을 비교하는 데 쓴다.
    /// 점수 = 승패가 났으면 ±WinScore, 아니면 항목(IEvalTerm) 점수 × 항목 배수의 합.
    /// 항목 안의 세부 가중치는 BotWeights(규칙 봇과 같은 값), 항목 전체의 비중은 EvalScales 로 조정한다.
    /// </summary>
    internal sealed class Evaluator
    {
        /// <summary>승패가 난 판의 점수(어떤 판 점수보다 크게).</summary>
        public const double WinScore = 100000;

        private readonly IEvalTerm[] _terms;
        private readonly double[] _scales;

        public IReadOnlyList<IEvalTerm> Terms => _terms;

        public Evaluator(IReadOnlyList<IEvalTerm> terms, EvalScales scales = null)
        {
            _terms = new IEvalTerm[terms.Count];
            _scales = new double[terms.Count];
            for (int i = 0; i < terms.Count; i++)
            {
                _terms[i] = terms[i];
                _scales[i] = scales?.Get(terms[i].Name) ?? 1;
            }
        }

        /// <summary>기본 항목 목록.</summary>
        public static Evaluator CreateDefault(EvalScales scales = null)
        {
            return new Evaluator(new IEvalTerm[]
            {
                new UnitWorthTerm(),
                new ExposureTerm(),
                new ApproachTerm(),
                new AfflictionsTerm(),
                new ChargeTerm(),
                new PendingStrikesTerm(),
                new CaptureTerm(),
                new CarriedApTerm(),
            }, scales);
        }

        /// <param name="caution">위협도 감점 배수. 탐색 봇은 탐색을 시작한 판의 값으로 고정해 넘긴다(판마다 바뀌면 비교가 어긋난다).</param>
        public double Evaluate(GameController game, Team team, BotWeights weights, bool stalled, double? caution = null)
        {
            if (game.State.IsGameOver)
                return TerminalScore(game.State, team);

            var a = new BoardAnalysis(game, team, weights, stalled, caution);
            double value = 0;
            for (int i = 0; i < _terms.Length; i++)
                value += _scales[i] * _terms[i].Evaluate(a);
            return value;
        }

        /// <summary>항목별 점수(배수 적용 후). 합은 Evaluate 와 같다. 승패가 난 판은 "Terminal" 하나만.</summary>
        public List<(string Name, double Score)> Explain(GameController game, Team team, BotWeights weights, bool stalled, double? caution = null)
        {
            var parts = new List<(string Name, double Score)>();
            if (game.State.IsGameOver)
            {
                parts.Add(("Terminal", TerminalScore(game.State, team)));
                return parts;
            }

            var a = new BoardAnalysis(game, team, weights, stalled, caution);
            for (int i = 0; i < _terms.Length; i++)
                parts.Add((_terms[i].Name, _scales[i] * _terms[i].Evaluate(a)));
            return parts;
        }

        /// <summary>빨리 이길수록, 늦게 질수록 좋다.</summary>
        private static double TerminalScore(GameState state, Team team)
        {
            return state.Winner == team ? WinScore - state.TurnNumber : state.Winner.HasValue ? -WinScore + state.TurnNumber : 0;
        }
    }

    /// <summary>항목 이름별 배수(없으면 1). 봇끼리 대전으로 조정하는 값이다.</summary>
    internal sealed class EvalScales
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>();

        public double? Get(string name) => _values.TryGetValue(name, out double value) ? value : (double?)null;

        public EvalScales Set(string name, double value)
        {
            _values[name] = value;
            return this;
        }
    }
}
