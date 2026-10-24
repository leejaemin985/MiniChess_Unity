using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 단일 보호막 시전(FX_ShieldBloom). 발밑 초록 고리와 함께 잎사귀가 몸 둘레를 나선으로 감아오르고(파티클),
    /// popDelay 뒤 몸을 덮는 막(bubble)이 튕기듯 커지며 나타나 빛 띠가 아래에서 위로 한 번 훑은 뒤 서서히 사라진다.
    /// 막은 MiniChess/ShieldBubble 셰이더를 쓰고, 세기(_Alpha)와 빛 띠 위치(_Sweep)를 MaterialPropertyBlock 으로 움직인다.
    /// </summary>
    public sealed class ShieldBloomEffect : HitEffect
    {
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");

        [Tooltip("막 렌더러(구체). 크기는 프리팹에 둔 크기가 다 펼쳐진 크기다.")]
        [SerializeField] private Renderer bubble;

        [Tooltip("잎사귀가 감아오른 뒤 막이 펼쳐지기 시작하는 시간(초).")]
        [SerializeField, Min(0f)] private float popDelay = 0.3f;
        [Tooltip("막이 펼쳐지는 시간(초).")]
        [SerializeField, Min(0.01f)] private float popTime = 0.15f;
        [Tooltip("펼쳐질 때 넘쳤다 돌아오는 정도.")]
        [SerializeField, Range(0f, 0.5f)] private float overshoot = 0.15f;
        [Tooltip("빛 띠가 아래에서 위로 훑는 시간(초). 다 펼쳐진 뒤 시작한다.")]
        [SerializeField, Min(0.01f)] private float sweepTime = 0.35f;
        [Tooltip("훑은 뒤 사라지는 시간(초).")]
        [SerializeField, Min(0.01f)] private float fadeTime = 0.35f;

        private MaterialPropertyBlock _block;
        private Vector3 _bubbleScale = Vector3.one;
        private float _elapsed;
        private bool _playing;

        private float Total => popDelay + popTime + sweepTime + fadeTime;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (bubble != null)
                _bubbleScale = bubble.transform.localScale;
        }

        protected override void OnSpawned()
        {
            _elapsed = 0f;
            _playing = true;
            Apply();
        }

        protected override void OnReleased()
        {
            _playing = false;
            if (bubble != null)
                bubble.enabled = false;
        }

        protected override void Update()
        {
            if (_playing)
            {
                _elapsed += Time.deltaTime;
                Apply();
                if (_elapsed >= Total)
                {
                    _playing = false;
                    if (bubble != null)
                        bubble.enabled = false;
                }
            }

            base.Update();
        }

        private void Apply()
        {
            if (bubble == null)
                return;

            float t = _elapsed - popDelay;
            bubble.enabled = t >= 0f;
            if (t < 0f)
                return;

            // 펼쳐짐: 0 에서 커지며 살짝 넘쳤다가 원래 크기로 돌아온다.
            float pop = Mathf.Clamp01(t / popTime);
            bubble.transform.localScale = _bubbleScale * EaseOutBack(pop);

            float afterPop = t - popTime;
            float sweep = afterPop < 0f ? -0.2f : Mathf.Lerp(-0.2f, 1.2f, afterPop / sweepTime);
            float fade = Mathf.Clamp01((afterPop - sweepTime) / fadeTime);
            float alpha = pop < 1f ? 0.4f + 0.6f * pop : 1f - fade * fade;

            bubble.GetPropertyBlock(_block);
            _block.SetFloat(AlphaId, alpha);
            _block.SetFloat(SweepId, sweep);
            bubble.SetPropertyBlock(_block);
        }

        // overshoot 0.15 이면 약 8% 넘친다.
        private float EaseOutBack(float x)
        {
            float c1 = overshoot * 10f;
            float c3 = c1 + 1f;
            float y = x - 1f;
            return 1f + c3 * y * y * y + c1 * y * y;
        }
    }
}
