namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 기본 공격 슬래시(FX_Slash). 초승달/잔상은 바닥에 누운 평면(Local 정렬)이라 오브젝트 회전을 따른다.
    /// 프리팹 기준: 위에서 내려다볼 때 볼록한 쪽이 +Z(12시). HitEffect.Place 가 +Z 를 공격 방향으로 돌린다.
    /// </summary>
    public sealed class SlashEffect : HitEffect
    {
    }
}
