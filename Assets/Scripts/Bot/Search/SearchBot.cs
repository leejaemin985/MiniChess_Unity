using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MiniChess.Bot.Rules;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot.Search
{
    /// <summary>
    /// 수 읽기 봇. 턴을 시작할 때 경기를 복사(GameController.Fork, 자기 팀 시점이라 숨겨진 정보는 모른다)해
    /// 행동 순서를 실제로 실행해 보고, 턴을 끝낸 판(깊이 2 면 상대 응수 뒤의 판)의 점수(PositionEvaluator)가 가장 좋은 순서를 고른다.
    ///   - 후보: 규칙 봇 점수(ActionScorer)가 높은 행동 CandidatesPerStep 개.
    ///   - 넓이: 단계마다 판 점수가 높은 순서 BeamWidth 개만 이어서 읽는다(빔 탐색).
    ///   - 깊이: 1 = 이번 턴, 2 = + 상대 응수, 3 = + 그 뒤 내 다음 턴(SearchSettings.Depth).
    /// 고른 순서를 한 행동씩 내며, 다음 행동을 지금 할 수 없으면(예상과 다른 결과) 그 자리에서 다시 읽는다.
    /// 상태는 이 봇 객체와 복사한 경기만 쓰므로, 실제 경기가 바뀌지 않는 동안이면 다른 스레드에서 불러도 된다.
    /// </summary>
    public class SearchBot : BotPlayer
    {
        private readonly Random _random;
        private readonly StallTracker _stall = new StallTracker();
        private readonly List<BotAction> _plan = new List<BotAction>();
        private int _planTurn = -1;

        public SearchSettings Settings { get; }
        public BotWeights Weights { get; }

        /// <summary>배치할 캐릭터 Id. 비우면 역할(전투 2, 수호 1, 제어 1)에 맞춰 무작위로 고른다.</summary>
        public IReadOnlyList<string> Lineup { get; set; }

        /// <summary>마지막으로 읽은 결과: 고른 순서의 판 점수와 실행해 본 판 수. 디버그/로그용.</summary>
        public double LastPlanScore { get; private set; }
        public int LastNodesSearched { get; private set; }
        public double LastSearchMilliseconds { get; private set; }

        public SearchBot(int seed, SearchSettings settings = null, BotWeights weights = null)
        {
            _random = new Random(seed);
            Settings = settings ?? new SearchSettings();
            Weights = weights ?? new BotWeights();
        }

        public override IReadOnlyList<UnitPlacement> DecidePlacement(GameController game, Team team)
        {
            return BotPlacement.ByRole(game, team, _random, Lineup);
        }

        protected override BotAction Choose(GameController game, Team team)
        {
            _stall.Update(game, team);

            if (_planTurn != game.State.TurnNumber)
            {
                _planTurn = game.State.TurnNumber;
                Replan(game, team);
            }
            else if (_plan.Count > 0 && !IsAvailable(game, team, _plan[0]))
            {
                Replan(game, team);
            }

            if (_plan.Count == 0)
                return null;

            BotAction next = _plan[0];
            _plan.RemoveAt(0);
            return next;
        }

        private static bool IsAvailable(GameController game, Team team, BotAction action)
        {
            return BotActionGenerator.Generate(game, team).Any(candidate => candidate.SameAs(action));
        }

        private void Replan(GameController game, Team team)
        {
            _plan.Clear();
            _plan.AddRange(Search(game, team));
        }

        /// <summary>읽은 행동 순서 하나(복사한 경기에서 그 순서까지 실행한 판).</summary>
        private class Line
        {
            public GameController Game;
            public List<BotAction> Actions;
            public double Score;
        }

        private IReadOnlyList<BotAction> Search(GameController game, Team team)
        {
            bool stalled = _stall.IsStalled(Weights);
            _nodes = 0;
            _deadline = Stopwatch.StartNew();

            List<Line> finished = BeamSearch(game.Fork(team), team, stalled);
            Line best = finished[0];
            LastPlanScore = best.Score;

            // 깊이 2 이상: 상위 순서마다 상대 응수(깊이 3 이면 그 뒤 내 다음 턴까지)를 실행해 본 점수로 다시 고른다.
            // 시간이 다 되면 거기까지 본 것 중에서 고른다(하나도 못 봤으면 깊이 1 결과).
            if (Settings.Depth >= 2)
            {
                double bestReply = double.NegativeInfinity;
                foreach (Line line in finished.Take(Settings.ReplyCandidates))
                {
                    if (bestReply > double.NegativeInfinity && TimeUp)
                        break;

                    double score = ScoreAfterReply(line.Game, team, stalled);
                    if (score > bestReply)
                    {
                        bestReply = score;
                        best = line;
                    }
                }

                LastPlanScore = bestReply;
            }

            LastNodesSearched = _nodes;
            LastSearchMilliseconds = _deadline.Elapsed.TotalMilliseconds;
            return best.Actions;
        }

        private int _nodes;
        private Stopwatch _deadline;

        private bool TimeUp => Settings.TimeLimitMs > 0 && _deadline.ElapsedMilliseconds >= Settings.TimeLimitMs;

        /// <summary>
        /// start 에서 team 의 이번 턴 행동 순서를 빔 탐색으로 읽는다(깊이 1). 읽은 모든 순서(아무것도 안 하는 순서 포함)를
        /// 턴을 끝낸 판 점수가 높은 순으로 돌려준다. 시간이 다 되면 그때까지 읽은 것만.
        /// </summary>
        private List<Line> BeamSearch(GameController start, Team team, bool stalled)
        {
            var root = new Line { Game = start, Actions = new List<BotAction>() };
            root.Score = ScoreEndingTurn(root.Game, team, stalled);
            var finished = new List<Line> { root };
            var beam = new List<Line> { root };

            for (int step = 0; step < Settings.MaxActions && beam.Count > 0 && !TimeUp; step++)
            {
                var next = new List<Line>();
                foreach (Line line in beam)
                {
                    if (line.Game.State.IsGameOver || line.Game.State.CurrentTeam != team)
                        continue;

                    foreach (BotAction action in Candidates(line.Game, team, stalled))
                    {
                        GameController child = line.Game.Fork();
                        if (!action.Execute(child))
                            continue;

                        _nodes++;
                        var extended = new Line { Game = child, Actions = new List<BotAction>(line.Actions) { action } };
                        extended.Score = ScoreEndingTurn(child, team, stalled);
                        next.Add(extended);
                    }
                }

                beam = next.OrderByDescending(line => line.Score).Take(Settings.BeamWidth).ToList();
                finished.AddRange(beam);
            }

            return finished.OrderByDescending(line => line.Score).ToList();
        }

        /// <summary>규칙 봇 점수가 높은 후보 CandidatesPerStep 개.</summary>
        private IEnumerable<BotAction> Candidates(GameController game, Team team, bool stalled)
        {
            var scorer = new ActionScorer(new BoardAnalysis(game, team, Weights, stalled));
            return BotActionGenerator.Generate(game, team)
                .Select(action => (Action: action, Score: scorer.Score(action)))
                .Where(pair => pair.Score > Settings.MinCandidateScore)
                .OrderByDescending(pair => pair.Score)
                .Take(Settings.CandidatesPerStep)
                .Select(pair => pair.Action);
        }

        /// <summary>지금 턴을 끝냈을 때의 판 점수(턴 종료/상대 턴 시작 효과까지 실행해 본다).</summary>
        private double ScoreEndingTurn(GameController game, Team team, bool stalled)
        {
            GameController ended = EndTurn(game, team);
            return PositionEvaluator.Evaluate(ended, team, Weights, stalled);
        }

        /// <summary>
        /// 턴을 끝낸 뒤 상대가 규칙 봇처럼 한 턴을 두었을 때의 판 점수.
        /// 깊이 3 이면 그 뒤 내 다음 턴을 깊이 1 로 읽어 가장 좋은 순서의 점수(턴을 끝낸 판)를 쓴다.
        /// </summary>
        private double ScoreAfterReply(GameController game, Team team, bool stalled)
        {
            _nodes++;
            GameController reply = EndTurn(game, team);
            if (!reply.State.IsGameOver)
                BotMatch.PlayTurn(reply, new RuleBot(_random.Next(), Weights));

            if (Settings.Depth >= 3 && !reply.State.IsGameOver && reply.State.CurrentTeam == team)
                return BeamSearch(reply, team, stalled)[0].Score;

            return PositionEvaluator.Evaluate(reply, team, Weights, stalled);
        }

        private static GameController EndTurn(GameController game, Team team)
        {
            GameController copy = game.Fork();
            if (!copy.State.IsGameOver && copy.State.CurrentTeam == team)
                copy.EndTurn(new EndTurnRequest(team));
            return copy;
        }
    }
}
