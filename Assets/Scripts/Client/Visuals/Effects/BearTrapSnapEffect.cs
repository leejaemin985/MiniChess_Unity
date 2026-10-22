using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 속박 덫 발동(FX_BearTrapSnap). 펼친 곰덫이 snapTime 동안 "철컥" 닫히며 살짝 튕기고, hold 동안 머문 뒤 작아지며 사라진다.
    /// 닫히는 순간의 금속 불티/먼지는 파티클의 시작 지연(snapTime 근처)으로 맞춘다.
    /// </summary>
    public sealed class BearTrapSnapEffect : TriggerEffect
    {
        [SerializeField] private BearTrapJaws jaws;
        [Tooltip("곰덫 모델 루트(사라질 때 크기를 줄인다).")]
        [SerializeField] private Transform model;

        [Tooltip("턱이 다 닫히기까지 시간(초).")]
        [SerializeField, Min(0.01f)] private float snapTime = 0.07f;
        [Tooltip("닫힌 뒤 머무는 시간(초).")]
        [SerializeField, Min(0f)] private float hold = 0.9f;
        [Tooltip("작아지며 사라지는 시간(초).")]
        [SerializeField, Min(0.01f)] private float shrinkTime = 0.25f;
        [Tooltip("닫힌 직후 튕기는 정도(닫힘 비율).")]
        [SerializeField, Range(0f, 0.3f)] private float bounce = 0.08f;

        private Vector3 _modelScale = Vector3.one;
        private float _elapsed;
        private bool _playing;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        private void Awake()
        {
            if (model != null)
                _modelScale = model.localScale;
        }

        protected override void OnPlaced()
        {
            _elapsed = 0f;
            _playing = true;
            Apply();
        }

        protected override void OnReleased()
        {
            _playing = false;
        }

        protected override void Update()
        {
            if (_playing)
            {
                _elapsed += Time.deltaTime;
                Apply();
                if (_elapsed >= snapTime + hold + shrinkTime)
                    _playing = false;
            }

            base.Update();
        }

        private void Apply()
        {
            float closedness;
            if (_elapsed < snapTime)
            {
                float t = _elapsed / snapTime;
                closedness = t * t;
            }
            else
            {
                // 맞부딪혀 조금 벌어졌다가 다시 닫힌다(감쇠 진동).
                float after = _elapsed - snapTime;
                closedness = 1f - bounce * Mathf.Sin(after * 40f) * Mathf.Exp(-after * 18f);
            }

            if (jaws != null)
                jaws.SetClosedness(closedness);

            if (model != null)
            {
                float shrink = Mathf.Clamp01((_elapsed - snapTime - hold) / shrinkTime);
                model.localScale = _modelScale * (1f - shrink * shrink);
            }
        }
    }
}
