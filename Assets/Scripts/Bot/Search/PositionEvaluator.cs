using System;
using System.Linq;
using MiniChess.Bot.Rules;
using MiniChess.Core.Combat;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Effects.Scheduled;
using MiniChess.Core.State;
using MiniChess.Core.Statuses;
using MiniChess.Core.Statuses.Library;

namespace MiniChess.Bot.Search
{
    /// <summary>
    /// 판 하나의 점수(team 기준, 높을수록 좋다). 탐색 봇이 복사한 경기에서 행동을 실행해 본 뒤의 판을 비교하는 데 쓴다.
    /// 단위는 규칙 봇과 같이 대략 "상대에게 준 HP 피해 1" 이며, 가중치도 규칙 봇(BotWeights)과 같은 값을 쓴다.
    ///   + 살아 있는 유닛의 가치(HP, 보호막, 처치 가치) 아군 - 적
    ///   - 다음 상대 턴에 아군이 받을 것으로 예상되는 피해(위협도)와 적과의 거리
    ///   + 점령 진행, 적에게 남은 지속 피해/표식, 내 차징, 착탄 대기 중인 내 포격, 다음 턴으로 넘어가는 AP
    /// </summary>
    internal static class PositionEvaluator
    {
        /// <summary>승패가 난 판의 점수(어떤 판 점수보다 크게).</summary>
        public const double WinScore = 100000;

        public static double Evaluate(GameController game, Team team, BotWeights weights, bool stalled)
        {
            GameState state = game.State;
            if (state.IsGameOver)
                return state.Winner == team ? WinScore - state.TurnNumber : state.Winner.HasValue ? -WinScore + state.TurnNumber : 0;

            var a = new BoardAnalysis(game, team, weights, stalled);
            double value = 0;

            foreach (Unit unit in a.Allies)
                value += UnitWorth(a, unit) - Exposure(a, unit) - weights.Approach * a.StepsToEngage(unit.Position.Value, a.PreferredRange(unit));

            foreach (Unit unit in a.Enemies)
                value -= UnitWorth(a, unit) - Afflictions(unit, weights);

            foreach (Unit unit in a.Allies)
            {
                if (SkillEstimates.FindBehavior<ChargedBehavior>(unit, StatusLibrary.ChargedId) is ChargedBehavior charged)
                    value += charged.BonusDamage * weights.DamageOverTime;
            }

            value += PendingStrikes(a, team);
            value += Capture(a, team, weights);
            value += CarriedAp(state, team, weights);
            return value;
        }

        /// <summary>살아 있는 유닛의 가치: 남은 HP + 보호막(비율) + 처치했을 때의 가치(잃으면 그만큼 손해).</summary>
        private static double UnitWorth(BoardAnalysis a, Unit unit)
        {
            return unit.Stats.CurrentHp + unit.Stats.Shield * a.Weights.ShieldDamageRatio + a.KillValue(unit);
        }

        /// <summary>다음 상대 턴의 예상 손해(규칙 봇의 위치 판단과 같은 식).</summary>
        private static double Exposure(BoardAnalysis a, Unit unit)
        {
            double danger = a.DangerAt(unit.Position.Value);
            int effectiveHp = BoardAnalysis.EffectiveHp(unit);
            double loss = a.Caution * a.Weights.Danger * Math.Min(danger, effectiveHp);
            if (danger >= effectiveHp)
                loss += a.Caution * a.Weights.DeathRisk * a.KillValue(unit);
            return loss;
        }

        /// <summary>적에게 남은 지속 피해와 저주 표식의 가치.</summary>
        private static double Afflictions(Unit enemy, BotWeights weights)
        {
            double value = 0;
            foreach (StatusEffect status in enemy.Statuses)
            {
                switch (status.Definition.Behavior)
                {
                    case DamageOverTimeBehavior dot:
                        value += dot.DamagePerTrigger * status.Remaining * weights.DamageOverTime;
                        break;
                    case CurseMarkBehavior mark:
                        value += mark.BonusDamage * weights.DamageOverTime;
                        break;
                }
            }

            return value;
        }

        /// <summary>착탄을 기다리는 내 포격이 지금 범위 안의 적에게 줄 피해 기댓값.</summary>
        private static double PendingStrikes(BoardAnalysis a, Team team)
        {
            double value = 0;
            foreach (ScheduledStrike strike in a.State.ScheduledStrikes.Where(strike => strike.SourceTeam == team))
            {
                foreach (Unit enemy in a.Enemies.Where(enemy => strike.Cells.Contains(enemy.Position.Value)))
                {
                    bool immobile = BoardAnalysis.IsRooted(enemy) || BoardAnalysis.IsInstalled(enemy) || BoardAnalysis.IsStunned(enemy);
                    double chance = immobile ? a.Weights.StrikeHitChanceImmobile : a.Weights.StrikeHitChance;
                    value += a.DamageValue(strike.Source, enemy, strike.Damage, DamageType.Area) * chance;
                }
            }

            return value;
        }

        /// <summary>점령: 쌓인 진행도, 점령 칸 위의 아군.</summary>
        private static double Capture(BoardAnalysis a, Team team, BotWeights weights)
        {
            CaptureState capture = a.State.Capture;
            if (!a.CaptureOpen)
                return 0;

            double value = weights.Capture * (capture.GetProgress(team) - capture.GetProgress(a.Enemy));
            Unit holder = a.State.Board.GetLivingUnit(capture.Tile.Value);
            if (holder != null && !holder.IsSummon)
                value += holder.Team == team ? weights.Capture : -weights.Capture;
            return value;
        }

        /// <summary>
        /// 남겨 둬서 내 턴에 회복분보다 더 쓸 수 있는 AP 의 가치(최대치에 막혀 버려질 몫은 0).
        /// 상대 차례면 다음 내 턴의 회복 후 AP 로, 내 차례(상대 응수까지 본 판)면 이미 회복된 지금 AP 로 본다.
        /// </summary>
        private static double CarriedAp(GameState state, Team team, BotWeights weights)
        {
            if (!state.Rules.Ap.CarryOver)
                return 0;

            PlayerState player = state.GetPlayer(team);
            int recovery = Math.Min(player.Ap.Max, state.Rules.Ap.TurnRecoveryAp);
            int nextTurnAp = state.CurrentTeam == team ? player.Ap.Current : Math.Min(player.Ap.Max, player.Ap.Current + recovery);
            return Math.Max(0, nextTurnAp - recovery) * weights.ApCost;
        }
    }
}
