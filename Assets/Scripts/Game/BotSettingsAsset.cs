using UnityEngine;

namespace MiniChess
{
    [CreateAssetMenu(fileName = "BotSettings", menuName = "Mini Chess/Bot Settings")]
    public sealed class BotSettingsAsset : ScriptableObject
    {
        [SerializeField, Tooltip("Shown on the HUD difficulty button.")]
        private string difficultyName = "Normal";
        [SerializeField, Range(1, 4), Tooltip("Completed turns to look ahead. 1 is greedy; 2 sees the reply.")]
        private int searchDepth = 2;
        [SerializeField, Min(1), Tooltip("Simulated actions per turn, including candidate generation.")]
        private int nodeLimit = 20000;
        [SerializeField, Min(0), Tooltip("Search budget in milliseconds. 0 leaves only the node limit.")]
        private int timeLimitMs = 300;
        [SerializeField, Min(0), Tooltip("Seconds between the bot's visible actions.")]
        private float actionDelay = 0.3f;

        public BotSettings CreateSettings() => new BotSettings(difficultyName,
            Mathf.Clamp(searchDepth, 1, 4), Mathf.Max(1, nodeLimit),
            Mathf.Max(0, timeLimitMs), Mathf.Max(0f, actionDelay));
    }
}
