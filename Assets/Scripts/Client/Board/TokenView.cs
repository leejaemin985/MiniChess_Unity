using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>토큰 프리팹의 보석을 회전·부유시킨다. 배치·크기·머티리얼은 프리팹에서 편집한다.</summary>
    public class TokenView : MonoBehaviour
    {
        [Tooltip("회전·부유시킬 모델.")]
        [SerializeField] private Transform animatedVisual;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float bobSpeed = 2.5f;
        [Tooltip("초당 회전 각도.")]
        [SerializeField] private float spinSpeed = 60f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;

        private void Awake()
        {
            if (animatedVisual != null)
            {
                _restPosition = animatedVisual.localPosition;
                _restRotation = animatedVisual.localRotation;
            }

            Update();
        }

        private void Update()
        {
            if (animatedVisual == null)
                return;

            animatedVisual.localPosition = _restPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
            animatedVisual.localRotation = _restRotation * Quaternion.Euler(0f, Time.time * spinSpeed, 0f);
        }
    }
}
