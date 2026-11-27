using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 호위 시전(FX_GuardLink). 시전 모션 castFrame(30fps) 에 보호 대상 머리 위에 방패 문양(impact)이 찍힌다.
    /// 날아가는 연출은 없다. 발사 대기(LaunchDelay)로 모션과 함께 시작해 castFrame 에 바로 도착하게 하고, 호위 반영도 그때 보인다.
    /// 시전자 손 방패 잔상은 손 시전 이펙트(FX_GuardShieldGhost)가 같은 프레임에 재생한다.
    /// </summary>
    public sealed class GuardLinkEffect : ProjectileEffect
    {
        private const float Fps = 30f;
        // 거리와 상관없이 castFrame 에 바로 도착하게 하는 비행 시간(0 이면 speed 를 쓰므로 아주 짧게 둔다).
        private const float ArriveTime = 0.001f;

        [Tooltip("시전 모션 시작부터 문양이 찍힐 때까지의 프레임(30fps). 방패를 들어올리는 순간.")]
        [SerializeField, Min(1f)] private float castFrame = 8f;

        public override float LaunchDelay => castFrame / Fps;

        protected override float TravelTime => ArriveTime;
    }
}
