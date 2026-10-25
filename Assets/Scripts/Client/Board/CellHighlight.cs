namespace MiniChess.Client.Board
{
    public enum CellHighlight
    {
        Selected,
        Move,
        Attack,

        /// <summary>스킬이 닿는 사거리(대상 유무와 무관, 스킬 버튼에 마우스를 올렸을 때 미리보기).</summary>
        SkillRange,

        /// <summary>스킬로 지정할 수 있는 칸.</summary>
        SkillTarget,

        /// <summary>스킬 범위 미리보기.</summary>
        SkillArea,

        /// <summary>여러 칸을 지정하는 스킬에서 이미 고른 칸.</summary>
        SkillChosen,

        /// <summary>미는 스킬로 대상이 밀려 도착할 칸(미리보기).</summary>
        PushDestination,
    }
}
