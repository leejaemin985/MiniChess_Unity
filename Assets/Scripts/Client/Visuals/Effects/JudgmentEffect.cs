using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 심판(FX_Judgment). 시전자 쪽 축(pivot)에 거대한 십자가 둔기(model)가 나타나 옆에서 크게 휘둘러 대상을 쳐낸다.
    /// 둔기 머리의 파티클(swingTrail)이 휘두른 호를 남기고, 닿는 순간(appearTime + swingTime) impact 가 터진다.
    /// 그 뒤 조금 더 휘둘러지고(followAngle) 작아지며 빛가루(vanish)로 흩어진다.
    /// 닿는 시점은 밀려남 출발 지연(UnitsView.knockbackDelay)과 맞춘다. 프리팹은 +Z 가 밀어내는 방향(시전자 → 대상)이다.
    /// </summary>
    public sealed class JudgmentEffect : HitEffect
    {
        [Tooltip("휘두르는 축. 시전자 쪽(로컬 -Z)에 두고, Y 축으로 돈다.")]
        [SerializeField] private Transform pivot;
        [Tooltip("둔기 모델(pivot 아래, +Z 로 뻗는다).")]
        [SerializeField] private Transform model;
        [Tooltip("휘두르는 동안 방출하는 둔기 머리 파티클(World 시뮬레이션).")]
        [SerializeField] private ParticleSystem swingTrail;
        [Tooltip("닿는 순간 재생(대상 자리).")]
        [SerializeField] private ParticleSystem impact;
        [Tooltip("사라질 때 둔기 머리 자리에서 재생.")]
        [SerializeField] private ParticleSystem vanish;
        [Tooltip("vanish 를 놓을 둔기 머리 위치.")]
        [SerializeField] private Transform head;

        [Tooltip("휘두르기 시작 각도(도, 0 이 대상을 정면으로 가리킴).")]
        [SerializeField] private float startAngle = -115f;
        [SerializeField] private float followAngle = 30f;
        [SerializeField, Min(0.01f)] private float appearTime = 0.05f;
        [SerializeField, Min(0.01f)] private float swingTime = 0.1f;
        [SerializeField, Min(0.01f)] private float followTime = 0.1f;
        [SerializeField, Min(0.01f)] private float fadeTime = 0.15f;

        private Vector3 _modelScale = Vector3.one;
        private float _elapsed;
        private bool _playing;
        private bool _hit;
        private bool _vanished;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        private float ContactTime => appearTime + swingTime;

        private void Awake()
        {
            _modelScale = model.localScale;
        }

        protected override void OnSpawned()
        {
            _elapsed = 0f;
            _playing = true;
            _hit = false;
            _vanished = false;
            model.gameObject.SetActive(true);
            swingTrail.Play(false);
            Apply();
        }

        protected override void OnReleased()
        {
            _playing = false;
            model.gameObject.SetActive(false);
        }

        protected override void Update()
        {
            if (_playing)
            {
                _elapsed += Time.deltaTime;
                Apply();

                if (!_hit && _elapsed >= ContactTime)
                {
                    _hit = true;
                    impact.Play(true);
                }

                if (!_vanished && _elapsed >= ContactTime + followTime)
                {
                    _vanished = true;
                    swingTrail.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    vanish.transform.position = head.position;
                    vanish.Play(true);
                }

                if (_elapsed >= ContactTime + followTime + fadeTime)
                {
                    _playing = false;
                    model.gameObject.SetActive(false);
                }
            }

            base.Update();
        }

        private void Apply()
        {
            float angle;
            if (_elapsed < appearTime)
            {
                angle = startAngle;
            }
            else if (_elapsed < ContactTime)
            {
                // 점점 빨라지며 내리친다.
                float t = (_elapsed - appearTime) / swingTime;
                angle = Mathf.Lerp(startAngle, 0f, t * t);
            }
            else
            {
                // 맞은 뒤 감속하며 조금 더 돈다.
                float t = Mathf.Clamp01((_elapsed - ContactTime) / followTime);
                angle = Mathf.Lerp(0f, followAngle, 1f - (1f - t) * (1f - t));
            }

            pivot.localRotation = Quaternion.Euler(0f, angle, 0f);

            float grow = Mathf.Clamp01(_elapsed / appearTime);
            float shrink = Mathf.Clamp01((_elapsed - ContactTime - followTime) / fadeTime);
            model.localScale = _modelScale * (Mathf.SmoothStep(0.3f, 1f, grow) * (1f - shrink * shrink));
        }
    }
}
