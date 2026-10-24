using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 실 관문(FX_ThreadGate). 재봉사 손에서 마법실 두 가닥(threadA/B)이 휘어지며 뻗어 출발 칸과 도착 칸에 닿고,
    /// 닿는 순간 두 칸 바닥에 실로 엮은 관문(gateFrom/gateTo)이 열린다. 유닛이 가라앉아 솟아오르는 동안 두 관문 사이에 실 한 줄(tether)이 이어지고,
    /// 관문은 실이 풀리듯 닫힌다(관문 파티클 수명). 실은 LineRenderer, 관문은 파티클이다.
    /// </summary>
    public sealed class ThreadGateEffect : WarpEffect
    {
        /// <summary>관문을 칸 윗면에서 띄우는 높이(바닥과 겹쳐 깜빡이지 않게).</summary>
        private const float GateLift = 0.03f;

        [SerializeField] private LineRenderer threadA;
        [SerializeField] private LineRenderer threadB;
        [SerializeField] private LineRenderer tether;
        [Tooltip("출발 칸 관문(하위 파티클 포함). 실이 닿을 때 재생한다.")]
        [SerializeField] private ParticleSystem gateFrom;
        [Tooltip("도착 칸 관문(하위 파티클 포함). 실이 닿을 때 재생한다.")]
        [SerializeField] private ParticleSystem gateTo;

        [Tooltip("실이 손에서 칸까지 뻗는 시간(초).")]
        [SerializeField, Min(0.01f)] private float threadTime = 0.2f;
        [Tooltip("실이 닿은 뒤 사라지기까지의 시간(초).")]
        [SerializeField, Min(0f)] private float threadHold = 0.25f;
        [SerializeField, Min(0.01f)] private float threadFade = 0.2f;
        [Tooltip("손 높이(시전자 칸 윗면 기준).")]
        [SerializeField] private float handHeight = 0.7f;
        [Tooltip("실이 위로 휘어지는 높이.")]
        [SerializeField] private float threadArc = 0.5f;
        [Tooltip("관문 사이 실이 위로 휘어지는 높이.")]
        [SerializeField] private float tetherArc = 0.35f;
        [SerializeField, Min(0.01f)] private float tetherFade = 0.15f;
        [SerializeField, Range(2, 32)] private int segments = 16;

        private Vector3 _hand;
        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private bool _playing;
        private bool _gatesOpened;
        private Color _threadStart, _threadEnd, _tetherStart, _tetherEnd;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        private float TetherStart => SinkDelay;
        private float TetherEnd => SinkDelay + SinkTime + RiseTime;

        private void Awake()
        {
            _threadStart = threadA.startColor;
            _threadEnd = threadA.endColor;
            _tetherStart = tether.startColor;
            _tetherEnd = tether.endColor;
            foreach (LineRenderer line in new[] { threadA, threadB, tether })
            {
                line.positionCount = segments + 1;
                line.enabled = false;
            }
        }

        public override void Place(Vector3 caster, Vector3 from, Vector3 to)
        {
            _hand = caster + Vector3.up * handHeight;
            _from = from;
            _to = to;
            transform.SetPositionAndRotation(caster, Quaternion.identity);
            gateFrom.transform.SetPositionAndRotation(from + Vector3.up * GateLift, Quaternion.identity);
            gateTo.transform.SetPositionAndRotation(to + Vector3.up * GateLift, Quaternion.identity);

            _elapsed = 0f;
            _playing = true;
            _gatesOpened = false;
            Apply();
        }

        protected override void OnReleased()
        {
            _playing = false;
            threadA.enabled = false;
            threadB.enabled = false;
            tether.enabled = false;
        }

        protected override void Update()
        {
            if (_playing)
            {
                _elapsed += Time.deltaTime;
                Apply();

                if (!_gatesOpened && _elapsed >= threadTime)
                {
                    _gatesOpened = true;
                    gateFrom.Play(true);
                    gateTo.Play(true);
                }

                if (_elapsed >= Mathf.Max(TetherEnd + tetherFade, threadTime + threadHold + threadFade))
                {
                    _playing = false;
                    threadA.enabled = false;
                    threadB.enabled = false;
                    tether.enabled = false;
                }
            }

            base.Update();
        }

        private void Apply()
        {
            // 손 → 두 칸: 앞쪽 끝이 뻗어 나가고, 닿은 뒤 머물다 흐려진다.
            float reach = Mathf.Clamp01(_elapsed / threadTime);
            reach = 1f - (1f - reach) * (1f - reach);
            float threadAlpha = 1f - Mathf.Clamp01((_elapsed - threadTime - threadHold) / threadFade);
            DrawArc(threadA, _hand, _from, threadArc, reach, threadAlpha, _threadStart, _threadEnd);
            DrawArc(threadB, _hand, _to, threadArc, reach, threadAlpha, _threadStart, _threadEnd);

            // 두 관문 사이: 유닛이 지나가는 동안 팽팽하게 이어졌다가 흐려진다.
            float tetherAlpha = _elapsed < TetherStart ? 0f : 1f - Mathf.Clamp01((_elapsed - TetherEnd) / tetherFade);
            DrawArc(tether, _from + Vector3.up * 0.05f, _to + Vector3.up * 0.05f, tetherArc, 1f, tetherAlpha, _tetherStart, _tetherEnd);
        }

        // a → b 를 위로 휘어진 곡선(2차 베지어)으로 그린다. reach 만큼만 뻗은 모양.
        private void DrawArc(LineRenderer line, Vector3 a, Vector3 b, float arc, float reach, float alpha, Color start, Color end)
        {
            line.enabled = alpha > 0f && reach > 0f;
            if (!line.enabled)
                return;

            Vector3 control = (a + b) * 0.5f + Vector3.up * arc;
            for (int i = 0; i <= segments; i++)
            {
                float t = reach * i / segments;
                float u = 1f - t;
                line.SetPosition(i, u * u * a + 2f * u * t * control + t * t * b);
            }

            start.a *= alpha;
            end.a *= alpha;
            line.startColor = start;
            line.endColor = end;
        }
    }
}
