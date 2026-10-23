using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 볼라 투척(FX_Bola). 추 2개를 밧줄로 이은 볼라가 수평으로 돌며 대상 다리 높이로 날아가고,
    /// 도착하면 볼라는 사라지고 발밑에서 밧줄 소용돌이와 고리들이 조여들며 감긴다(흙먼지, 추가 부딪히는 불티).
    /// </summary>
    public sealed class BolaEffect : ProjectileEffect
    {
        [Tooltip("날아가는 동안 도는 부분(추와 밧줄). 투사체 아래에 둔다.")]
        [SerializeField] private Transform spinner;
        [Tooltip("도는 속도(도/초). 수평으로 돈다.")]
        [SerializeField] private float spinSpeed = 1080f;

        protected override void Update()
        {
            if (spinner != null)
                spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

            base.Update();
        }
    }
}
