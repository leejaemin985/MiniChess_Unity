using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 회복 초원 장판(FX_GrassField). 칸을 덮는 빛나는 풀(grass, 메시)과 그 위로 떠오르는 빛 점(파티클).
    /// 깔릴 때 풀이 바닥에서 자라 오르고(growTime), 걷힐 때(Release) 바닥으로 시든다(witherTime).
    /// 빛 점은 방출만 멈추고 남은 것이 사라지면 반납되므로, witherTime 은 빛 점 수명보다 짧게 둔다.
    /// </summary>
    public sealed class GrassFieldEffect : FieldEffect
    {
        [Tooltip("자라나고 시드는 풀(칸을 덮는 메시). 높이(Y) 크기만 바꾼다.")]
        [SerializeField] private Transform grass;
        [Tooltip("풀이 다 자라는 시간(초).")]
        [SerializeField, Min(0.01f)] private float growTime = 0.35f;
        [Tooltip("풀이 다 시드는 시간(초).")]
        [SerializeField, Min(0.01f)] private float witherTime = 0.4f;

        private Vector3 _grassScale = Vector3.one;
        private float _height;
        private float _from;
        private float _to;
        private float _elapsed;
        private float _duration;

        private void Awake()
        {
            if (grass != null)
                _grassScale = grass.localScale;
        }

        protected override void OnSpawned()
        {
            _height = 0f;
            Animate(1f, growTime);
        }

        protected override void OnReleased()
        {
            Animate(0f, witherTime);
        }

        protected override void Update()
        {
            if (IsInUse && _elapsed < _duration)
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                _height = Mathf.Lerp(_from, _to, t * t * (3f - 2f * t));
                Apply();
            }

            base.Update();
        }

        private void Animate(float to, float duration)
        {
            _from = _height;
            _to = to;
            _elapsed = 0f;
            _duration = duration;
            Apply();
        }

        // 높이 0 이면 면이 겹쳐 깜빡일 수 있어 아주 얇게만 남긴다.
        private void Apply()
        {
            if (grass != null)
                grass.localScale = new Vector3(_grassScale.x, _grassScale.y * Mathf.Max(_height, 0.001f), _grassScale.z);
        }
    }
}
