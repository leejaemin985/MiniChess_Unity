using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 손에 쥐고 늘어뜨리는 사슬 무기(사슬방패 등). 같은 오브젝트의 CharacterWeapon 과 함께 무기 프리팹 루트에 붙인다.
    ///   - 늘어진 사슬: dangleStart 에서 점 nodeCount 개를 간단한 물리(Verlet)로 늘어뜨리고, 점 사이마다 고리를 하나씩 놓는다(뼈 없이).
    ///   - 팔에 감긴 사슬: 쥐는 손(CharacterWeapon.Hand) 쪽 아래팔 뼈를 부모 Animator 에서 찾아 나선으로 고리를 붙인다. 뼈를 따라다니고 계산은 없다.
    ///   - 고리는 하나 걸러 90° 돌려 맞물리게 한다. 고리 오브젝트는 실행 중에 만든다(프리팹에 두지 않는다).
    ///   - 채찍: 부모 Animator 가 whipState(기본 공격)를 재생하는 동안 공격 프레임에 맞춰 사슬을 궤적 점(WhipPoint) 쪽으로 끌어당기고, 끝 트레일을 켠다.
    /// 나중에 리깅할 때 늘어진 사슬의 점이 그대로 뼈 자리가 된다.
    /// </summary>
    [RequireComponent(typeof(CharacterWeapon))]
    public sealed class ChainWeapon : MonoBehaviour
    {
        /// <summary>한 프레임에 이만큼 넘게 움직이면(순간이동 등) 사슬을 새 자리에 다시 늘어뜨린다.</summary>
        private const float TeleportDistance = 0.5f;

        [Tooltip("사슬이 늘어지기 시작하는 지점. 비우면 무기 루트(손 소켓).")]
        [SerializeField] private Transform dangleStart;
        [SerializeField] private Mesh linkMesh;
        [SerializeField] private Material linkMaterial;
        [Tooltip("고리 크기 배율(고리 메시 기준).")]
        [SerializeField, Min(0.01f)] private float linkScale = 0.3f;
        [Tooltip("점 사이 간격 = 고리 하나가 차지하는 길이(월드 단위).")]
        [SerializeField, Min(0.001f)] private float linkSpacing = 0.036f;
        [SerializeField] private bool castShadows = true;

        [Header("Dangle")]
        [Tooltip("늘어진 사슬의 점 개수(고리 = 점 - 1).")]
        [SerializeField, Min(2)] private int nodeCount = 12;
        [Tooltip("클수록 빨리 아래로 돌아와 무겁게 보인다(출렁임 속도는 그대로 자연스럽다).")]
        [SerializeField] private Vector3 gravity = new Vector3(0f, -25f, 0f);
        [Tooltip("매 프레임 속도를 줄이는 비율. 크면 슬로우 모션처럼 굼떠지니 낮게 둔다.")]
        [SerializeField, Range(0f, 1f)] private float damping = 0.08f;
        [Tooltip("손이 움직인 만큼 사슬 전체를 함께 옮기는 비율. 클수록 이동할 때 덜 휘날리고 아래에서만 출렁인다.")]
        [SerializeField, Range(0f, 1f)] private float follow = 0.6f;
        [SerializeField, Range(1, 20)] private int iterations = 8;

        [Header("Arm Coil")]
        [Tooltip("팔에 감긴 사슬을 넣는다(쥐는 손 쪽 아래팔).")]
        [SerializeField] private bool armCoil = true;
        [Tooltip("아래팔 축을 따라 감는 구간(0 = 팔꿈치, 1 = 손목).")]
        [SerializeField] private Vector2 coilRange = new Vector2(0.35f, 0.95f);
        [SerializeField, Min(0.1f)] private float coilTurns = 3f;
        [Tooltip("팔 축에서 사슬까지 거리(팔 두께 + 고리 두께).")]
        [SerializeField, Min(0.001f)] private float coilRadius = 0.032f;
        [Tooltip("팔에 감긴 고리 크기 배율(늘어진 사슬 linkScale 과 따로 정한다). 고리 간격도 linkSpacing 을 linkScale 대비 이 비율로 맞춰 쓴다.")]
        [SerializeField, Min(0.01f)] private float coilLinkScale = 0.3f;

        [Header("Whip")]
        [Tooltip("이 Animator 상태를 재생하는 동안 사슬을 채찍 모양으로 끌어당긴다(기본 공격 등).")]
        [SerializeField] private string whipState = "Attack";
        [Tooltip("아래 프레임 값들을 잡은 기준 클립의 전체 프레임 수(30fps). 진행도(0~1) × 이 값으로 프레임을 정해, 클립 속도를 바꿔도 같은 자세에 맞는다.")]
        [SerializeField, Min(1f)] private float whipFrames = 45f;
        [Tooltip("공격 프레임(30fps) → 채찍 모양을 섞는 비율(0 = 물리만, 1 = 채찍 모양).")]
        [SerializeField] private AnimationCurve whipWeight = new AnimationCurve(
            new Keyframe(5f, 0f), new Keyframe(8f, 1f), new Keyframe(28f, 1f), new Keyframe(30f, 0f));
        [Tooltip("채찍 궤적 점(WhipPoint)들의 부모(몸체 루트 아래). 점이 둘 이상이면 프레임 순서대로 Lerp 해 그 위치를 향해 뻗는다.")]
        [SerializeField] private Transform whipPath;
        [Tooltip("점이 없을 때: 공격 프레임 → 뻗는 좌우 각도(몸 기준, + = 오른쪽).")]
        [SerializeField] private AnimationCurve whipYaw = new AnimationCurve(new Keyframe(12f, 80f), new Keyframe(19f, -15f), new Keyframe(23f, -45f));
        [Tooltip("점이 없을 때: 공격 프레임 → 뻗는 위아래 각도(+ = 위).")]
        [SerializeField] private AnimationCurve whipPitch = new AnimationCurve(new Keyframe(12f, 12f), new Keyframe(32f, 12f));
        [Tooltip("끝으로 갈수록 이만큼 프레임 늦게 따라온다(끝이 뒤늦게 휘어 튕기는 맛).")]
        [SerializeField, Min(0f)] private float whipTipLag = 3f;
        [Tooltip("매 프레임 채찍 모양 쪽으로 끌어당기는 세기. 낮으면 물리가 더 남는다.")]
        [SerializeField, Range(0f, 1f)] private float whipPull = 0.65f;

        [Header("Tip Trail")]
        [Tooltip("사슬 끝 궤적(WeaponTrail, 수동 모드). 공격 프레임이 trailFrames 안일 때만 켠다.")]
        [SerializeField] private WeaponTrail tipTrail;
        [Tooltip("트레일 날 안쪽 점. 매 프레임 끝에서 trailSpan 번째 점으로 옮긴다.")]
        [SerializeField] private Transform trailBase;
        [Tooltip("트레일 날 끝 점. 매 프레임 사슬 마지막 점으로 옮긴다.")]
        [SerializeField] private Transform trailTip;
        [Tooltip("트레일 폭: 끝에서 몇 칸 안쪽까지 띠를 그릴지(1 칸 = linkSpacing).")]
        [SerializeField, Min(1)] private int trailSpan = 11;
        [Tooltip("트레일을 켜는 공격 프레임 구간(시작, 끝).")]
        [SerializeField] private Vector2 trailFrames = new Vector2(13f, 22f);

        private Vector3[] _nodes;
        private Vector3[] _prev;
        private readonly List<Transform> _links = new List<Transform>();
        private readonly List<Transform> _coilLinks = new List<Transform>();
        private readonly List<WhipPoint> _pathPoints = new List<WhipPoint>();
        private Animator _animator;

        /// <summary>늘어진 사슬이 시작되는 지점(손). 사슬을 던지는 연출(사슬 속박)이 여기서 출발한다.</summary>
        public Transform Anchor => dangleStart != null ? dangleStart : transform;

        /// <summary>
        /// 늘어진 사슬을 숨기거나 다시 보인다(팔에 감긴 사슬은 그대로). 던진 사슬이 손 사슬을 대신하는 동안 숨긴다.
        /// 다시 보일 때는 손 아래로 곧게 늘어뜨린 자리에서 시작한다(숨긴 동안의 출렁임을 남기지 않게).
        /// </summary>
        public void SetDangleVisible(bool visible)
        {
            if (visible && _nodes != null)
                ResetNodes();
            foreach (Transform link in _links)
                link.GetComponent<MeshRenderer>().enabled = visible;
        }

        private void OnEnable()
        {
            _animator = GetComponentInParent<Animator>();
            BuildDangle();
            if (armCoil)
                BuildCoil();
        }

        private void OnDisable()
        {
            DestroyLinks(_links);
            DestroyLinks(_coilLinks);
        }

        private void BuildDangle()
        {
            _nodes = new Vector3[nodeCount];
            _prev = new Vector3[nodeCount];
            ResetNodes();

            for (int i = 0; i < nodeCount - 1; i++)
                _links.Add(CreateLink("Link" + i, transform));
        }

        private void ResetNodes()
        {
            Vector3 origin = Anchor.position;
            for (int i = 0; i < nodeCount; i++)
            {
                _nodes[i] = origin + Vector3.down * linkSpacing * i;
                _prev[i] = _nodes[i];
            }
        }

        /// <summary>
        /// 쥐는 손 쪽 아래팔 뼈 로컬 공간에 나선으로 고리를 깐다. 뼈 아래에 붙여 두므로 매 프레임 계산이 없다.
        /// 손목 뼈의 로컬 위치(뼈 길이)는 자세와 상관없이 같아서 한 번만 계산해도 된다.
        /// </summary>
        private void BuildCoil()
        {
            Animator animator = _animator;
            if (animator == null || !animator.isHuman)
                return;

            bool right = GetComponent<CharacterWeapon>().Hand == WeaponHand.Right;
            Transform bone = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            Transform wrist = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (bone == null || wrist == null)
                return;

            Vector3 end = bone.InverseTransformPoint(wrist.position);
            float boneLength = end.magnitude;
            if (boneLength < 1e-4f)
                return;

            float scale = bone.lossyScale.x;
            Vector3 axis = end / boneLength;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            float radius = coilRadius / scale;
            float spacing = linkSpacing * (coilLinkScale / linkScale) / scale;

            Vector3 Helix(float t)
            {
                float along = Mathf.Lerp(coilRange.x, coilRange.y, t) * boneLength;
                float angle = t * coilTurns * Mathf.PI * 2f;
                return axis * along + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius;
            }

            // 나선을 촘촘히 따라가며 고리 간격마다 점을 찍는다.
            const int Samples = 400;
            var points = new List<Vector3> { Helix(0f) };
            Vector3 prev = points[0];
            float walked = 0f;
            for (int s = 1; s <= Samples; s++)
            {
                Vector3 p = Helix(s / (float)Samples);
                walked += Vector3.Distance(prev, p);
                prev = p;
                if (walked >= spacing)
                {
                    points.Add(p);
                    walked = 0f;
                }
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 mid = (points[i] + points[i + 1]) * 0.5f;
                Vector3 outward = (mid - axis * Vector3.Dot(mid, axis)).normalized;
                // 짝수 고리는 팔에 눕고, 홀수 고리는 90° 세워 맞물린다.
                Quaternion look = Quaternion.LookRotation(points[i + 1] - points[i], outward);
                if ((i & 1) == 1)
                    look *= Quaternion.AngleAxis(90f, Vector3.forward);

                Transform link = CreateLink("CoilLink" + i, bone);
                link.localPosition = mid;
                link.localRotation = look;
                link.localScale = Vector3.one * (coilLinkScale / scale);
                _coilLinks.Add(link);
            }
        }

        private void LateUpdate()
        {
            if (_nodes == null)
                return;

            Vector3 anchor = Anchor.position;
            Vector3 moved = anchor - _nodes[0];
            if (moved.sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                ResetNodes();
            }
            else
            {
                // 손 이동의 일부를 사슬 전체에 그대로 옮긴다(속도로 남기지 않게 이전 위치도 같이). 나머지만 출렁임이 된다.
                Vector3 carry = moved * follow;
                for (int i = 1; i < nodeCount; i++)
                {
                    _nodes[i] += carry;
                    _prev[i] += carry;
                }
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            Vector3 accel = gravity * dt * dt;
            for (int i = 1; i < nodeCount; i++)
            {
                Vector3 velocity = (_nodes[i] - _prev[i]) * (1f - damping);
                _prev[i] = _nodes[i];
                _nodes[i] += velocity + accel;
            }

            bool attacking = TryGetAttackFrame(out float attackFrame);
            if (attacking)
                ApplyWhip(anchor, attackFrame);

            for (int k = 0; k < iterations; k++)
            {
                _nodes[0] = anchor;
                for (int i = 0; i < nodeCount - 1; i++)
                {
                    Vector3 delta = _nodes[i + 1] - _nodes[i];
                    float length = delta.magnitude;
                    if (length < 1e-5f)
                        continue;
                    Vector3 fix = delta * ((length - linkSpacing) / length);
                    if (i == 0)
                    {
                        _nodes[i + 1] -= fix;
                    }
                    else
                    {
                        _nodes[i] += fix * 0.5f;
                        _nodes[i + 1] -= fix * 0.5f;
                    }
                }
            }
            _prev[0] = _nodes[0];

            // 사슬 끝 트레일: WeaponTrail 이 이 뒤(LateUpdate 실행 순서 1000)에 두 점을 기록한다.
            if (trailTip != null)
                trailTip.position = _nodes[nodeCount - 1];
            if (trailBase != null)
                trailBase.position = _nodes[Mathf.Max(0, nodeCount - 1 - trailSpan)];
            if (tipTrail != null)
                tipTrail.Emitting = attacking && attackFrame >= trailFrames.x && attackFrame <= trailFrames.y;

            // 고리: 점 사이 가운데, 긴 축(Z)을 사슬 방향으로. 앞 고리의 옆 방향을 이어받아 꼬이지 않게 하고, 맞물림 회전은 그 위에만 얹는다.
            Vector3 side = Anchor.right;
            for (int i = 0; i < _links.Count; i++)
            {
                Vector3 a = _nodes[i], b = _nodes[i + 1];
                Vector3 dir = b - a;
                if (dir.sqrMagnitude < 1e-8f)
                    dir = Vector3.down;
                Quaternion look = Quaternion.LookRotation(dir, Vector3.ProjectOnPlane(side, dir).sqrMagnitude > 1e-6f ? Vector3.Cross(dir, side) : Vector3.up);
                side = look * Vector3.right;
                if ((i & 1) == 1)
                    look *= Quaternion.AngleAxis(90f, Vector3.forward);

                Transform link = _links[i];
                link.SetPositionAndRotation((a + b) * 0.5f, look);
                link.localScale = Vector3.one * (linkScale / transform.lossyScale.x);
            }
        }

        /// <summary>부모 Animator 가 whipState 를 재생 중이면 지금 공격 프레임(기준 클립 whipFrames 기준, 재생 속도와 무관).</summary>
        private bool TryGetAttackFrame(out float frame)
        {
            frame = 0f;
            if (_animator == null || !_animator.isActiveAndEnabled)
                return false;
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(whipState))
                return false;
            frame = Mathf.Repeat(state.normalizedTime, 1f) * whipFrames;
            return true;
        }

        /// <summary>
        /// 점들을 채찍 모양(손에서 궤적 점 또는 각도 커브 방향으로 곧게 뻗은 선) 쪽으로 끌어당긴다.
        /// 끝으로 갈수록 whipTipLag 만큼 늦은 프레임의 방향을 써서 끝이 뒤늦게 휘어 따라온다.
        /// 위치를 직접 옮기므로 Verlet 속도로 남아, 섞는 비율이 줄면 그 관성으로 자연스럽게 풀린다.
        /// </summary>
        private void ApplyWhip(Vector3 anchor, float frame)
        {
            float weight = Mathf.Clamp01(whipWeight.Evaluate(frame)) * whipPull;
            if (weight <= 0f)
                return;

            Quaternion body = _animator.transform.rotation;
            List<WhipPoint> points = GetPathPoints();
            for (int i = 1; i < nodeCount; i++)
            {
                float lagged = frame - whipTipLag * (i / (float)(nodeCount - 1));
                Vector3 dir;
                if (points.Count >= 2)
                {
                    dir = EvaluatePath(points, lagged) - anchor;
                    if (dir.sqrMagnitude < 1e-6f)
                        continue;
                    dir.Normalize();
                }
                else
                {
                    dir = body * (Quaternion.Euler(-whipPitch.Evaluate(lagged), whipYaw.Evaluate(lagged), 0f) * Vector3.forward);
                }

                _nodes[i] = Vector3.Lerp(_nodes[i], anchor + dir * (linkSpacing * i), weight);
            }
        }

        /// <summary>whipPath 바로 아래의 켜진 WhipPoint 를 하이어라키 순서대로.</summary>
        private List<WhipPoint> GetPathPoints()
        {
            _pathPoints.Clear();
            if (whipPath == null)
                return _pathPoints;
            foreach (Transform child in whipPath)
            {
                if (child.gameObject.activeSelf && child.TryGetComponent(out WhipPoint point))
                    _pathPoints.Add(point);
            }
            return _pathPoints;
        }

        /// <summary>프레임에 해당하는 궤적 위치. 점 사이는 Lerp, 처음/끝 밖은 첫/마지막 점에 머문다.</summary>
        private static Vector3 EvaluatePath(List<WhipPoint> points, float frame)
        {
            if (frame <= points[0].Frame)
                return points[0].transform.position;
            for (int i = 0; i < points.Count - 1; i++)
            {
                WhipPoint a = points[i], b = points[i + 1];
                if (frame <= b.Frame)
                {
                    float span = b.Frame - a.Frame;
                    return Vector3.Lerp(a.transform.position, b.transform.position, span > 1e-4f ? (frame - a.Frame) / span : 1f);
                }
            }
            return points[points.Count - 1].transform.position;
        }

#if UNITY_EDITOR
        /// <summary>씬 뷰에 궤적 점과 잇는 선, 점마다 프레임을 그린다.</summary>
        private void OnDrawGizmosSelected()
        {
            List<WhipPoint> points = GetPathPoints();
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i].transform.position;
                Gizmos.DrawWireSphere(p, 0.03f);
                if (i > 0)
                    Gizmos.DrawLine(points[i - 1].transform.position, p);
                UnityEditor.Handles.Label(p + Vector3.up * 0.05f, $"{points[i].name}  f{points[i].Frame:0.#}");
            }
        }
#endif

        private Transform CreateLink(string linkName, Transform parent)
        {
            var link = new GameObject(linkName);
            link.transform.SetParent(parent, false);
            link.AddComponent<MeshFilter>().sharedMesh = linkMesh;
            var meshRenderer = link.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = linkMaterial;
            meshRenderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return link.transform;
        }

        private static void DestroyLinks(List<Transform> links)
        {
            foreach (Transform link in links)
            {
                if (link != null)
                    Destroy(link.gameObject);
            }
            links.Clear();
        }
    }
}
