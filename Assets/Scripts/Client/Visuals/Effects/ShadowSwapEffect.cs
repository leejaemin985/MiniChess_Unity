using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 분신 교환(FX_ShadowSwap). 두 칸 바닥에 그림자 웅덩이(poolA/poolB)가 퍼지고, 두 유닛이 각자 웅덩이로 가라앉는 동안
    /// 그림자 줄기 둘(streamA: from → to, streamB: to → from)이 바닥을 타고 엇갈려 지나가며, 솟아오르는 순간 두 칸에서 보라 연기가 솟구친다(burstA/burstB).
    /// 교환은 한 쌍이라 Place 의 from/to 는 두 유닛의 칸이고 시전자 자리는 쓰지 않는다. 유닛 가라앉기/솟아오르기 시간은 WarpEffect 값을 따른다.
    /// </summary>
    public sealed class ShadowSwapEffect : WarpEffect
    {
        [SerializeField] private ParticleSystem poolA;
        [SerializeField] private ParticleSystem poolB;
        [Tooltip("from 칸 → to 칸으로 미끄러지는 그림자 줄기(하위 연기 꼬리는 월드 시뮬레이션).")]
        [SerializeField] private ParticleSystem streamA;
        [Tooltip("to 칸 → from 칸으로 미끄러지는 그림자 줄기.")]
        [SerializeField] private ParticleSystem streamB;
        [SerializeField] private ParticleSystem burstA;
        [SerializeField] private ParticleSystem burstB;
        [Tooltip("줄기가 휘어 지나가는 옆 폭(월드). 두 줄기가 서로 반대쪽으로 휘어 엇갈린다.")]
        [SerializeField] private float streamCurve = 0.35f;

        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private bool _playing;
        private bool _streaming;
        private bool _burst;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        /// <summary>줄기는 유닛이 다 가라앉을 무렵 출발해 솟아오르기 시작할 때 닿는다.</summary>
        private float StreamStart => SinkDelay + SinkTime * 0.5f;
        private float StreamEnd => SinkDelay + SinkTime;

        public override void Place(Vector3 caster, Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
            transform.SetPositionAndRotation(from, Quaternion.identity);
            poolA.transform.position = from;
            poolB.transform.position = to;
            burstA.transform.position = from;
            burstB.transform.position = to;
            streamA.transform.position = from;
            streamB.transform.position = to;

            _elapsed = 0f;
            _playing = true;
            _streaming = false;
            _burst = false;
            poolA.Play(true);
            poolB.Play(true);
        }

        protected override void OnReleased()
        {
            _playing = false;
        }

        protected override void Update()
        {
            if (_playing)
                Step();

            base.Update();
        }

        private void Step()
        {
            _elapsed += Time.deltaTime;

            if (!_streaming && _elapsed >= StreamStart)
            {
                _streaming = true;
                streamA.Play(true);
                streamB.Play(true);
            }

            if (_streaming)
            {
                float t = Mathf.Clamp01((_elapsed - StreamStart) / (StreamEnd - StreamStart));
                float eased = t * t * (3f - 2f * t);
                Vector3 side = Vector3.Cross(Vector3.up, _to - _from).normalized * (streamCurve * Mathf.Sin(Mathf.PI * eased));
                streamA.transform.position = Vector3.Lerp(_from, _to, eased) + side;
                streamB.transform.position = Vector3.Lerp(_to, _from, eased) - side;
            }

            if (!_burst && _elapsed >= StreamEnd)
            {
                _burst = true;
                streamA.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                streamB.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                burstA.Play(true);
                burstB.Play(true);
            }

            if (_elapsed >= StreamEnd + RiseTime)
                _playing = false;
        }
    }
}
