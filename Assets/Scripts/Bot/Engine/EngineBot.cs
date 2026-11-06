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
        /// <summary>읽는 턴 수. 1 = 내 이번 턴만, 2 = + 상대 응수, 3 = + 그 뒤 내 턴.</summary>
        public int Depth { get; set; } = 1;

        /// <summary>깊이 2 이상: 내 턴마다 비교할 계획 수.</summary>
        public int MyPlans { get; set; } = 4;

        /// <summary>깊이 2 이상: 상대 턴마다 비교할 상대 계획 수.</summary>
        public int ReplyPlans { get; set; } = 4;

        /// <summary>턴 종료에 무작위가 섞일 때 뽑아 볼 결과 수. 1 이면 기본 흐름 하나만 본다.</summary>
        public int ChanceSamples { get; set; } = 3;

        /// <summary>턴 계획: 단계마다 이어서 읽을 행동 순서 수.</summary>
        public int BeamWidth { get; set; } = 4;

        /// <summary>턴 계획: 한 턴에 읽는 행동 수 상한.</summary>
        public int MaxActions { get; set; } = 10;

        /// <summary>턴 계획: 빔 안에서 마지막 행동 종류(유닛, 행동, 스킬)가 같은 순서의 수 상한.</summary>
        public int MaxPerActionKey { get; set; } = 2;

        /// <summary>턴 계획 한 번에 실행해 볼 판 수 상한(기기 성능과 무관한 실력 기준). 0 이면 제한 없음.</summary>
        public int NodeBudget { get; set; }

        /// <summary>한 번 판단할 때의 시간 상한(ms, 안전장치). 0 이면 제한 없음(테스트/레벨 측정은 결과가 매번 같도록 0).</summary>
        public int TimeLimitMs { get; set; }

        /// <summary>루트 계획마다 하위 트리를 병렬로 읽는다(결과는 순차와 같다).</summary>
        public bool Parallel { get; set; } = true;

        /// <summary>낮은 레벨용: 이 확률로 최선 대신 2~MistakeRange 위 계획 중 하나를 고른다(깊이 1 에서만). 0 이면 실수 없음.</summary>
        public double MistakeChance { get; set; }

        /// <summary>실수할 때 고를 수 있는 계획 순위 범위(2 이상).</summary>
        public int MistakeRange { get; set; } = 3;
    }

    /// <summary>
    /// 새 엔진 봇. 턴을 시작할 때 자기 팀 시점 복사본에서 턴 단위 탐색(TurnSearch)으로 이번 턴 계획을 고르고 한 행동씩 낸다.
    /// 행동마다 실제 판이 계획에서 예상한 판(해시)과 다르면(무작위 결과, 숨겨진 덫 등) 그 자리에서 다시 읽는다.
    /// </summary>
    public class EngineBot : BotPlayer
    {
        private readonly Random _random;
        private readonly StallTracker _stall = new StallTracker();
        private readonly Evaluator _evaluator;

        private TurnPlan _plan;
        private int _cursor;
        private int _planTurn = -1;

        public EngineSettings Settings { get; }
        public BotWeights Weights { get; }

        /// <summary>배치할 캐릭터 Id. 비우면 역할(전투 2, 수호 1, 제어 1)에 맞춰 무작위로 고른다.</summary>
        public IReadOnlyList<string> Lineup { get; set; }

        /// <summary>디버그/측정용: 마지막 판단의 점수, 읽은 판 수, 해시로 건너뛴 판 수, 계획 생성 횟수, 걸린 시간. 누적 재계획 수(예상과 다른 판).</summary>
        public double LastPlanScore { get; private set; }
        public long LastNodesSearched { get; private set; }
        public long LastTranspositions { get; private set; }
        public long LastPlannerCalls { get; private set; }
        public double LastSearchMilliseconds { get; private set; }
        public int Replans { get; private set; }

        /// <summary>디버그용: 마지막 판단의 루트 계획과 탐색 값(깊이 1 이면 계획 점수).</summary>
        internal List<(TurnPlan Plan, double Value)> LastRootValues { get; private set; }

        public EngineBot(int seed, EngineSettings settings = null, BotWeights weights = null)
            : this(seed, settings, weights, null)
        {
        }

        /// <param name="scales">판 평가 항목 배수(튜닝용). 없으면 모두 1.</param>
        internal EngineBot(int seed, EngineSettings settings, BotWeights weights, EvalScales scales)
        {
            _random = new Random(seed);
            Settings = settings ?? new EngineSettings();
            Weights = weights ?? new BotWeights();
            _evaluator = Evaluator.CreateDefault(scales);
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

        private bool CanMistake => Settings.MistakeChance > 0 && Settings.Depth <= 1 && Settings.MistakeRange >= 2;

        private void Replan(GameController game, Team team)
        {
            var watch = Stopwatch.StartNew();
            bool TimeUp() => Settings.TimeLimitMs > 0 && watch.ElapsedMilliseconds >= Settings.TimeLimitMs;

            var search = new TurnSearch(new TurnSearchSettings
            {
                Depth = Settings.Depth,
                MyPlans = Settings.MyPlans,
                ReplyPlans = Settings.ReplyPlans,
                ChanceSamples = Settings.ChanceSamples,
                Parallel = Settings.Parallel,
                MinRootPlans = CanMistake ? Settings.MistakeRange : 1,
                Planner = new PlannerSettings
                {
                    BeamWidth = Settings.BeamWidth,
                    MaxActions = Settings.MaxActions,
                    MaxPerActionKey = Settings.MaxPerActionKey,
                    NodeBudget = Settings.NodeBudget,
                },
            }, _evaluator, Weights, TimeUp, Cancellation);

            _plan = search.Search(game.Fork(team), team, _stall.IsStalled(Weights));
            _cursor = 0;

            // 실수: 계획 점수 2~MistakeRange 위 중 하나(후보는 행동 조합이 서로 다른 계획이라 그럴듯한 차선책이 된다).
            List<(TurnPlan Plan, double Value)> roots = search.LastRootValues;
            if (CanMistake && roots.Count > 1 && _random.NextDouble() < Settings.MistakeChance)
                _plan = roots[1 + _random.Next(Math.Min(Settings.MistakeRange, roots.Count) - 1)].Plan;

            LastPlanScore = _plan.Score;
            LastNodesSearched = search.LastNodes;
            LastTranspositions = search.LastTranspositions;
            LastPlannerCalls = search.LastPlannerCalls;
            LastRootValues = search.LastRootValues;
            LastSearchMilliseconds = watch.Elapsed.TotalMilliseconds;
        }
    }
}
