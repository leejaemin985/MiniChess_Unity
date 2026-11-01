using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 리깅된 Humanoid 캐릭터 몸체(Animator 와 같은 오브젝트)의 손 소켓과 무기, 반대손 IK.
    ///   - 손 소켓: 손 뼈 아래 손바닥 지점(CharacterWeapon 과 같은 축 규칙). 손가락 뿌리 뼈로 자동 계산하고(CreateSocket), 캐릭터마다 프리팹에서 조정할 수 있다.
    ///   - 무기는 CharacterWeapon.Hand 쪽 소켓의 자식이다. 반대손은 무기의 OffHandGrip 에 그 손의 소켓이 겹치도록 2본 IK(위팔-아래팔-손)로 잡는다.
    ///   - IK 는 애니메이션이 뼈를 쓴 뒤(LateUpdate) 같은 프레임에 풀어서 빠른 동작에서도 손이 무기에서 밀리지 않는다.
    ///     팔꿈치는 애니메이션이 굽힌 방향을 그대로 쓴다.
    ///   - 세기(0~1)는 재생 중인 클립의 OffHandIK 커브(Animator float 파라미터)가 정한다. 클립마다 들어 있으므로 캐릭터별(오버라이드 클립)·구간별로 다를 수 있다.
    ///     커브가 없는 클립이면 파라미터가 기본값(-1, 음수)으로 돌아오고, 그때는 Animator 상태의 OffHandIKState 값을 쓴다. 어느 쪽이든 여기서 부드럽게 따라간다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterHandRig : MonoBehaviour
    {
        /// <summary>클립 커브로 반대손 IK 세기를 넣는 Animator float 파라미터(기본값 -1 = 커브 없음).</summary>
        public const string OffHandIKParameter = "OffHandIK";
        private static readonly int OffHandIKId = Animator.StringToHash(OffHandIKParameter);

        /// <summary>손바닥 지점: 중지 뿌리 쪽으로 이만큼(손 뼈 → 중지 뿌리 대비).</summary>
        private const float PalmRatio = 0.8f;
        private const float Epsilon = 0.0001f;

        [SerializeField] private Transform rightSocket;
        [SerializeField] private Transform leftSocket;
        [SerializeField] private CharacterWeapon weapon;
        [Tooltip("반대손 IK 세기가 목표값을 따라가는 속도(초당).")]
        [SerializeField, Min(0f)] private float blendSpeed = 8f;

        private Animator _animator;
        private bool _hasCurve;
        private Transform _upper;
        private Transform _lower;
        private Transform _hand;
        private Transform _offSocket;
        private float _weight;
        private float _targetWeight;

        public CharacterWeapon Weapon => weapon;

        public Transform GetSocket(WeaponHand side) => side == WeaponHand.Right ? rightSocket : leftSocket;

        /// <summary>에디터(프리팹 생성 메뉴)에서 소켓과 무기를 연결한다.</summary>
        public void Setup(Transform right, Transform left, CharacterWeapon equipped)
        {
            rightSocket = right;
            leftSocket = left;
            weapon = equipped;
        }

        /// <summary>반대손 IK 목표 세기(0~1, 재생 중인 클립에 OffHandIK 커브가 없을 때 쓰는 값). 한손 무기거나 무기가 없으면 무시된다.</summary>
        public void SetOffHandWeight(float target)
        {
            _targetWeight = Mathf.Clamp01(target);
        }

        /// <summary>
        /// side 손 뼈 아래에 손바닥 소켓을 만든다. 손가락 뿌리 뼈({Right|Left}{Index|Middle|Little}Proximal)가 손 뼈의 +Y 쪽에 있는 리그(Meshy/Mixamo) 기준.
        /// 손가락 뼈가 없으면 손 뼈 원점에 손 뼈 회전 그대로 만든다.
        /// </summary>
        public static Transform CreateSocket(Animator animator, WeaponHand side)
        {
            Transform hand = animator.GetBoneTransform(side == WeaponHand.Right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (hand == null)
                return null;

            var socket = new GameObject($"{side}HandSocket").transform;
            socket.SetParent(hand, false);

            string prefix = side.ToString();
            Transform index = hand.Find($"{prefix}IndexProximal");
            Transform middle = hand.Find($"{prefix}MiddleProximal");
            Transform little = hand.Find($"{prefix}LittleProximal");
            if (index == null || middle == null || little == null)
                return socket;

            // 검지 뿌리 - 새끼 뿌리 = 쥔 손잡이 축(엄지 쪽이 +Y), 손가락(손 뼈 +Y)을 그 축에 수직으로 눕힌 것이 +X.
            Vector3 up = (index.localPosition - little.localPosition).normalized;
            Vector3 fingers = Vector3.ProjectOnPlane(Vector3.up, up).normalized;
            socket.localPosition = middle.localPosition * PalmRatio;
            socket.localRotation = Quaternion.LookRotation(Vector3.Cross(fingers, up), up);
            return socket;
        }

        private void Awake()
        {
            if (weapon == null || weapon.OffHandGrip == null)
                return;

            _animator = GetComponent<Animator>();
            bool right = weapon.OffHand == WeaponHand.Right;
            _upper = _animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            _lower = _animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            _hand = _animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            _offSocket = GetSocket(weapon.OffHand);

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == OffHandIKId && parameter.type == AnimatorControllerParameterType.Float)
                    _hasCurve = true;
            }
        }

        /// <summary>지금 따라갈 세기: 클립 커브가 있으면(0 이상) 그 값, 없으면 상태 값.</summary>
        private float GetTargetWeight()
        {
            if (!_hasCurve)
                return _targetWeight;

            float curve = _animator.GetFloat(OffHandIKId);
            return curve >= 0f ? Mathf.Clamp01(curve) : _targetWeight;
        }

        private void LateUpdate()
        {
            if (_hand == null || _offSocket == null)
                return;

            _weight = Mathf.MoveTowards(_weight, GetTargetWeight(), blendSpeed * Time.deltaTime);
            if (_weight <= 0f)
                return;

            // 반대손 소켓이 그립 지점에 겹치려면 손 뼈가 놓일 곳.
            Transform grip = weapon.OffHandGrip;
            Quaternion handRotation = grip.rotation * Quaternion.Inverse(_offSocket.localRotation);
            Vector3 handPosition = grip.position - handRotation * Vector3.Scale(_offSocket.localPosition, _hand.lossyScale);

            SolveTwoBone(_upper, _lower, _hand, Vector3.Lerp(_hand.position, handPosition, _weight));
            _hand.rotation = Quaternion.Slerp(_hand.rotation, handRotation, _weight);
        }

        /// <summary>위팔(root)·아래팔(mid)을 돌려 손(tip)을 target 에 보낸다. 팔꿈치는 지금 굽은 면 안에서만 펴고 굽힌다.</summary>
        private static void SolveTwoBone(Transform root, Transform mid, Transform tip, Vector3 target)
        {
            Vector3 a = root.position, b = mid.position, c = tip.position;
            Vector3 ab = b - a, cb = c - b;
            float abLength = ab.magnitude, cbLength = cb.magnitude;
            float atLength = Mathf.Clamp((target - a).magnitude, Mathf.Abs(abLength - cbLength) + Epsilon, abLength + cbLength - Epsilon);

            Vector3 axis = Vector3.Cross(ab, cb);
            if (axis.sqrMagnitude < Epsilon)
                axis = Vector3.Cross(target - a, ab);
            if (axis.sqrMagnitude >= Epsilon)
            {
                float oldAngle = TriangleAngle((c - a).magnitude, abLength, cbLength);
                float newAngle = TriangleAngle(atLength, abLength, cbLength);
                mid.rotation = Quaternion.AngleAxis((oldAngle - newAngle) * Mathf.Rad2Deg, axis.normalized) * mid.rotation;
            }

            root.rotation = Quaternion.FromToRotation(tip.position - a, target - a) * root.rotation;
        }

        /// <summary>세 변이 side, adjacent1, adjacent2 인 삼각형에서 side 맞은편 각(라디안).</summary>
        private static float TriangleAngle(float side, float adjacent1, float adjacent2)
        {
            float cos = (adjacent1 * adjacent1 + adjacent2 * adjacent2 - side * side) / (2f * adjacent1 * adjacent2);
            return Mathf.Acos(Mathf.Clamp(cos, -1f, 1f));
        }
    }
}
