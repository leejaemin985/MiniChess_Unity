using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>무기를 쥐는 손.</summary>
    public enum WeaponHand
    {
        Right,
        Left,
    }

    /// <summary>
    /// 캐릭터가 손에 쥐는 무기(무기 프리팹 루트에 붙인다). CharacterHandRig 가 hand 쪽 손 소켓에 붙이고, 반대손은 offHandGrip 을 IK 로 잡는다.
    ///   - 루트 = 쥐는 손의 손잡이 지점(스케일 1). 무기마다 다른 크기/각도/위치는 자식 모델 노드에서만 맞춘다(포인트가 모델 스케일을 타지 않게).
    ///   - 손 지점 축(루트, offHandGrip 공통): +X = 손가락 방향(손목 → 손가락 끝), +Y = 손잡이를 따라 엄지 쪽(칼끝 방향), +Z = X × Y.
    ///     오른손/왼손 같은 규칙이라 양손으로 같은 손잡이를 쥐면 두 지점의 회전이 거의 같다.
    /// </summary>
    public class CharacterWeapon : MonoBehaviour
    {
        private const float GizmoSize = 0.05f;

        [Tooltip("무기를 쥐는 손. 소켓은 이 손에 붙고, IK 는 반대손에 건다.")]
        [SerializeField] private WeaponHand hand = WeaponHand.Right;
        [Tooltip("반대손이 잡을 지점(위 축 규칙). 비우면 한손 무기(반대손 IK 없음).")]
        [SerializeField] private Transform offHandGrip;

        public WeaponHand Hand => hand;
        public WeaponHand OffHand => Opposite(hand);
        public Transform OffHandGrip => offHandGrip;

        public static WeaponHand Opposite(WeaponHand side) => side == WeaponHand.Right ? WeaponHand.Left : WeaponHand.Right;

        private void OnDrawGizmos()
        {
            DrawGrip(transform, 1f);
            if (offHandGrip != null)
                DrawGrip(offHandGrip, 0.7f);
        }

        /// <summary>손 지점 축을 그린다: 빨강 = 손가락, 초록 = 엄지(칼끝) 쪽, 파랑 = Z.</summary>
        private static void DrawGrip(Transform grip, float scale)
        {
            Vector3 p = grip.position;
            float size = GizmoSize * scale;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(p, p + grip.right * size);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(p, p + grip.up * size);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(p, p + grip.forward * size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(p, size * 0.25f);
        }
    }
}
