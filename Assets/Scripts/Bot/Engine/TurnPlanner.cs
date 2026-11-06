using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiniChess.Bot.Search;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;

namespace MiniChess.Bot.Engine
{
    /// <summary>턴 계획 하나: 이번 턴에 할 행동 순서와, 행동마다 실행한 뒤 예상되는 판 해시.</summary>
    internal sealed class TurnPlan
    {
        public IReadOnlyList<BotAction> Actions;

        /// <summary>Actions[i] 를 실행한 뒤의 판 해시(StateHasher). 실제 경기가 이와 다르면 계획을 다시 세운다.</summary>
        public IReadOnlyList<ulong> ExpectedHashes;

        /// <summary>턴을 끝낸 판의 점수.</summary>
        public double Score;

        /// <summary>행동을 모두 실행한 판(턴 종료 전). 상위 계획만 남기고, 나머지는 메모리를 아끼려고 비운다.</summary>
        public GameController Result;
    }

    /// <summary>턴 계획 생성 설정.</summary>
    internal sealed class PlannerSettings
    {
        /// <summary>단계(행동 하나)마다 이어서 읽을 행동 순서 수.</summary>
        public int BeamWidth = 8;

        /// <summary>한 턴에 읽는 행동 수 상한.</summary>
        public int MaxActions = 10;

        /// <summary>
        /// 빔 안에서 마지막 행동의 종류(유닛, 행동, 스킬)가 같은 순서의 수 상한.
        /// 한 유닛의 비슷한 이동/스킬 변형이 빔을 모두 차지해 다른 유닛의 행동을 읽지 못하는 것을 막는다.
        /// </summary>
        public int MaxPerActionKey = 2;

        /// <summary>돌려줄 계획 수(점수 순, 결과 판이 서로 다른 것).</summary>
        public int PlansToKeep = 1;

        /// <summary>실행해 볼 판 수 상한. 0 이면 제한 없음.</summary>
        public int NodeBudget;
    }

    /// <summary>
    /// 이번 턴의 행동 순서를 읽는다(빔 탐색). 후보를 미리 점수로 거르지 않고, 단계마다 모든 합법 행동을 Core 로 실제 실행해
    /// 턴을 끝낸 판의 점수로 고른다. 그래서 새 메커닉(장판, AP 토큰 등)의 결과를 봇이 따로 알 필요가 없다.
    /// 행동 순서만 다르고 결과가 같은 판은 해시로 한 번만 읽는다.
    /// </summary>
    internal sealed class TurnPlanner
    {
        private sealed class Line
        {
            public GameController Game;
            public List<BotAction> Actions;
            public List<ulong> Hashes;
            public ulong Hash;
            public double Score;
        }

        private readonly PlannerSettings _settings;

        /// <summary>마지막으로 읽은 판 수와 해시로 건너뛴 판 수.</summary>
        public int LastNodes { get; private set; }
        public int LastTranspositions { get; private set; }

        public TurnPlanner(PlannerSettings settings)
        {
            _settings = settings;
        }

        /// <param name="start">자기 팀 시점 복사본(Fork(team)). 바뀌지 않는다.</param>
        /// <param name="scoreAfterTurn">행동을 실행한 판(턴 종료 전)을 받아, 턴을 끝낸 판의 점수를 낸다.</param>
        /// <param name="stop">true 면 그때까지 읽은 것으로 끝낸다(시간 제한, 취소).</param>
        public List<TurnPlan> Plan(GameController start, Team team, Func<GameController, double> scoreAfterTurn, Func<bool> stop, CancellationToken cancellation = default)
        {
            int nodes = 0, transpositions = 0;
            var seen = new HashSet<ulong>();
            var root = new Line
            {
                Game = start,
                Actions = new List<BotAction>(),
                Hashes = new List<ulong>(),
                Hash = StateHasher.Hash(start, team),
            };
            root.Score = scoreAfterTurn(start);
            seen.Add(root.Hash);

            var best = new List<Line> { root };
            var beam = new List<Line> { root };
            bool exhausted = false;

            for (int step = 0; step < _settings.MaxActions && beam.Count > 0 && !exhausted; step++)
            {
                var children = new List<Line>();
                foreach (Line line in beam)
                {
                    if (line.Game.State.IsGameOver || line.Game.State.CurrentTeam != team)
                        continue;

                    foreach (BotAction action in BotActionGenerator.Generate(line.Game, team))
                    {
                        if ((_settings.NodeBudget > 0 && nodes >= _settings.NodeBudget) || cancellation.IsCancellationRequested || stop())
                        {
                            exhausted = true;
                            break;
                        }

                        GameController child = line.Game.Fork();
                        if (!action.Execute(child))
                            continue;

                        ulong hash = StateHasher.Hash(child, team);
                        if (!seen.Add(hash))
                        {
                            transpositions++;
                            continue;
                        }

                        nodes++;
                        children.Add(new Line
                        {
                            Game = child,
                            Actions = new List<BotAction>(line.Actions) { action },
                            Hashes = new List<ulong>(line.Hashes) { hash },
                            Hash = hash,
                            Score = scoreAfterTurn(child),
                        });
                    }

                    if (exhausted)
                        break;
                }

                if (children.Count == 0)
                    break;

                beam = SelectBeam(children);
                best = best.Concat(children).OrderByDescending(line => line.Score).Take(_settings.PlansToKeep).ToList();

                // 빔과 상위 계획이 아닌 판은 더 쓰지 않으므로 놓아 준다(판 하나가 수십 KB).
                foreach (Line child in children)
                {
                    if (!beam.Contains(child) && !best.Contains(child))
                        child.Game = null;
                }
            }

            LastNodes = nodes;
            LastTranspositions = transpositions;
            return best.Select(line => new TurnPlan
            {
                Actions = line.Actions,
                ExpectedHashes = line.Hashes,
                Score = line.Score,
                Result = line.Game,
            }).ToList();
        }

        /// <summary>점수 순으로 BeamWidth 개. 마지막 행동 종류가 같은 순서는 MaxPerActionKey 개까지만.</summary>
        private List<Line> SelectBeam(List<Line> children)
        {
            var beam = new List<Line>();
            var perKey = new Dictionary<(int UnitId, BotActionKind Kind, string SkillId), int>();
            foreach (Line line in children.OrderByDescending(line => line.Score))
            {
                if (beam.Count >= _settings.BeamWidth)
                    break;

                BotAction last = line.Actions[line.Actions.Count - 1];
                var key = (last.UnitId, last.Kind, last.SkillId);
                perKey.TryGetValue(key, out int taken);
                if (taken >= _settings.MaxPerActionKey)
                    continue;

                perKey[key] = taken + 1;
                beam.Add(line);
            }

            return beam;
        }
    }
}
