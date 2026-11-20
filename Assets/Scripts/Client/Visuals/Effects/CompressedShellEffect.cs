using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 압축열탄(FX_CompressedShell). 손을 떠나는 순간 불꽃이 터지고(launchBurst), 하얗게 달아오른 핵의 주황 열탄이 낮은 포물선으로 날아가(불꽃/불티/연기 꼬리)
    /// 대상 앞에서 압축 폭발(섬광, 피어오르는 불길, 불티, 바닥 그을음 고리)하고, 대상 몸 둘레에 불꽃이 잠시 피어올라 화상이 붙었음을 보여준다.
    /// </summary>
    public sealed class CompressedShellEffect : ProjectileEffect
    {
        [Tooltip("발사 순간 불꽃 터짐(하위 파티클 포함). 발사 지점에서 +Z 를 날아가는 방향으로 맞춰 재생한다.")]
        [SerializeField] private ParticleSystem launchBurst;

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
