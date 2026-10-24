using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 호위 시전(FX_GuardLink). 푸른 빛(projectile)이 호위자에서 보호 대상으로 날아가며 사슬 줄(tether)을 잇고,
    /// 도착하면 보호 대상 머리 위에 방패 문양(impact)이 찍힌다. 줄은 tetherHold 동안 남았다가 tetherFade 동안 사라진다.
    /// </summary>
    public sealed class GuardLinkEffect : ProjectileEffect
    {
        [Tooltip("호위자 → 빛 머리를 잇는 사슬 줄(월드 좌표, 점 2개).")]
        [SerializeField] private LineRenderer tether;
        [SerializeField, Min(0f)] private float tetherHold = 0.35f;
        [SerializeField, Min(0.01f)] private float tetherFade = 0.3f;

        private Color _startColor;
        private Color _endColor;
        private float _afterArrive;
        private bool _linked;

        protected override bool IsFinished => base.IsFinished && !_linked;

        private void Awake()
        {
            _startColor = tether.startColor;
            _endColor = tether.endColor;
            tether.enabled = false;
        }

        protected override void OnLaunched()
        {
            _linked = true;
            _afterArrive = -1f;
            SetTether(LaunchPoint, Projectile.position, 1f);
        }

        protected override void OnArrived()
        {
            _afterArrive = 0f;
        }

        protected override void OnReleased()
        {
            base.OnReleased();
            _linked = false;
            tether.enabled = false;
        }

        protected override void Update()
        {
            base.Update();
            if (!_linked)
                return;

            if (IsFlying)
            {
                SetTether(LaunchPoint, Projectile.position, 1f);
                return;
            }

            _afterArrive += Time.deltaTime;
            float fade = Mathf.Clamp01((_afterArrive - tetherHold) / tetherFade);
            SetTether(LaunchPoint, HitPoint, 1f - fade);
            if (fade >= 1f)
            {
                _linked = false;
                tether.enabled = false;
            }
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
