namespace MiniChess.Bot.Arena
{
    /// <summary>A 기준 승/무/패와 Elo 차이(95% 신뢰구간).</summary>
    public sealed class MatchStats
    {
        public int Wins, Draws, Losses, Errors;
        public int Games => Wins + Draws + Losses;
        public double Score => Games == 0 ? 0.5 : (Wins + 0.5 * Draws) / Games;

        public static MatchStats From(IEnumerable<GameResult> results)
        {
            var stats = new MatchStats();
            foreach (GameResult r in results)
            {
                if (r.Error != null) { stats.Errors++; continue; }
                if (r.ScoreA == 1) stats.Wins++;
                else if (r.ScoreA == 0) stats.Losses++;
                else stats.Draws++;
            }

            return stats;
        }

        /// <summary>점수율로 본 Elo 차이와 95% 구간(판당 점수의 표본 분산으로 계산).</summary>
        public (double elo, double low, double high) Elo()
        {
            int n = Games;
            if (n == 0)
                return (0, 0, 0);

            double p = Score;
            double variance = (Wins * Sq(1 - p) + Draws * Sq(0.5 - p) + Losses * Sq(0 - p)) / n;
            double margin = 1.96 * Math.Sqrt(variance / n);
            return (ToElo(p), ToElo(p - margin), ToElo(p + margin));
        }

        private static double Sq(double x) => x * x;

        private static double ToElo(double p)
        {
            p = Math.Clamp(p, 0.001, 0.999);
            return -400 * Math.Log10(1 / p - 1);
        }
    }
}
