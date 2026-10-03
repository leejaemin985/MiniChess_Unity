using System;

namespace MiniChess
{
    // Difficulty knobs for the Player Two search, kept free of Unity types so the
    // headless checks can sweep them the same way the editor assets do.
    public sealed class BotSettings
    {
        public string DifficultyName { get; }
        public int SearchDepth { get; }
        public int NodeLimit { get; }
        public int TimeLimitMs { get; }
        public float ActionDelay { get; }

        public BotSettings(string difficultyName = "Normal", int searchDepth = 2,
            int nodeLimit = 20000, int timeLimitMs = 300, float actionDelay = 0.3f)
        {
            if (searchDepth < 1 || searchDepth > 4) throw new ArgumentOutOfRangeException(nameof(searchDepth));
            if (nodeLimit < 1) throw new ArgumentOutOfRangeException(nameof(nodeLimit));
            if (timeLimitMs < 0) throw new ArgumentOutOfRangeException(nameof(timeLimitMs));
            if (actionDelay < 0) throw new ArgumentOutOfRangeException(nameof(actionDelay));
            DifficultyName = string.IsNullOrWhiteSpace(difficultyName) ? "Custom" : difficultyName;
            SearchDepth = searchDepth; NodeLimit = nodeLimit;
            TimeLimitMs = timeLimitMs; ActionDelay = actionDelay;
        }

        public DfsBot CreateBot() => new DfsBot(SearchDepth, NodeLimit, TimeLimitMs);
    }
}
