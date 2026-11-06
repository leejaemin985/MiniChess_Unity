using MiniChess.Bot;

namespace MiniChess.Client.Players
{
    /// <summary>
    /// 게임에서 쓸 봇 종류(GameSession 에서 팀별로 고른다, 경기 시작 때의 값이 그 경기 동안 쓰인다).
    ///   - Engine: 엔진 봇(EngineBot), 레벨 1~10.
    ///   - Legacy: 이전 봇(규칙/탐색 봇), 쉬움/보통/어려움. 새 봇과 비교해 보는 용도.
    /// 값 순서는 씬에 숫자로 저장되므로 새 값은 끝에만 붙인다.
    /// </summary>
    public enum BotMode
    {
        Engine,
        Legacy,
    }

    /// <summary>이전 봇의 난이도(BotDifficulty 의 Easy/Normal/Hard 와 같은 값).</summary>
    public enum LegacyBotDifficulty
    {
        Easy = BotDifficulty.Easy,
        Normal = BotDifficulty.Normal,
        Hard = BotDifficulty.Hard,
    }
}
