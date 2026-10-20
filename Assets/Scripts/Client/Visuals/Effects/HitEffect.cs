using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 타격 이펙트의 기반. 맞은 자리(칸 윗면 중심)에서 공격 방향을 향해 한 번 재생되고, 끝나면 스스로 풀로 돌아간다.
    /// 프리팹은 +Z 가 공격 방향이 되게 만든다(Place 가 +Z 를 공격 방향으로 돌린다).
    /// </summary>
    public abstract class HitEffect : EffectBase
    {
        [Tooltip("맞은 자리에서 공격자 쪽으로 당기는 거리(월드 단위). 피격자 몸통(캡슐) 안에서 터지지 않게 한다.")]
        [SerializeField] private float towardAttackerOffset = 0.35f;

        protected override bool ReleaseWhenFinished => true;

        /// <param name="position">맞은 칸 윗면 중심(월드).</param>
        /// <param name="direction">공격 방향(공격자 → 피격자, 월드). 길이가 0 이면 방향 없음(당기지 않는다).</param>
        public void Place(Vector3 position, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                transform.SetPositionAndRotation(position, Quaternion.identity);
                return;
            }

            direction.Normalize();
            transform.SetPositionAndRotation(position - direction * towardAttackerOffset, Quaternion.LookRotation(direction));
        }
    }
}
