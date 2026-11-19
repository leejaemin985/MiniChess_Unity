using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 장거리 조준(FX_AimedShot). 시전자 → 대상 칸:
    ///   - 조준(aimTime) + 고정(lockTime): 대상 몸통 앞에 조준 문양(reticle)이 조여들고, 함께 활 → 명중 지점으로 레이저(laser)와 조준점(laserDot)이
    ///     깜빡이다 밝고 가늘게 팽팽해지며 조준점 떨림이 멈춘다. 이 시간이 LaunchDelay 다(그동안 화살은 숨긴다).
    ///   - 발사: 레이저/조준점이 꺼지고 화살 모델이 빛줄기(tracer 트레일)를 끌며 빠르게 날아간다.
    ///   - 명중: impact(불티, 카메라 쪽으로 당겨 놓는다)를 재생하고 조준 문양을 조각내 흩어 깨뜨린다.
    /// 문양/레이저/조준점은 깊이를 무시하는 스프라이트 셰이더(SpriteAdditive)로 캐릭터 위에 그린다.
    /// </summary>
    public sealed class AimedShotEffect : ProjectileEffect
    {
        [Header("Aim")]
        [SerializeField] private SnipeReticle reticle;
        [Tooltip("조준 문양 중심 높이(대상 칸 윗면 위). 대상 몸통 가운데쯤에 둔다.")]
        [SerializeField] private float reticleHeight = 0.55f;
        [Tooltip("조준 문양이 조여드는 시간(초).")]
        [SerializeField, Min(0f)] private float aimTime = 0.4f;
        [Tooltip("문양이 다 조여든 뒤 발사까지 레이저로 겨누는 시간(초).")]
        [SerializeField, Min(0f)] private float lockTime = 0.35f;

        [Header("Laser")]
        [SerializeField] private LineRenderer laser;
        [SerializeField] private SpriteRenderer laserDot;
        [Tooltip("레이저 굵기: 조준 시작 → 발사 직전.")]
        [SerializeField] private Vector2 laserWidth = new Vector2(0.035f, 0.012f);
        [Tooltip("레이저/조준점 알파: 조준 시작 → 발사 직전.")]
        [SerializeField] private Vector2 laserAlpha = new Vector2(0.25f, 1f);
        [Tooltip("앞부분(이 비율까지) 동안 깜빡이는 속도(회/초).")]
        [SerializeField, Min(0f)] private float flickerRate = 30f;
        [SerializeField, Range(0f, 1f)] private float flickerUntil = 0.65f;
        [Tooltip("조준점이 떨리는 폭(월드 단위). 발사 직전으로 갈수록 0 이 된다.")]
        [SerializeField, Min(0f)] private float dotJitter = 0.03f;

        [Header("Flight")]
        [Tooltip("화살이 끄는 빛줄기. projectile 아래에 둔다.")]
        [SerializeField] private TrailRenderer tracer;
        [Tooltip("명중 연출(불티)을 카메라 쪽으로 당기는 거리. 대상 몸통에 가려지지 않게 한다.")]
        [SerializeField, Min(0f)] private float impactTowardCamera = 0.6f;

        private Color _laserColor = Color.white;
        private Color _dotColor = Color.white;
        private bool _aiming;

        public override float LaunchDelay => aimTime + lockTime;

        protected override bool IsFinished => base.IsFinished && (reticle == null || !reticle.IsVisible);

        private void Awake()
        {
            if (laser != null)
                _laserColor = laser.startColor;
            if (laserDot != null)
                _dotColor = laserDot.color;
            SetLaserVisible(false);
        }

        protected override void OnAimStarted()
        {
            if (reticle != null)
            {
                reticle.transform.position = TargetCell + Vector3.up * reticleHeight;
                reticle.Show();
            }

            if (laser != null)
            {
                laser.positionCount = 2;
                laser.SetPosition(0, LaunchPoint);
                laser.SetPosition(1, HitPoint);
            }

            if (Impact != null)
            {
                Camera view = Camera.main;
                if (view != null)
                    Impact.transform.position = HitPoint + (view.transform.position - HitPoint).normalized * impactTowardCamera;
            }

            if (tracer != null)
            {
                tracer.emitting = false;
                tracer.Clear();
            }

            _aiming = true;
            SetLaserVisible(true);
            EvaluateLaser(0f);
        }

        protected override void OnLaunched()
        {
            _aiming = false;
            SetLaserVisible(false);
            if (tracer != null)
            {
                // 풀에서 다시 꺼낼 때 이전 궤적에서 이어 그려지지 않게 한다.
                tracer.Clear();
                tracer.emitting = true;
            }
        }

        protected override void OnArrived()
        {
            if (tracer != null)
                tracer.emitting = false;
            if (reticle != null)
                reticle.Shatter();
        }

        protected override void OnReleased()
        {
            base.OnReleased();
            _aiming = false;
            SetLaserVisible(false);
            if (tracer != null)
                tracer.emitting = false;
            if (reticle != null)
                reticle.Hide();
        }

        protected override void Update()
        {
            base.Update();
            if (_aiming)
                EvaluateLaser(LaunchDelay > 0f ? Elapsed / LaunchDelay : 1f);
        }

        /// <param name="t">조준 + 고정 진행도(0 → 1).</param>
        private void EvaluateLaser(float t)
        {
            t = Mathf.Clamp01(t);
            // 앞부분은 깜빡이고, 뒤로 갈수록 밝고 가늘게 팽팽해진다.
            float flicker = t < flickerUntil && Mathf.Repeat(Elapsed * flickerRate, 1f) < 0.35f ? 0.35f : 1f;
            float alpha = Mathf.Lerp(laserAlpha.x, laserAlpha.y, t * t) * flicker;
            if (laser != null)
            {
                laser.widthMultiplier = Mathf.Lerp(laserWidth.x, laserWidth.y, t);
                Color color = _laserColor;
                color.a *= alpha;
                laser.startColor = color;
                laser.endColor = color;
            }

            if (laserDot != null)
            {
                Camera view = Camera.main;
                Vector2 jitter = Random.insideUnitCircle * (dotJitter * (1f - t));
                Vector3 offset = view != null ? view.transform.right * jitter.x + view.transform.up * jitter.y : Vector3.zero;
                laserDot.transform.position = HitPoint + offset;
                if (view != null)
                    laserDot.transform.rotation = view.transform.rotation;
                Color color = _dotColor;
                color.a *= alpha;
                laserDot.color = color;
            }
        }

        private void SetLaserVisible(bool visible)
        {
            if (laser != null)
                laser.enabled = visible;
            if (laserDot != null)
                laserDot.enabled = visible;
        }
    }
}
