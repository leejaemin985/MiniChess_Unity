using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 궁수 기본 공격 화살(FX_ArcherArrow). 화살 모델이 빛줄기(tracer 트레일)를 끌며 곧게 날아가고, 도착하면 모델은 사라지고 튀는 불꽃(impact)을 재생한다.
    /// 트레일은 발사 때 지난 궤적을 지우고 방출을 켜며, 도착하면 방출만 멈춰 남은 꼬리가 흐려지게 둔다.
    /// </summary>
    public sealed class ArrowEffect : ProjectileEffect
    {
        [Tooltip("화살이 끄는 빛줄기. projectile 아래에 둔다. 비우면 트레일 없음.")]
        [SerializeField] private TrailRenderer tracer;

        protected override void OnLaunched()
        {
            if (tracer == null)
                return;

            // 풀에서 다시 꺼낼 때 이전 도착 지점에서 이어 그려지지 않게 한다.
            tracer.Clear();
            tracer.emitting = true;
        }

        protected override void OnArrived()
        {
            if (tracer != null)
                tracer.emitting = false;
        }
    }
}
