using System;
using MiniChess.Bot.Rules;
using MiniChess.Bot.Search;

namespace MiniChess.Bot
{
    /// <summary>
    /// 봇 난이도. 수를 얼마나 깊이 읽는지로 나눈다.
    ///   - Easy: 깊이 0(규칙 봇: 행동마다 점수만 보고 고른다). 가끔 차선책을 고른다.
    ///   - Normal: 깊이 1(이번 턴의 행동 순서를 실행해 보고 턴을 끝낸 판으로 고른다).
    ///   - Hard: 깊이 2(상대의 다음 턴 응수까지 실행해 보고 고른다). 더 넓게 읽는다.
    ///   - Custom: 깊이와 넓이를 직접 정한다(BotFactory.Create 의 custom). 깊이 0 이면 실수 없는 규칙 봇.
    /// </summary>
    public enum BotDifficulty
    {
        Easy,
        Normal,
        Hard,
        Custom,
    }

    public static class BotFactory
    {
        /// <param name="custom">Custom 일 때 쓰는 탐색 설정. 없으면 보통과 같은 값.</param>
        public static BotPlayer Create(BotDifficulty difficulty, int seed, SearchSettings custom = null)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return new RuleBot(seed) { MistakeChance = 0.3 };
                case BotDifficulty.Normal:
                    return new SearchBot(seed, new SearchSettings { Depth = 1, BeamWidth = 4, CandidatesPerStep = 6 });
                case BotDifficulty.Hard:
                    return new SearchBot(seed, new SearchSettings { Depth = 2, BeamWidth = 6, CandidatesPerStep = 8, ReplyCandidates = 6 });
                case BotDifficulty.Custom:
                    custom = custom ?? new SearchSettings();
                    return custom.Depth <= 0 ? new RuleBot(seed) : (BotPlayer)new SearchBot(seed, custom);
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null);
            }
        }
    }
}
