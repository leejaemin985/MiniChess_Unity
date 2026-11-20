using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 시전자 → 대상으로 날아가 명중하는 투사체 이펙트의 기반(장거리 조준 등). 끝나면 스스로 풀로 돌아간다.
    /// projectile(하위 파티클 포함)이 일정 속도로 날아가고, 도착하면 방출을 멈추고 impact 를 대상 자리에서 재생한다.
    /// LaunchDelay 가 있으면 Place 뒤 그만큼 기다렸다가 발사한다(그동안 모델은 숨긴다, 조준 연출 등).
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
        [Tooltip("0 보다 크면 거리와 상관없이 이 시간(초)에 도착한다(speed 무시). 밀어내는 스킬은 UnitsView.knockbackDelay 와 맞춘다.")]
        [SerializeField, Min(0f)] private float travelTime;
        [Tooltip("시전자 칸 윗면에서 발사 지점까지 높이.")]
        [SerializeField] private float launchHeight = 0.6f;
        [Tooltip("대상 칸 윗면에서 명중 지점까지 높이.")]
        [SerializeField] private float hitHeight = 0.5f;
        [Tooltip("명중 지점을 대상에서 시전자 쪽으로 당기는 거리. 대상 몸통(캡슐) 안에서 터지지 않게 한다.")]
        [SerializeField, Min(0f)] private float hitPullback = 0.35f;
        [Tooltip("포물선 꼭대기 높이(직선 대비). 0 이면 직선으로 날아간다. 포물선이면 projectile 의 +Z 가 궤적 접선을 따라간다.")]
        [SerializeField, Min(0f)] private float arcHeight;
        [Tooltip("0 보다 크면 포물선 높이를 수평 거리에 비례시킨다(높이 = 거리 × 이 값, arcHeight 무시). 거리와 상관없이 같은 각도로 던진다.")]
        [SerializeField, Min(0f)] private float arcPerDistance;

        private ParticleSystem[] _projectileParticles;
        private float _arc;
        private Vector3 _from;
        private Vector3 _to;
        private float _duration;
        private float _elapsed;
        private bool _flying;
        private bool _launched;

        protected override bool ReleaseWhenFinished => true;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_flying && !IsAnyParticleAlive();

        /// <summary>날아가는 부분.</summary>
        protected Transform Projectile => projectile;

        /// <summary>명중 연출의 루트. 없으면 null.</summary>
        protected ParticleSystem Impact => impact;

        /// <summary>발사 지점(월드).</summary>
        protected Vector3 LaunchPoint => _from;

        /// <summary>명중 지점(월드, hitPullback 적용 후).</summary>
        protected Vector3 HitPoint => _to;

        /// <summary>대상 칸 윗면 중심(월드, Place 에 넘긴 값).</summary>
        protected Vector3 TargetCell { get; private set; }

        /// <summary>날아가는 중인지(발사 전 대기 포함).</summary>
        protected bool IsFlying => _flying;

        /// <summary>Place 뒤 발사까지 기다리는 시간(초). 0 이면 바로 발사한다.</summary>
        public virtual float LaunchDelay => 0f;

        /// <summary>Place 뒤 지난 시간(초).</summary>
        protected float Elapsed => _elapsed;

        /// <summary>대상이 이 스킬로 밀려(당겨져) 도착한 칸 윗면 중심(월드). 밀리지 않았거나 모르면 null.</summary>
        protected Vector3? PushedTo { get; private set; }

        /// <summary>대상이 밀려 도착한 칸을 알려 준다(Place 다음에 호출). 밀린 자리에 연출을 이어 붙이는 이펙트가 쓴다.</summary>
        public void SetPushedTo(Vector3? cellTop)
        {
            PushedTo = cellTop;
        }

        private ParticleSystem[] ProjectileParticles =>
            _projectileParticles ?? (_projectileParticles = projectile.GetComponentsInChildren<ParticleSystem>(true));

        /// <summary>from → to 로 Place 했을 때 날아가는 시간(초). 프리팹에서 불러 발사 전에 도착 시점을 계산할 때 쓴다.</summary>
        public float GetFlightTime(Vector3 from, Vector3 to)
        {
            (Vector3 launch, Vector3 hit) = GetFlightPoints(from, to);
            return travelTime > 0f ? travelTime : (hit - launch).magnitude / speed;
        }

        /// <summary>from → to 로 Place 했을 때 명중까지 걸리는 시간(초, 발사 전 대기 + 날아가는 시간).</summary>
        public float GetArrivalTime(Vector3 from, Vector3 to) => LaunchDelay + GetFlightTime(from, to);

        /// <param name="from">시전자 칸 윗면 중심(월드).</param>
        /// <param name="to">대상 칸 윗면 중심(월드).</param>
        public void Place(Vector3 from, Vector3 to)
        {
            TargetCell = to;
            (_from, _to) = GetFlightPoints(from, to);
            Vector3 direction = _to - _from;
            _arc = arcPerDistance > 0f ? new Vector3(direction.x, 0f, direction.z).magnitude * arcPerDistance : arcHeight;

            Quaternion facing = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;

            transform.SetPositionAndRotation(_from, Quaternion.identity);
            projectile.SetPositionAndRotation(_from, facing);
            if (impact != null)
                impact.transform.SetPositionAndRotation(_to, facing * Quaternion.Euler(0f, 180f, 0f));

            _duration = GetFlightTime(from, to);
            PushedTo = null;
            _elapsed = 0f;
            _flying = true;
            _launched = false;
            if (LaunchDelay > 0f)
            {
                SetModelVisible(false);
                OnAimStarted();
            }
            else
            {
                Launch();
            }
        }

        private void Launch()
        {
            _launched = true;
            projectile.position = _from;
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
                if (!_launched)
                {
                    if (_elapsed < LaunchDelay)
                    {
                        base.Update();
                        return;
                    }
                    Launch();
                }

                float flight = _elapsed - LaunchDelay;
                float t = _duration > 0f ? Mathf.Clamp01(flight / _duration) : 1f;
                projectile.position = PointAt(t);
                if (_arc > 0f)
                {
                    // 궤적 접선(미분: 직선 성분 + 4h(1-2t) 위쪽)으로 머리를 돌린다.
                    Vector3 tangent = (_to - _from) + Vector3.up * (4f * _arc * (1f - 2f * t));
                    if (tangent.sqrMagnitude > 0.0001f)
                        projectile.rotation = Quaternion.LookRotation(tangent);
                }
                if (t >= 1f)
                    Arrive();
            }

            base.Update();
        }

        /// <summary>발사 지점과 명중 지점(hitPullback 적용 후).</summary>
        private (Vector3 Launch, Vector3 Hit) GetFlightPoints(Vector3 from, Vector3 to)
        {
            Vector3 launch = from + Vector3.up * launchHeight;
            Vector3 hit = to + Vector3.up * hitHeight;
            Vector3 direction = hit - launch;
            if (direction.magnitude > hitPullback)
                hit -= direction.normalized * hitPullback;
            return (launch, hit);
        }

        private Vector3 PointAt(float t) =>
            Vector3.Lerp(_from, _to, t) + Vector3.up * (4f * _arc * t * (1f - t));

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

        /// <summary>LaunchDelay 가 있을 때 Place 끝에 호출된다(발사 전 대기 시작, 조준 연출 등).</summary>
        protected virtual void OnAimStarted() { }

        /// <summary>발사 직후에 호출된다(LaunchDelay 가 없으면 Place 끝).</summary>
        protected virtual void OnLaunched() { }

        /// <summary>명중 지점에 도착해 impact 를 재생한 뒤 호출된다.</summary>
        protected virtual void OnArrived() { }
    }
}
