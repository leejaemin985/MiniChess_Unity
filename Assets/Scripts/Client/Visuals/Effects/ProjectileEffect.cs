using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 시전자 → 대상으로 날아가 명중하는 투사체 이펙트의 기반(장거리 조준 등). 끝나면 스스로 풀로 돌아간다.
    /// projectile(하위 파티클 포함)이 일정 속도로 날아가고, 도착하면 방출을 멈추고 impact 를 대상 자리에서 재생한다.
    /// projectile 의 +Z 가 날아가는 방향이 되게 만든다. impact 는 +Z 가 시전자 쪽을 향한다.
    /// </summary>
    public abstract class ProjectileEffect : EffectBase
    {
        [Tooltip("날아가는 부분(머리/꼬리 파티클). 꼬리처럼 남는 파티클은 World 시뮬레이션으로 둔다.")]
        [SerializeField] private Transform projectile;
        [Tooltip("명중 연출의 루트(하위 파티클 포함). 도착할 때 재생한다.")]
        [SerializeField] private ParticleSystem impact;
        [Tooltip("날아가는 모델의 렌더러(볼라 추/밧줄 등). 도착하면 숨기고, 다시 꺼낼 때 보인다. 파티클만 쓰면 비워 둔다.")]
        [SerializeField] private Renderer[] hideOnArrive = new Renderer[0];

        [Tooltip("날아가는 속도(월드 단위/초).")]
        [SerializeField, Min(0.1f)] private float speed = 18f;
        [Tooltip("시전자 칸 윗면에서 발사 지점까지 높이.")]
        [SerializeField] private float launchHeight = 0.6f;
        [Tooltip("대상 칸 윗면에서 명중 지점까지 높이.")]
        [SerializeField] private float hitHeight = 0.5f;
        [Tooltip("명중 지점을 대상에서 시전자 쪽으로 당기는 거리. 대상 몸통(캡슐) 안에서 터지지 않게 한다.")]
        [SerializeField, Min(0f)] private float hitPullback = 0.35f;
        [Tooltip("포물선 꼭대기 높이(직선 대비). 0 이면 직선으로 날아간다. 포물선이면 projectile 의 +Z 가 궤적 접선을 따라간다.")]
        [SerializeField, Min(0f)] private float arcHeight;

        private ParticleSystem[] _projectileParticles;
        private Vector3 _from;
        private Vector3 _to;
        private float _duration;
        private float _elapsed;
        private bool _flying;

        protected override bool ReleaseWhenFinished => true;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_flying && !IsAnyParticleAlive();

        /// <summary>날아가는 부분.</summary>
        protected Transform Projectile => projectile;

        /// <summary>발사 지점(월드).</summary>
        protected Vector3 LaunchPoint => _from;

        /// <summary>명중 지점(월드, hitPullback 적용 후).</summary>
        protected Vector3 HitPoint => _to;

        /// <summary>대상 칸 윗면 중심(월드, Place 에 넘긴 값).</summary>
        protected Vector3 TargetCell { get; private set; }

        /// <summary>날아가는 중인지.</summary>
        protected bool IsFlying => _flying;

        private ParticleSystem[] ProjectileParticles =>
            _projectileParticles ?? (_projectileParticles = projectile.GetComponentsInChildren<ParticleSystem>(true));

        /// <param name="from">시전자 칸 윗면 중심(월드).</param>
        /// <param name="to">대상 칸 윗면 중심(월드).</param>
        public void Place(Vector3 from, Vector3 to)
        {
            TargetCell = to;
            _from = from + Vector3.up * launchHeight;
            _to = to + Vector3.up * hitHeight;
            Vector3 direction = _to - _from;
            if (direction.magnitude > hitPullback)
            {
                _to -= direction.normalized * hitPullback;
                direction = _to - _from;
            }

            Quaternion facing = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;

            transform.SetPositionAndRotation(_from, Quaternion.identity);
            projectile.SetPositionAndRotation(_from, facing);
            if (impact != null)
                impact.transform.SetPositionAndRotation(_to, facing * Quaternion.Euler(0f, 180f, 0f));

            _duration = direction.magnitude / speed;
            _elapsed = 0f;
            _flying = true;
            SetModelVisible(true);
            foreach (ParticleSystem particle in ProjectileParticles)
                particle.Play(false);
            OnLaunched();
        }

        protected override void OnReleased()
        {
            _flying = false;
        }

        protected override void Update()
        {
            if (_flying)
            {
                _elapsed += Time.deltaTime;
                float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
                projectile.position = PointAt(t);
                if (arcHeight > 0f)
                {
                    // 궤적 접선(미분: 직선 성분 + 4h(1-2t) 위쪽)으로 머리를 돌린다.
                    Vector3 tangent = (_to - _from) + Vector3.up * (4f * arcHeight * (1f - 2f * t));
                    if (tangent.sqrMagnitude > 0.0001f)
                        projectile.rotation = Quaternion.LookRotation(tangent);
                }
                if (t >= 1f)
                    Arrive();
            }

            base.Update();
        }

        private Vector3 PointAt(float t) =>
            Vector3.Lerp(_from, _to, t) + Vector3.up * (4f * arcHeight * t * (1f - t));

        private void SetModelVisible(bool visible)
        {
            foreach (Renderer model in hideOnArrive)
            {
                if (model != null)
                    model.enabled = visible;
            }
        }

        private void Arrive()
        {
            _flying = false;
            SetModelVisible(false);
            foreach (ParticleSystem particle in ProjectileParticles)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (impact != null)
                impact.Play(true);
            OnArrived();
        }

        /// <summary>발사 직후(Place 끝)에 호출된다.</summary>
        protected virtual void OnLaunched() { }

        /// <summary>명중 지점에 도착해 impact 를 재생한 뒤 호출된다.</summary>
        protected virtual void OnArrived() { }
    }
}
