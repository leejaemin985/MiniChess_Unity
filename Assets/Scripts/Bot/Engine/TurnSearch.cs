using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiniChess.Bot.Eval;
using MiniChess.Bot.Rules;
using MiniChess.Bot.Search;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot.Engine
{
    /// <summary>턴 단위 탐색 설정.</summary>
    internal sealed class TurnSearchSettings
    {
        /// <summary>읽는 턴 수. 1 = 내 이번 턴만, 2 = + 상대 응수, 3 = + 그 뒤 내 턴 ...</summary>
        public int Depth = 2;

        /// <summary>내 턴마다 비교할 계획 수(턴 계획 생성기 상위).</summary>
        public int MyPlans = 4;

        /// <summary>상대 턴마다 비교할 상대 계획 수.</summary>
        public int ReplyPlans = 4;

        /// <summary>턴 종료에 무작위가 섞일 때(점령 보상 등) 뽑아 볼 결과 수. 1 이면 뽑지 않는다.</summary>
        public int ChanceSamples = 3;

        /// <summary>계획 생성 설정(빔 폭 등). PlansToKeep 은 위의 계획 수로 덮어쓴다.</summary>
        public PlannerSettings Planner = new PlannerSettings();

        /// <summary>내 루트 계획마다 하위 트리를 병렬로 읽는다(결과는 순차와 같다).</summary>
        public bool Parallel = true;

        /// <summary>깊이 1 에서도 이 수만큼 루트 계획을 만든다(실수로 차선 계획을 고를 후보). 깊이 2 이상은 MyPlans 와 큰 쪽.</summary>
        public int MinRootPlans = 1;
    }

    /// <summary>
    /// 턴 단위 미니맥스(알파베타). 한 턴 = 턴 계획 생성기(TurnPlanner)가 낸 상위 계획 하나.
    ///   - 내 턴: 내 계획 중 최대, 상대 턴: 상대 계획(상대 평가로 고른 상위) 중 나에게 최소.
    ///   - 상대 계획은 상대 시점 복사본(Fork(상대))에서 세운다(상대는 내 덫을 모른다). 세운 계획은 내 판에 다시 실행해, 덫에 걸리면 거기서 멈춘다.
    ///   - 턴 종료(다음 턴 시작 효과 포함)에 무작위가 섞이면 시드를 바꾼 복사본으로 ChanceSamples 번 뽑아 평균한다.
    ///     무작위인지는 메커닉을 보지 않고 시드 둘로 실행한 결과 판이 다른지로 안다.
    ///   - 잎: 턴을 끝낸 판을 내 기준으로 평가한다.
    /// 결과는 스레드 수와 무관하다(무작위 시드는 판 해시로 정하고, 예산은 계획 1회 단위).
    /// </summary>
    internal sealed class TurnSearch
    {
        private readonly TurnSearchSettings _settings;
        private readonly Evaluator _evaluator;
        private readonly BotWeights _weights;
        private readonly Func<bool> _stop;
        private readonly CancellationToken _cancellation;

        private Team _team;
        private bool _stalled;
        private double _caution;
        private long _nodes;
        private long _transpositions;
        private long _plannerCalls;
        private long _chanceTurns;

        public long LastNodes => _nodes;
        public long LastTranspositions => _transpositions;
        public long LastPlannerCalls => _plannerCalls;
        public long LastChanceTurns => _chanceTurns;
        /// <summary>깊이 2 이상에서 루트 계획마다 읽은 값(디버그용, 계획 순).</summary>
        public List<(TurnPlan Plan, double Value)> LastRootValues { get; private set; }

        public TurnSearch(TurnSearchSettings settings, Evaluator evaluator, BotWeights weights, Func<bool> stop, CancellationToken cancellation)
        {
            _settings = settings;
            _evaluator = evaluator;
            _weights = weights;
            _stop = stop;
            _cancellation = cancellation;
        }

        private bool Stopped => _cancellation.IsCancellationRequested || _stop();

        /// <param name="start">team 시점 복사본(Fork(team)). 바뀌지 않는다.</param>
        public TurnPlan Search(GameController start, Team team, bool stalled)
        {
            _team = team;
            _stalled = stalled;
            _nodes = _transpositions = _plannerCalls = _chanceTurns = 0;

            // 신중함은 탐색을 시작한 판의 형세로 고정한다(판마다 다시 정하면 점수를 비교할 수 없다).
            _caution = new BoardAnalysis(start, team, _weights, stalled).Caution;

            int rootCount = Math.Max(_settings.MinRootPlans, _settings.Depth <= 1 ? 1 : _settings.MyPlans);
            List<TurnPlan> roots = PlanTurn(start, team, rootCount, myView: true);
            LastRootValues = roots.Select(plan => (plan, plan.Score)).ToList();
            if (_settings.Depth <= 1 || roots.Count == 1)
                return roots[0];

            // 루트 계획마다 상대 응수부터 읽는다. 병렬일 때는 알파 공유 없이 각자 읽는다(값은 순차와 같고, 가지치기만 덜 한다).
            var values = new double[roots.Count];
            if (_settings.Parallel)
            {
                System.Threading.Tasks.Parallel.For(0, roots.Count, i =>
                    values[i] = AfterTurn(roots[i].Result, team, _settings.Depth - 1, double.NegativeInfinity, double.PositiveInfinity));
            }
            else
            {
                double alpha = double.NegativeInfinity;
                for (int i = 0; i < roots.Count; i++)
                {
                    values[i] = AfterTurn(roots[i].Result, team, _settings.Depth - 1, alpha, double.PositiveInfinity);
                    alpha = Math.Max(alpha, values[i]);
                }
            }

            // 시간이 다 돼 읽다 멈춘 하위 트리도 거기까지 본 판의 평가로 값이 나오므로 그대로 비교한다.
            // (순차 탐색에서 가지친 루트의 값은 상한이라 실제보다 높지 않다. 차선 선택(실수)은 깊이 1 에서만 쓴다.)
            LastRootValues = roots.Select((plan, i) => (plan, values[i])).ToList();
            int best = 0;
            for (int i = 1; i < roots.Count; i++)
            {
                if (values[i] > values[best])
                    best = i;
            }

            return roots[best];
        }

        /// <summary>mover 가 행동을 마친 판(턴 종료 전)에서 턴을 끝낸 뒤의 값(team 기준). 무작위면 뽑아서 평균.</summary>
        private double AfterTurn(GameController acted, Team mover, int depthLeft, double alpha, double beta)
        {
            List<GameController> outcomes = EndTurnOutcomes(acted, mover);
            double sum = 0;
            foreach (GameController next in outcomes)
            {
                // 무작위 결과가 여럿이면 평균이라 가지치기 창을 좁히지 않는다.
                sum += outcomes.Count == 1 ? Value(next, depthLeft, alpha, beta) : Value(next, depthLeft, double.NegativeInfinity, double.PositiveInfinity);
            }

            return sum / outcomes.Count;
        }

        /// <summary>턴이 막 바뀐 판의 값(team 기준). depthLeft 턴을 더 읽는다.</summary>
        private double Value(GameController state, int depthLeft, double alpha, double beta)
        {
            if (state.State.IsGameOver || depthLeft <= 0 || Stopped)
                return Evaluate(state);

            Team mover = state.State.CurrentTeam;
            bool mine = mover == _team;

            // 마지막 내 턴은 고를 것이 계획 점수 1위뿐이라(그 뒤는 잎 평가) 계획 하나만 만든다.
            List<TurnPlan> plans = mine
                ? PlanTurn(state, mover, depthLeft == 1 ? 1 : _settings.MyPlans, myView: true)
                : OpponentPlans(state, mover);

            double best = mine ? double.NegativeInfinity : double.PositiveInfinity;
            foreach (TurnPlan plan in plans)
            {
                double value = AfterTurn(plan.Result, mover, depthLeft - 1, alpha, beta);
                if (mine)
                {
                    best = Math.Max(best, value);
                    alpha = Math.Max(alpha, best);
                }
                else
                {
                    best = Math.Min(best, value);
                    beta = Math.Min(beta, best);
                }

                if (alpha >= beta)
                    break;
            }

            return best;
        }

        /// <summary>
        /// 상대 계획: 상대 시점 복사본에서 상대 평가로 세운 상위 계획을, 이 판(내가 아는 전부)에 다시 실행한 결과.
        /// 실행이 거부되면(내 덫에 걸려 속박 등) 거기까지만 한다.
        /// </summary>
        private List<TurnPlan> OpponentPlans(GameController state, Team opponent)
        {
            List<TurnPlan> plans = PlanTurn(state.Fork(opponent), opponent, _settings.ReplyPlans, myView: false);
            var replayed = new List<TurnPlan>(plans.Count);
            foreach (TurnPlan plan in plans)
            {
                GameController copy = state.Fork();
                foreach (BotAction action in plan.Actions)
                {
                    if (copy.State.IsGameOver || copy.State.CurrentTeam != opponent || !action.Execute(copy))
                        break;
                }

                replayed.Add(new TurnPlan { Actions = plan.Actions, Score = plan.Score, Result = copy });
            }

            return replayed;
        }

        /// <summary>mover 의 이번 턴 상위 계획. 내 계획은 내 평가(고정 신중함), 상대 계획은 상대 평가(그 판의 신중함)로 고른다.</summary>
        private List<TurnPlan> PlanTurn(GameController start, Team mover, int count, bool myView)
        {
            Interlocked.Increment(ref _plannerCalls);
            bool stalled = myView && _stalled;
            double caution = myView ? _caution : new BoardAnalysis(start, mover, _weights).Caution;

            double ScoreAfterTurn(GameController line)
            {
                return _evaluator.Evaluate(EndTurnOnce(line, mover), mover, _weights, stalled, caution);
            }

            double QuickScore(GameController line)
            {
                return _evaluator.Evaluate(line, mover, _weights, stalled, caution);
            }

            // 정밀 채점 수: 음수면 자동(빔 폭의 3배 + 돌려줄 계획 후보 수), 0 이면 모두.
            int fullScore = _settings.Planner.FullScoreCount;
            if (fullScore < 0)
                fullScore = _settings.Planner.BeamWidth * 3 + (count <= 1 ? 1 : count * 4);

            var planner = new TurnPlanner(new PlannerSettings
            {
                BeamWidth = _settings.Planner.BeamWidth,
                MaxActions = _settings.Planner.MaxActions,
                MaxPerActionKey = _settings.Planner.MaxPerActionKey,
                NodeBudget = _settings.Planner.NodeBudget,
                PlansToKeep = count,
                FullScoreCount = fullScore,
            });
            List<TurnPlan> plans = planner.Plan(start, mover, ScoreAfterTurn, _stop, _cancellation, QuickScore);
            Interlocked.Add(ref _nodes, planner.LastNodes);
            Interlocked.Add(ref _transpositions, planner.LastTranspositions);
            return plans;
        }

        /// <summary>잎 평가: 턴을 끝낸(또는 막 바뀐) 판을 team 기준으로.</summary>
        private double Evaluate(GameController state)
        {
            return _evaluator.Evaluate(state, _team, _weights, _stalled, _caution);
        }

        /// <summary>
        /// 턴 종료의 결과 판들. 시드 둘로 실행해 결과 판이 같으면 무작위가 없다고 보고 하나만,
        /// 다르면 ChanceSamples 개를 뽑는다. 시드는 판 해시로 정해 스레드 순서와 무관하게 같다.
        /// </summary>
        private List<GameController> EndTurnOutcomes(GameController acted, Team mover)
        {
            if (acted.State.IsGameOver || acted.State.CurrentTeam != mover)
                return new List<GameController> { acted };

            int samples = Math.Max(1, _settings.ChanceSamples);
            ulong hash = StateHasher.Hash(acted, mover);
            GameController first = EndTurnWithSeed(acted, mover, SeedFor(hash, 0));
            if (samples == 1)
                return new List<GameController> { first };

            GameController second = EndTurnWithSeed(acted, mover, SeedFor(hash, 1));
            Team viewer = _team;
            if (StateHasher.Hash(first, viewer) == StateHasher.Hash(second, viewer))
                return new List<GameController> { first };

            Interlocked.Increment(ref _chanceTurns);
            var outcomes = new List<GameController> { first, second };
            for (int i = 2; i < samples; i++)
                outcomes.Add(EndTurnWithSeed(acted, mover, SeedFor(hash, i)));
            return outcomes;
        }

        private static GameController EndTurnWithSeed(GameController acted, Team mover, int seed)
        {
            GameController copy = acted.Fork(randomSeed: seed);
            copy.EndTurn(new EndTurnRequest(mover));
            return copy;
        }

        /// <summary>계획 생성 중 점수용 턴 종료(무작위는 기본 흐름 하나만 본다).</summary>
        private static GameController EndTurnOnce(GameController line, Team mover)
        {
            if (line.State.IsGameOver || line.State.CurrentTeam != mover)
                return line;

            GameController ended = line.Fork();
            ended.EndTurn(new EndTurnRequest(mover));
            return ended;
        }

        private static int SeedFor(ulong hash, int sample)
        {
            ulong mixed = (hash + (ulong)sample * 0x9E3779B97F4A7C15UL) * 0xBF58476D1CE4E5B9UL;
            return (int)(mixed >> 33);
        }
    }
}
