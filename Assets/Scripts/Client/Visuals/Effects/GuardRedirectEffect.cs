using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 호위 반응: 보호 대상이 맞을 피해를 호위자가 대신 받음(FX_GuardRedirect). StatusVisual(GUARD) 의 반응 이펙트로 쓴다.
    /// 보호 대상 앞에 방패 문양이 번쩍이며 막아내고(block), boltDelay 뒤 푸른 빛(bolt)이 사슬 줄(tether)을 끌며 호위자에게 날아가
    /// 호위자 자리에서 피격 섬광(hit)이 터진다. 줄은 도착 뒤 tetherFade 동안 사라진다.
    /// </summary>
    public sealed class GuardRedirectEffect : LineEffect
    {
        [Tooltip("보호 대상 자리에서 바로 재생(방패 문양 + 불티).")]
        [SerializeField] private ParticleSystem block;
        [Tooltip("날아가는 빛(하위 파티클 포함, 꼬리는 World 시뮬레이션).")]
        [SerializeField] private Transform bolt;
        [Tooltip("보호 대상 → 빛을 잇는 사슬 줄(월드 좌표, 점 2개).")]
        [SerializeField] private LineRenderer tether;
        [Tooltip("호위자 자리에서 빛이 도착할 때 재생.")]
        [SerializeField] private ParticleSystem hit;

        [Tooltip("넘겨받은 칸 윗면에서 연출 높이(몸 중심).")]
        [SerializeField] private float height = 0.55f;
        [SerializeField, Min(0f)] private float boltDelay = 0.12f;
        [SerializeField, Min(0.01f)] private float travelTime = 0.16f;
        [SerializeField, Min(0.01f)] private float tetherFade = 0.3f;

        private ParticleSystem[] _boltParticles;
        private Color _startColor;
        private Color _endColor;
        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private bool _playing;
        private bool _boltLaunched;
        private bool _arrived;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        private void Awake()
        {
            _boltParticles = bolt.GetComponentsInChildren<ParticleSystem>(true);
            _startColor = tether.startColor;
            _endColor = tether.endColor;
            tether.enabled = false;
        }

        /// <param name="from">보호 대상 칸 윗면(월드).</param>
        /// <param name="to">호위자 칸 윗면(월드).</param>
        public override void Place(Vector3 from, Vector3 to)
        {
            _from = from + Vector3.up * height;
            _to = to + Vector3.up * height;
            transform.SetPositionAndRotation(_from, Quaternion.identity);
            block.transform.position = _from;
            hit.transform.position = _to;
            Vector3 direction = _to - _from;
            bolt.SetPositionAndRotation(_from, direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity);

            _elapsed = 0f;
            _playing = true;
            _boltLaunched = false;
            _arrived = false;
            tether.enabled = false;
            block.Play(true);
        }

        protected override void OnReleased()
        {
            _playing = false;
            tether.enabled = false;
        }

        protected override void Update()
        {
            if (_playing)
            {
                _elapsed += Time.deltaTime;
                float flight = (_elapsed - boltDelay) / travelTime;

                if (!_boltLaunched && flight >= 0f)
                {
                    _boltLaunched = true;
                    foreach (ParticleSystem particle in _boltParticles)
                        particle.Play(false);
                }

                if (_boltLaunched)
                {
                    float t = Mathf.Clamp01(flight);
                    bolt.position = Vector3.Lerp(_from, _to, 1f - (1f - t) * (1f - t));
                    float fade = Mathf.Clamp01((flight - 1f) * travelTime / tetherFade);
                    SetTether(_from, bolt.position, 1f - fade);

                    if (!_arrived && t >= 1f)
                    {
                        _arrived = true;
                        foreach (ParticleSystem particle in _boltParticles)
                            particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                        hit.Play(true);
                    }

                    if (fade >= 1f)
                    {
                        _playing = false;
                        tether.enabled = false;
                    }
                }
            }

            base.Update();
        }

        private void SetTether(Vector3 from, Vector3 to, float alpha)
        {
            tether.enabled = alpha > 0f;
            tether.SetPosition(0, from);
            tether.SetPosition(1, to);
            tether.startColor = WithAlpha(_startColor, _startColor.a * alpha);
            tether.endColor = WithAlpha(_endColor, _endColor.a * alpha);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
