using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot.Rules
{
    /// <summary>
    /// 규칙 기반 봇. 시뮬레이션 없이 매 행동마다 모든 후보의 점수(ActionScorer)를 매겨 가장 좋은 것을 고르고,
    /// 남은 후보가 모두 MinActionScore 이하면 턴을 끝낸다. 모든 행동이 AP 를 쓰므로 한 턴은 반드시 끝난다.
    /// </summary>
    public class RuleBot : BotPlayer
    {
        private readonly Random _random;

        /// <summary>공격하려고 이동해 다음 행동을 이어서 해야 하는 유닛(0 이면 없음)과 그 턴 번호.</summary>
        private int _committedUnitId;
        private int _committedTurn = -1;

        public BotWeights Weights { get; }

        /// <summary>배치할 캐릭터 Id. 비우면 역할(전투 2, 수호 1, 제어 1)에 맞춰 무작위로 고른다.</summary>
        public IReadOnlyList<string> Lineup { get; set; }

        /// <summary>마지막 결정의 후보 점수(높은 순). 디버그/로그용.</summary>
        public IReadOnlyList<(BotAction Action, double Score)> LastScores { get; private set; } = Array.Empty<(BotAction, double)>();

        public RuleBot(int seed, BotWeights weights = null)
        {
            _random = new Random(seed);
            Weights = weights ?? new BotWeights();
        }

        public override IReadOnlyList<UnitPlacement> DecidePlacement(GameController game, Team team)
        {
            return BotPlacement.ByRole(game, team, _random, Lineup);
        }

        protected override BotAction Choose(GameController game, Team team)
        {
            List<BotAction> candidates = BotActionGenerator.Generate(game, team);
            if (candidates.Count == 0)
                return null;

            var scorer = new ActionScorer(new BoardAnalysis(game, team, Weights));
            LastScores = candidates
                .Select(action => (action, scorer.Score(action)))
                .OrderByDescending(pair => pair.Item2)
                .ToList();

            // 공격하려고 이동한 유닛이 있으면 다른 유닛이 AP 를 먼저 쓰지 않도록 그 유닛의 행동을 이어서 한다.
            int committed = _committedTurn == game.State.TurnNumber ? _committedUnitId : 0;
            _committedUnitId = 0;
            if (committed != 0)
            {
                foreach ((BotAction action, double score) in LastScores)
                {
                    if (action.UnitId == committed && action.Kind != BotActionKind.Move && score > Weights.MinActionScore)
                        return action;
                }
            }

            (BotAction best, double bestScore) = LastScores[0];
            if (bestScore <= Weights.MinActionScore)
                return null;

            if (scorer.IsOffensiveMove(best))
            {
                _committedUnitId = best.UnitId;
                _committedTurn = game.State.TurnNumber;
            }

            return best;
        }
    }
}
