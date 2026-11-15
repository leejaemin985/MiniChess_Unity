using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 전사 돌진 경로(FX_WarriorDashPath). from → to 로 미끄러지는 동안:
    ///   - Start: 출발 칸에서 박차고 나가는 먼지(자식 파티클, Place 때 재생).
    ///   - path: 경로 전체에 놓는 속도선, 파편. 경로 가운데에 놓이고 Z 크기를 경로 길이 / pathReferenceLength 로 늘린다.
    ///   - wake: 경로를 덮는 바닥 띠. 머리(_Head)가 travelTime 동안 출발 → 도착으로 가며 지나간 곳이 왜곡으로 일렁이고, 도착 뒤 lingerTime 동안 사라진다.
    ///   - smoke: 머리를 따라 경로에 검은 연기를 뿌린다(자체 방출 없음, Emit).
    /// 루트는 from 에 놓이고 +Z 가 돌진 방향이다. travelTime 은 UnitsView.dashDuration 에 맞춘다.
    /// </summary>
    public sealed class DashPathEffect : LineEffect
    {
        [SerializeField] private Renderer wake;
        [SerializeField] private ParticleSystem smoke;
        [Tooltip("경로 전체에 놓는 파티클의 부모(속도선, 파편).")]
        [SerializeField] private Transform path;
        [Tooltip("프리팹을 만든 기준 경로 길이(칸 3개 = 3).")]
        [SerializeField, Min(0.1f)] private float pathReferenceLength = 3f;
        [Tooltip("머리가 출발 → 도착으로 가는 시간(초). 돌진 이동 시간과 맞춘다.")]
        [SerializeField, Min(0.01f)] private float travelTime = 0.15f;
        [Tooltip("도착 뒤 바닥 띠가 사라지는 시간(초).")]
        [SerializeField, Min(0.01f)] private float lingerTime = 0.4f;
        [SerializeField] private float wakeWidth = 1f;
        [SerializeField] private float wakeHeight = 0.04f;
        [SerializeField] private float smokePerUnit = 8f;
        [SerializeField] private float smokeHeight = 0.25f;

        private static readonly int HeadId = Shader.PropertyToID("_Head");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private MaterialPropertyBlock _block;
        private float _length;
        private float _elapsed;
        private float _smokeCarry;
        private bool _playing;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        public override void Place(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            _length = direction.magnitude;
            transform.SetPositionAndRotation(from, _length > 0.0001f ? Quaternion.LookRotation(direction / _length) : Quaternion.identity);

            if (wake != null)
            {
                // 쿼드의 X(UV x)가 경로(+Z), Y 가 폭(X)이 되게 눕힌다.
                wake.transform.localPosition = new Vector3(0f, wakeHeight, _length * 0.5f);
                wake.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
                wake.transform.localScale = new Vector3(Mathf.Max(_length, 0.01f), wakeWidth, 1f);
            }

            if (path != null)
            {
                path.localPosition = new Vector3(0f, 0f, _length * 0.5f);
                path.localScale = new Vector3(1f, 1f, Mathf.Max(_length, 0.01f) / pathReferenceLength);
            }

            foreach (ParticleSystem particle in Particles)
            {
                particle.Clear(false);
                if (particle != smoke)
                    particle.Play(false);
            }

            _elapsed = 0f;
            _smokeCarry = 0f;
            _playing = true;
            Evaluate();
        }

        protected override void OnReleased()
        {
            _playing = false;
            if (wake != null)
                wake.enabled = false;
        }

        protected override void Update()
        {
            if (_playing)
            {
                float previousHead = Head(_elapsed);
                _elapsed += Time.deltaTime;
                EmitSmoke(previousHead, Head(_elapsed));
                Evaluate();
                if (_elapsed >= travelTime + lingerTime)
                    _playing = false;
            }

            base.Update();
        }

        private float Head(float time)
        {
            float t = Mathf.Clamp01(time / travelTime);
            return 1f - (1f - t) * (1f - t);
        }

        private void Evaluate()
        {
            if (wake == null)
                return;

            _block ??= new MaterialPropertyBlock();
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_elapsed - travelTime) / lingerTime));
            _block.SetFloat(HeadId, Head(_elapsed));
            _block.SetFloat(FadeId, fade);
            wake.SetPropertyBlock(_block);
            wake.enabled = fade > 0.001f;
        }

        private void EmitSmoke(float fromHead, float toHead)
        {
            if (smoke == null || toHead <= fromHead)
                return;

            _smokeCarry += (toHead - fromHead) * _length * smokePerUnit;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            while (_smokeCarry >= 1f)
            {
                _smokeCarry -= 1f;
                float along = Random.Range(fromHead, toHead) * _length;
                emit.position = transform.position + transform.rotation * new Vector3(0f, smokeHeight, along);
                smoke.Emit(emit, 1);
            }
        }
    }
}
