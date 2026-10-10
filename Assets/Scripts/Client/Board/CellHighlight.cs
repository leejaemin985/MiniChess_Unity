namespace MiniChess.Client.Board
{
    public enum CellHighlight
    {
        Selected,
        Move,
        Attack,

        /// <summary>스킬로 지정할 수 있는 칸.</summary>
        SkillTarget,

        /// <summary>스킬 범위 미리보기.</summary>
        SkillArea,

        /// <summary>여러 칸을 지정하는 스킬에서 이미 고른 칸.</summary>
        SkillChosen,
    }
}
