using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 맞은 유닛 몸통을 가로질러 그어지는 베기 선(FX_ScytheHitLine).
    ///   - 위치: 피격 대상 중앙(발밑 + height)에서 카메라 위치를 향해 Toward Camera(HitEffect) 만큼(화면에서는 대상 중앙에 겹친다).
    ///     프리팹은 공격자 쪽으로 당기지 않는다(Toward Attacker Offset 0).
    ///   - line: 선 전체의 부모. 카메라를 바라보며, 화면에서 2시 → 8시 방향(오른쪽 위 → 왼쪽 아래)으로 기운다(공격 방향과 상관없이 같음).
    ///   - sweep: line 의 오른쪽 끝(+X)에 두는 기준점. 선 파티클은 그 왼쪽으로 놓여 있고(Local, 크기는 부모를 따름),
    ///     sweep 의 X 크기를 0 → 1 로 늘려 선이 오른쪽 위에서 왼쪽 아래로 그어진다.
    /// </summary>
    public sealed class HitLineEffect : HitEffect
    {
        [Tooltip("선 전체의 부모. 매 프레임 카메라를 보도록 돌린다.")]
        [SerializeField] private Transform line;
        [Tooltip("선 오른쪽 끝의 기준점. X 크기를 0 → 1 로 늘려 오른쪽에서 왼쪽으로 긋는다.")]
        [SerializeField] private Transform sweep;
        [Tooltip("선이 끝까지 그어지는 시간(초).")]
        [SerializeField, Min(0.01f)] private float sweepTime = 0.08f;
        [Tooltip("발밑에서 피격 대상 중앙(몸통)까지 높이.")]
        [SerializeField] private float height = 0.5f;
        [Tooltip("화면에서 선의 기울기(도). 오른쪽 끝이 위로 올라간다(2시 → 8시).")]
        [SerializeField, Range(0f, 80f)] private float tilt = 25f;

        private Camera _camera;
        private float _startTime;

        protected override float AimHeight => height;

        protected override void OnSpawned()
        {
            _startTime = Time.time;
            SetSweep(0f);
        }

        // Place 가 꺼낸 뒤에 위치를 정하므로, 그려지기 전 LateUpdate 에서 맞춘다(선 파티클은 Local 이라 따라온다).
        private void LateUpdate()
        {
            float t = Mathf.Clamp01((Time.time - _startTime) / sweepTime);
            SetSweep(1f - (1f - t) * (1f - t));

            if (line == null)
                return;
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            Transform view = _camera.transform;
            line.SetPositionAndRotation(
                transform.position + Vector3.up * height,
                Quaternion.LookRotation(view.forward, view.up) * Quaternion.Euler(0f, 0f, tilt));
        }

        // 크기 0 은 파티클 메시가 사라지거나 뒤집힐 수 있어 아주 작게만 남긴다.
        private void SetSweep(float amount)
        {
            if (sweep != null)
                sweep.localScale = new Vector3(Mathf.Max(amount, 0.001f), 1f, 1f);
        }
    }
}
