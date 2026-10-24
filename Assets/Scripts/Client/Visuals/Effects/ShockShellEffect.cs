using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 충격탄(마법공학탄, FX_ShockShell). 박격포 앞에 마법진이 돌며 펼쳐지고(muzzle), 룬 고리를 두른 탄(projectile)이 직선으로 날아가
    /// 맞는 순간 충격파(impact)와 함께 밀려나는 방향으로 빛줄기/불티(pushBurst)가 뻗는다.
    /// 대상이 밀려 멈춘 뒤 그 칸 발밑에서 푸른 전류(shackle)가 지직거려 이동 제한을 알린다.
    /// 밀려나는 방향은 밀린 도착 칸(PushedTo)이 있으면 그쪽, 없으면 코어 밀어내기 규칙(세로 차이 ≥ 가로 차이면 상하)으로 정한다.
    /// 도착 시간(travelTime)은 밀려남 출발 지연(UnitsView.knockbackDelay)과 맞춘다.
    /// </summary>
    public sealed class ShockShellEffect : ProjectileEffect
    {
        [Tooltip("발사 순간 시전자 앞에서 재생(+Z 가 대상 쪽으로 놓인다).")]
        [SerializeField] private ParticleSystem muzzle;
        [Tooltip("muzzle 을 발사 지점에서 대상 쪽으로 내미는 거리.")]
        [SerializeField] private float muzzleForward = 0.35f;
        [Tooltip("맞는 순간 대상 몸에서 재생(+Z 가 밀려나는 방향으로 놓인다).")]
        [SerializeField] private ParticleSystem pushBurst;
        [Tooltip("밀려 멈춘 칸 발밑에서 재생하는 전류(이동 제한).")]
        [SerializeField] private ParticleSystem shackle;
        [Tooltip("맞은 뒤 shackle 을 재생하기까지의 시간(초). 밀려 미끄러지는 시간과 맞춘다.")]
        [SerializeField, Min(0f)] private float shackleDelay = 0.22f;

        private float _sinceArrive;
        private bool _shacklePending;

        protected override bool IsFinished => base.IsFinished && !_shacklePending;

        protected override void OnLaunched()
        {
            _shacklePending = false;
            Vector3 aim = Flat(TargetCell - LaunchPoint);
            Quaternion facing = aim.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(aim) : Quaternion.identity;
            muzzle.transform.SetPositionAndRotation(LaunchPoint + facing * Vector3.forward * muzzleForward, facing);
            muzzle.Play(true);
        }

        protected override void OnArrived()
        {
            Vector3 push = PushDirection();
            pushBurst.transform.SetPositionAndRotation(HitPoint, Quaternion.LookRotation(push));
            pushBurst.Play(true);

            _sinceArrive = 0f;
            _shacklePending = true;
        }

        protected override void OnReleased()
        {
            base.OnReleased();
            _shacklePending = false;
        }

        protected override void Update()
        {
            if (_shacklePending)
            {
                _sinceArrive += Time.deltaTime;
                if (_sinceArrive >= shackleDelay)
                {
                    _shacklePending = false;
                    shackle.transform.SetPositionAndRotation(PushedTo ?? TargetCell, Quaternion.identity);
                    shackle.Play(true);
                }
            }

            base.Update();
        }

        private Vector3 PushDirection()
        {
            if (PushedTo.HasValue)
            {
                Vector3 moved = Flat(PushedTo.Value - TargetCell);
                if (moved.sqrMagnitude > 0.0001f)
                    return moved.normalized;
            }

            Vector3 away = Flat(TargetCell - LaunchPoint);
            if (away.sqrMagnitude < 0.0001f)
                return Vector3.forward;
            return Mathf.Abs(away.z) >= Mathf.Abs(away.x)
                ? new Vector3(0f, 0f, Mathf.Sign(away.z))
                : new Vector3(Mathf.Sign(away.x), 0f, 0f);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
