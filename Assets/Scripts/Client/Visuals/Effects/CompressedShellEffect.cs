using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 압축열탄(FX_CompressedShell). 손 앞에 불씨/불꽃이 소용돌이치며 빨려 들어 기가 모이고(charge, chargeTime 동안),
    /// 손을 떠나는 순간 불꽃이 터지며(launchBurst), 하얗게 달아오른 핵의 주황 열탄이 낮은 포물선으로 날아가(불꽃/불티/연기 꼬리)
    /// 대상 앞에서 압축 폭발(섬광, 피어오르는 불길, 불티, 바닥 그을음 고리)하고, 대상 몸 둘레에 불꽃이 잠시 피어올라 화상이 붙었음을 보여준다.
    /// charge 가 있으면 chargeTime 이 발사 전 대기(LaunchDelay)가 되어, 모션과 함께 먼저 재생되고 명중할 때 타격을 보인다(장거리 조준과 같은 흐름).
    /// 평타(FX_FlameBasicAttack)는 charge 없이 같은 컴포넌트를 쓴다.
    /// </summary>
    public sealed class CompressedShellEffect : ProjectileEffect
    {
        [Tooltip("기 모으기(하위 파티클 포함). 비우면 모으지 않고 바로 던진다.")]
        [SerializeField] private ParticleSystem charge;
        [Tooltip("기를 모으는 시간(초) = 발사 전 대기. 시전 모션의 던지는 순간에 맞춘다.")]
        [SerializeField, Min(0f)] private float chargeTime = 0.92f;
        [Tooltip("기를 모으는 위치: 발사 지점에서 위로, 대상 쪽으로 옮기는 거리.")]
        [SerializeField] private float chargeUp = 0.15f;
        [SerializeField] private float chargeForward = 0.25f;

        [Tooltip("발사 순간 불꽃 터짐(하위 파티클 포함). 발사 지점에서 +Z 를 날아가는 방향으로 맞춰 재생한다.")]
        [SerializeField] private ParticleSystem launchBurst;

        public override float LaunchDelay => charge != null ? chargeTime : 0f;

        protected override void OnAimStarted()
        {
            if (charge == null)
                return;

            Vector3 forward = HitPoint - LaunchPoint;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            charge.transform.SetPositionAndRotation(LaunchPoint + Vector3.up * chargeUp + forward * chargeForward, Quaternion.LookRotation(forward));
            charge.Clear(true);
            charge.Play(true);
        }

        protected override void OnLaunched()
        {
            if (launchBurst == null)
                return;

            Vector3 direction = HitPoint - LaunchPoint;
            launchBurst.transform.SetPositionAndRotation(LaunchPoint, direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity);
            launchBurst.Clear(true);
            launchBurst.Play(true);
        }
    }
}
