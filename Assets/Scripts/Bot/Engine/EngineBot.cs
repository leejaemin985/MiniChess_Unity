using System;
using System.Collections.Generic;
using System.Diagnostics;
using MiniChess.Bot.Eval;
using MiniChess.Bot.Rules;
using MiniChess.Bot.Search;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot.Engine
{
    /// <summary>새 엔진 봇의 설정. 레벨(1~10)이 이 값을 정한다(레벨 표는 이후 단계에서).</summary>
    public sealed class EngineSettings
    {
        /// <summary>턴 계획: 단계마다 이어서 읽을 행동 순서 수.</summary>
        public int BeamWidth { get; set; } = 8;

        /// <summary>턴 계획: 한 턴에 읽는 행동 수 상한.</summary>
        public int MaxActions { get; set; } = 10;

        /// <summary>턴 계획: 빔 안에서 마지막 행동 종류(유닛, 행동, 스킬)가 같은 순서의 수 상한.</summary>
        public int MaxPerActionKey { get; set; } = 2;

        /// <summary>한 번 계획할 때 실행해 볼 판 수 상한(기기 성능과 무관한 실력 기준). 0 이면 제한 없음.</summary>
        public int NodeBudget { get; set; }

        /// <summary>한 번 계획할 때의 시간 상한(ms, 안전장치). 0 이면 제한 없음(테스트/레벨 측정은 결과가 매번 같도록 0).</summary>
        public int TimeLimitMs { get; set; }
    }

    /// <summary>
    /// 새 엔진 봇. 턴을 시작할 때 자기 팀 시점 복사본에서 이번 턴의 행동 순서를 계획(TurnPlanner)하고 한 행동씩 낸다.
    /// 행동마다 실제 판이 계획에서 예상한 판(해시)과 다르면(무작위 결과, 숨겨진 덫 등) 그 자리에서 다시 계획한다.
    /// </summary>
    public class EngineBot : BotPlayer
    {
        private readonly Random _random;
        private readonly StallTracker _stall = new StallTracker();
        private readonly Evaluator _evaluator = Evaluator.CreateDefault();
        private readonly TurnPlanner _planner;

        private TurnPlan _plan;
        private int _cursor;
        private int _planTurn = -1;

        public EngineSettings Settings { get; }
        public BotWeights Weights { get; }

        /// <summary>배치할 캐릭터 Id. 비우면 역할(전투 2, 수호 1, 제어 1)에 맞춰 무작위로 고른다.</summary>
        public IReadOnlyList<string> Lineup { get; set; }

        /// <summary>디버그/측정용: 마지막 계획의 점수, 읽은 판 수, 해시로 건너뛴 판 수, 걸린 시간. 누적 재계획 수(예상과 다른 판).</summary>
        public double LastPlanScore { get; private set; }
        public int LastNodesSearched { get; private set; }
        public int LastTranspositions { get; private set; }
        public double LastSearchMilliseconds { get; private set; }
        public int Replans { get; private set; }

        public EngineBot(int seed, EngineSettings settings = null, BotWeights weights = null)
        {
            _random = new Random(seed);
            Settings = settings ?? new EngineSettings();
            Weights = weights ?? new BotWeights();
            _planner = new TurnPlanner(new PlannerSettings
            {
                BeamWidth = Settings.BeamWidth,
                MaxActions = Settings.MaxActions,
                MaxPerActionKey = Settings.MaxPerActionKey,
                NodeBudget = Settings.NodeBudget,
            });
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
            else if (_cursor > 0 && StateHasher.Hash(game, team) != _plan.ExpectedHashes[_cursor - 1])
            {
                Replans++;
                Replan(game, team);
            }

            if (_cursor >= _plan.Actions.Count)
                return null;

            return _plan.Actions[_cursor++];
        }

        private void Replan(GameController game, Team team)
        {
            var watch = Stopwatch.StartNew();
            bool stalled = _stall.IsStalled(Weights);
            GameController start = game.Fork(team);

            // 신중함(위협도 감점 배수)은 계획을 시작한 판의 형세로 고정한다(판마다 다시 정하면 점수를 비교할 수 없다).
            double caution = new BoardAnalysis(start, team, Weights, stalled).Caution;

            double ScoreAfterTurn(GameController line)
            {
                GameController ended = line;
                if (!line.State.IsGameOver && line.State.CurrentTeam == team)
                {
                    ended = line.Fork();
                    ended.EndTurn(new EndTurnRequest(team));
                }

                return _evaluator.Evaluate(ended, team, Weights, stalled, caution);
            }

            bool TimeUp() => Settings.TimeLimitMs > 0 && watch.ElapsedMilliseconds >= Settings.TimeLimitMs;

            _plan = _planner.Plan(start, team, ScoreAfterTurn, TimeUp, Cancellation)[0];
            _cursor = 0;

            LastPlanScore = _plan.Score;
            LastNodesSearched = _planner.LastNodes;
            LastTranspositions = _planner.LastTranspositions;
            LastSearchMilliseconds = watch.Elapsed.TotalMilliseconds;
        }
    }
}
