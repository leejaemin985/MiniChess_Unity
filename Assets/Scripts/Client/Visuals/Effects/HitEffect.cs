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
        [Tooltip("카메라 쪽으로 당기는 거리(월드 단위). 피격자 몸통에 가려지지 않고 앞에서 보이게 한다.\n" +
                 "기준점(맞은 자리 + AimHeight)에서 카메라 위치로 향하는 시선을 따라 당기므로 화면에서는 기준점과 같은 자리에 겹친다.")]
        [SerializeField] private float towardCamera = 0.35f;

        protected override bool ReleaseWhenFinished => true;

        /// <summary>카메라 쪽으로 당길 때 시선의 기준이 되는 높이(맞은 자리 위). 몸통 중앙에 그리는 이펙트는 그 높이를 준다.</summary>
        protected virtual float AimHeight => 0f;

        /// <param name="position">맞은 칸 윗면 중심(월드).</param>
        /// <param name="direction">공격 방향(공격자 → 피격자, 월드). 길이가 0 이면 방향 없음(당기지 않는다).</param>
        public void Place(Vector3 position, Vector3 direction)
        {
            position += TowardCamera(position + Vector3.up * AimHeight) * towardCamera;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                transform.SetPositionAndRotation(position, Quaternion.identity);
                return;
            }

            direction.Normalize();
            transform.SetPositionAndRotation(position - direction * towardAttackerOffset, Quaternion.LookRotation(direction));
        }

        /// <summary>
        /// point 에서 카메라로 향하는 방향. 카메라 축(-forward)이 아니라 그 점을 지나는 시선이라, 원근 카메라에서도 화면 위치가 밀리지 않는다.
        /// 카메라가 없으면 0.
        /// </summary>
        internal static Vector3 TowardCamera(Vector3 point)
        {
            Camera camera = Camera.main;
            if (camera == null)
                return Vector3.zero;
            if (camera.orthographic)
                return -camera.transform.forward;

            Vector3 toCamera = camera.transform.position - point;
            return toCamera.sqrMagnitude < 0.0001f ? Vector3.zero : toCamera.normalized;
        }
    }
}
