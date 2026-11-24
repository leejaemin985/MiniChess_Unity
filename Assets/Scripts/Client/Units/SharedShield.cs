using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 공용 실드 막(Resources/Effects/FX_SharedShield). 실드 출처와 상관없이 실드가 남아 있는 동안 유닛을 감싸는 은은한 막.
    /// 출처별 연출(정원사 FX_ShieldBloom 등)은 일회성 이펙트로 따로 재생되고, 끝나면 이 막만 남아 "실드가 있다"만 알린다.
    /// Show 는 showDelay 뒤 fadeInTime 동안 _Alpha 를 0 → 1, Hide 는 fadeOutTime 동안 현재 값 → 0 으로 움직인다.
    /// showDelay 는 출처별 시전 이펙트의 막이 사라질 즈음 이어받도록 두는 여유 시간이다.
    /// </summary>
    public sealed class SharedShield : MonoBehaviour
    {
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [SerializeField] private Renderer bubble;
        [SerializeField, Min(0f)] private float showDelay = 0.8f;
        [SerializeField, Min(0.01f)] private float fadeInTime = 0.4f;
        [SerializeField, Min(0.01f)] private float fadeOutTime = 0.3f;

        private MaterialPropertyBlock _block;
        private float _alpha;
        private float _target;
        private float _wait;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            Apply();
        }

        /// <summary>실드가 있으면 막을 띄우고(이미 떠 있거나 뜨는 중이면 그대로), 없으면 사라지게 한다.</summary>
        public void Set(bool shielded)
        {
            if (shielded)
                Show();
            else
                Hide();
        }

        private void Show()
        {
            if (_target > 0f)
                return;

            _target = 1f;
            _wait = showDelay;
        }

        private void Hide()
        {
            _target = 0f;
            _wait = 0f;
        }

        /// <summary>숨겨진 뒤(사망 등) 다시 보일 때 남은 페이드 없이 지금 목표 상태로 맞춘다.</summary>
        private void OnDisable()
        {
            _wait = 0f;
            _alpha = _target;
            Apply();
        }

        private void Update()
        {
            if (_wait > 0f)
            {
                _wait -= Time.deltaTime;
                return;
            }

            if (Mathf.Approximately(_alpha, _target))
                return;

            float speed = _target > _alpha ? 1f / fadeInTime : 1f / fadeOutTime;
            _alpha = Mathf.MoveTowards(_alpha, _target, speed * Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            if (bubble == null)
                return;

            bubble.enabled = _alpha > 0f;
            bubble.GetPropertyBlock(_block);
            _block.SetFloat(AlphaId, _alpha);
            bubble.SetPropertyBlock(_block);
        }
    }
}
