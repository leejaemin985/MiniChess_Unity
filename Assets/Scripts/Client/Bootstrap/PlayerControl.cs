namespace MiniChess.Client.Bootstrap
{
    /// <summary>팀을 누가 조작하는지.</summary>
    public enum PlayerControl
    {
        /// <summary>마우스로 직접 조작(한 화면 교대).</summary>
        Human,

        /// <summary>규칙 기반 봇(RuleBot)이 배치와 턴을 자동으로 진행.</summary>
        Bot,
    }
}
