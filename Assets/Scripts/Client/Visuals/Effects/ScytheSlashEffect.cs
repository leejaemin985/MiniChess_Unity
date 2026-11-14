using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 일자 베기(FX_ScytheSlash). 보라 섬광이 첫 칸에서 마지막 칸으로 빠르게 날아가고, 지나간 자리에 베인 선이 남으며 선을 따라 파편이 터진다.
    ///   - 루트: 두 점 가운데 + height 에서 카메라 쪽으로 towardCamera 만큼(그 점을 지나는 시선을 따라 당기므로 화면에서는 같은 자리). 로컬 +X 가 from → to 방향.
    ///   - span: 선 길이 / referenceLength 만큼 X 로 늘린다(파티클은 Hierarchy 스케일이라 따라 늘어난다).
    /// </summary>
    public sealed class ScytheSlashEffect : LineEffect
    {
        [Tooltip("선 길이에 맞춰 X 로 늘리는 부모.")]
        [SerializeField] private Transform span;
        [Tooltip("프리팹을 만든 기준 선 길이(칸 4개 = 4).")]
        [SerializeField, Min(0.1f)] private float referenceLength = 4f;
        [Tooltip("칸 윗면에서 선까지 높이(몸통).")]
        [SerializeField] private float height = 0.5f;
        [Tooltip("카메라 쪽으로 당기는 거리(월드 단위). 피격자 몸통에 가려지지 않고 앞에서 보이게 한다.")]
        [SerializeField] private float towardCamera = 1f;

        public override void Place(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            float length = direction.magnitude;
            Vector3 right = length > 0.0001f ? direction / length : Vector3.right;

            Vector3 center = (from + to) * 0.5f + Vector3.up * height;
            transform.SetPositionAndRotation(center + HitEffect.TowardCamera(center) * towardCamera,
                Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up));
            if (span != null)
                span.localScale = new Vector3(Mathf.Max(length, 0.01f) / referenceLength, 1f, 1f);
        }
    }
}
