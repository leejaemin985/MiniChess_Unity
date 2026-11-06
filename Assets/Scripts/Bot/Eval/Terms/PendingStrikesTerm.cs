using System.Linq;
using MiniChess.Bot.Rules;
using MiniChess.Core.Combat;
using MiniChess.Core.Effects.Scheduled;
using MiniChess.Core.State;

namespace MiniChess.Bot.Eval.Terms
{
    /// <summary>착탄을 기다리는 내 포격이 지금 범위 안의 적에게 줄 피해 기댓값(적이 피할 수 있으면 확률을 낮춘다).</summary>
    internal sealed class PendingStrikesTerm : IEvalTerm
    {
        public string Name => "PendingStrikes";

        public double Evaluate(BoardAnalysis a)
        {
            double value = 0;
            foreach (ScheduledStrike strike in a.State.ScheduledStrikes.Where(strike => strike.SourceTeam == a.Team))
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
    }
}
