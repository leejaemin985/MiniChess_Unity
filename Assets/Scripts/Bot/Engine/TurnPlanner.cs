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

        /// <summary>
        /// 2단계 채점: 단계마다 가벼운 점수 상위 몇 개만 턴 종료까지 실행해 정밀 채점할지. 0 이면 모두 정밀 채점(가장 느림).
        /// 정밀 채점 수가 빔 폭보다 넉넉해야 빔이 가벼운 점수의 오차에 휘둘리지 않는다.
        /// </summary>
        public int FullScoreCount;
    }

    /// <summary>
    /// 이번 턴의 행동 순서를 읽는다(빔 탐색). 후보를 규칙 점수로 미리 거르지 않고, 단계마다 모든 합법 행동을 Core 로 실제 실행해
    /// 판 점수로 고른다. 그래서 새 메커닉(장판, AP 토큰 등)의 결과를 봇이 따로 알 필요가 없다.
    /// 2단계 채점(FullScoreCount)이면 실행한 판을 먼저 가볍게(턴 종료 없이) 평가하고, 상위만 턴을 끝낸 판으로 정밀 채점한다.
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
        /// <param name="quickScore">2단계 채점의 가벼운 점수(턴 종료 없이 평가). null 이거나 FullScoreCount 가 0 이면 모든 판을 정밀 채점한다.</param>
        public List<TurnPlan> Plan(GameController start, Team team, Func<GameController, double> scoreAfterTurn, Func<bool> stop,
            CancellationToken cancellation = default, Func<GameController, double> quickScore = null)
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

            bool twoStage = quickScore != null && _settings.FullScoreCount > 0;

            for (int step = 0; step < _settings.MaxActions && beam.Count > 0 && !exhausted; step++)
            {
                // 1) 빔의 모든 줄에 모든 합법 행동을 실행해 본다. 2단계 채점이면 여기서는 가벼운 점수(턴 종료 없이 평가)만 매긴다.
                var executed = new List<(Line Parent, BotAction Action, GameController Game, double Quick)>();
                foreach (Line line in beam)
                {
                    if (line.Game.State.IsGameOver || line.Game.State.CurrentTeam != team)
                        continue;

                    long started = SearchProfile.Begin();
                    List<BotAction> actions = BotActionGenerator.Generate(line.Game, team);
                    SearchProfile.End(ProfileStep.Generate, started);

                    foreach (BotAction action in actions)
                    {
                        if ((_settings.NodeBudget > 0 && nodes >= _settings.NodeBudget) || cancellation.IsCancellationRequested || stop())
                        {
                            exhausted = true;
                            break;
                        }

                        started = SearchProfile.Begin();
                        GameController child = line.Game.Fork();
                        SearchProfile.End(ProfileStep.Fork, started);

                        started = SearchProfile.Begin();
                        bool executedOk = action.Execute(child);
                        SearchProfile.End(ProfileStep.Execute, started);
                        if (!executedOk)
                            continue;

                        nodes++;
                        executed.Add((line, action, child, twoStage ? quickScore(child) : 0));
                    }

                    if (exhausted)
                        break;
                }

                // 2) 가벼운 점수 상위 FullScoreCount 개만(2단계 채점이 아니면 모두) 중복 판을 거르고 턴 종료까지 실행해 정밀하게 채점한다.
                //    해시와 턴 종료는 판 하나에 드는 비용의 대부분이라, 빔에 들 가능성이 낮은 판에는 쓰지 않는다.
                IEnumerable<(Line Parent, BotAction Action, GameController Game, double Quick)> ordered =
                    twoStage ? executed.OrderByDescending(item => item.Quick) : (IEnumerable<(Line, BotAction, GameController, double)>)executed;
                var children = new List<Line>();
                foreach ((Line parent, BotAction action, GameController child, double _) in ordered)
                {
                    if (twoStage && children.Count >= _settings.FullScoreCount)
                        break;

                    ulong hash = StateHasher.Hash(child, team);
                    if (!seen.Add(hash))
                    {
                        transpositions++;
                        continue;
                    }

                    children.Add(new Line
                    {
                        Game = child,
                        Actions = new List<BotAction>(parent.Actions) { action },
                        Hashes = new List<ulong>(parent.Hashes) { hash },
                        Hash = hash,
                        Score = scoreAfterTurn(child),
                    });
                }

                if (children.Count == 0)
                    break;

                beam = SelectBeam(children);
                best = best.Concat(children).OrderByDescending(line => line.Score).Take(CandidatesToKeep).ToList();

                // 빔과 상위 계획이 아닌 판은 더 쓰지 않으므로 놓아 준다(판 하나가 수십 KB).
                foreach (Line child in children)
                {
                    if (!beam.Contains(child) && !best.Contains(child))
                        child.Game = null;
                }
            }

            LastNodes = nodes;
            LastTranspositions = transpositions;
            return Diverse(best).Select(line => new TurnPlan
            {
                Actions = line.Actions,
                ExpectedHashes = line.Hashes,
                Score = line.Score,
                Result = line.Game,
            }).ToList();
        }

        /// <summary>계획을 고르기 전에 모아 두는 후보 수(다양한 계획을 고를 여유).</summary>
        private int CandidatesToKeep => _settings.PlansToKeep <= 1 ? 1 : _settings.PlansToKeep * 4;

        /// <summary>
        /// 점수 순으로 PlansToKeep 개를 고르되, 이동을 뺀 행동(공격, 스킬과 그 대상)의 조합이 같은 계획은 가장 좋은 하나만 먼저 고른다.
        /// 끝에 이동 한 칸만 다른 계획들로 자리가 차면 턴 단위 탐색이 사실상 같은 계획끼리 비교하게 된다.
        /// 조합이 다른 계획이 모자라면 남은 자리는 점수 순으로 채운다.
        /// </summary>
        private List<Line> Diverse(List<Line> candidates)
        {
            if (_settings.PlansToKeep <= 1)
                return candidates.Take(1).ToList();

            var picked = new List<Line>();
            var signatures = new HashSet<string>();
            foreach (Line line in candidates)
            {
                if (picked.Count >= _settings.PlansToKeep)
                    break;
                if (signatures.Add(CombatSignature(line)))
                    picked.Add(line);
            }

            foreach (Line line in candidates)
            {
                if (picked.Count >= _settings.PlansToKeep)
                    break;
                if (!picked.Contains(line))
                    picked.Add(line);
            }

            return picked.OrderByDescending(line => line.Score).ToList();
        }

        /// <summary>이동을 뺀 행동들(순서 무관). 같은 공격/스킬을 같은 대상에 쓰는 계획은 이동이 달라도 같은 조합이다.</summary>
        private static string CombatSignature(Line line)
        {
            return string.Join("|", line.Actions.Where(action => action.Kind != BotActionKind.Move).Select(action => action.ToString()).OrderBy(text => text, StringComparer.Ordinal));
        }

        /// <summary>
        /// 점수 순으로 BeamWidth 개. 먼저 이동을 뺀 행동 조합이 서로 다른 줄로 채우고(빔이 한 줄의 변형으로만 차지 않게),
        /// 모자라면 나머지를 점수 순으로 채운다. 어느 쪽이든 마지막 행동 종류가 같은 줄은 MaxPerActionKey 개까지만.
        /// </summary>
        private List<Line> SelectBeam(List<Line> children)
        {
            var beam = new List<Line>();
            var perKey = new Dictionary<(int UnitId, BotActionKind Kind, string SkillId), int>();
            var signatures = new HashSet<string>();
            List<Line> ordered = children.OrderByDescending(line => line.Score).ToList();

            bool TryAdd(Line line)
            {
                BotAction last = line.Actions[line.Actions.Count - 1];
                var key = (last.UnitId, last.Kind, last.SkillId);
                perKey.TryGetValue(key, out int taken);
                if (taken >= _settings.MaxPerActionKey)
                    return false;

                perKey[key] = taken + 1;
                beam.Add(line);
                return true;
            }

            foreach (Line line in ordered)
            {
                if (beam.Count >= _settings.BeamWidth)
                    break;
                string signature = CombatSignature(line);
                if (!signatures.Contains(signature) && TryAdd(line))
                    signatures.Add(signature);
            }

            foreach (Line line in ordered)
            {
                if (beam.Count >= _settings.BeamWidth)
                    break;
                if (!beam.Contains(line))
                    TryAdd(line);
            }

            return beam;
        }
    }
}
