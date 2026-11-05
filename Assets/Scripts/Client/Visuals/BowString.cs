using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 활 시위. 활 무기 프리팹 루트(CharacterWeapon 과 같은 오브젝트)에 붙이고, LineRenderer 3점(위 끝 - 가운데 - 아래 끝)으로 그린다.
    ///   - 가운데 점(nock): 당김 세기(0~1)만큼 쉬는 자리(위아래 끝 사이)에서 당기는 손(활 반대손) 소켓으로 옮긴다.
    ///     세기는 재생 중인 클립의 BowDraw 커브(Animator float 파라미터)가 정한다. 커브가 없는 클립이면 파라미터가 기본값(-1)으로 돌아오고 0 으로 본다.
    ///   - 세기가 떨어지면(놓음) 가운데 점이 감쇠 스프링으로 쉬는 자리에 돌아가며 떨린다. 당기는 중/유지 중에는 손을 그대로 따라간다.
    ///   - Nock 은 지금 가운데 점(월드)이라 화살을 붙일 때 기준으로 쓴다(그리는 방식과 무관).
    /// 애니메이션과 손 IK(CharacterHandRig)가 뼈를 쓴 뒤 같은 프레임에 갱신한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(LineRenderer))]
    public class BowString : MonoBehaviour
    {
        /// <summary>클립 커브로 당김 세기를 넣는 Animator float 파라미터(기본값 -1 = 커브 없음).</summary>
        public const string DrawParameter = "BowDraw";
        private static readonly int DrawId = Animator.StringToHash(DrawParameter);

        /// <summary>스프링 계산 한 걸음(초). 프레임 시간을 이 단위로 나눠 돌려 큰 강성에서도 튀지 않게.</summary>
        private const float SpringStep = 1f / 240f;

        [Tooltip("시위 위 끝(활 위쪽 홈).")]
        [SerializeField] private Transform top;
        [Tooltip("시위 아래 끝(활 아래쪽 홈).")]
        [SerializeField] private Transform bottom;
        [Tooltip("당기지 않았을 때 시위 가운데 자리.")]
        [SerializeField] private Transform rest;
        [Tooltip("놓은 뒤 떨림 스프링 강성. 클수록 빠르게 떤다.")]
        [SerializeField, Min(0f)] private float stiffness = 1500f;
        [Tooltip("놓은 뒤 떨림 감쇠. 클수록 빨리 멈춘다.")]
        [SerializeField, Min(0f)] private float damping = 12f;

        private LineRenderer _line;
        private Animator _animator;
        private Transform _drawHand;
        private bool _hasCurve;
        private Vector3 _nockLocal;
        private Vector3 _velocity;
        private float _weight;

        /// <summary>지금 시위 가운데 점(월드).</summary>
        public Vector3 Nock => transform.TransformPoint(_nockLocal);

        /// <summary>에디터(프리팹 설정)에서 시위 끝점과 쉬는 자리를 연결한다.</summary>
        public void Setup(Transform topPoint, Transform bottomPoint, Transform restPoint)
        {
            top = topPoint;
            bottom = bottomPoint;
            rest = restPoint;
        }

        private void Start()
        {
            Bind();
        }

        /// <summary>선과 당기는 손, 커브 파라미터를 찾는다. 몸체에 붙은 뒤에 부른다(무기 프리팹 단독이면 손이 없어 쉬는 자리에만 둔다).</summary>
        private void Bind()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 3;
            _nockLocal = transform.InverseTransformPoint(rest.position);

            _animator = GetComponentInParent<Animator>();
            var rig = GetComponentInParent<CharacterHandRig>();
            var weapon = GetComponent<CharacterWeapon>();
            if (rig != null && weapon != null)
                _drawHand = rig.GetSocket(weapon.OffHand);

            if (_animator == null)
                return;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == DrawId && parameter.type == AnimatorControllerParameterType.Float)
                    _hasCurve = true;
            }
        }

        private void LateUpdate()
        {
            if (_line == null)
                return;

            float weight = 0f;
            if (_hasCurve)
                weight = Mathf.Clamp01(_animator.GetFloat(DrawId));
            UpdateString(weight, Time.deltaTime);
        }

        /// <summary>당김 세기 weight 로 가운데 점을 옮기고 시위를 다시 그린다. deltaTime 이 0 이면 떨림 없이 바로 그 자리에 둔다(미리보기용).</summary>
        public void UpdateString(float weight, float deltaTime)
        {
            if (_line == null)
                Bind();

            Vector3 target = transform.InverseTransformPoint(rest.position);
            if (_drawHand != null && weight > 0f)
                target = Vector3.Lerp(target, transform.InverseTransformPoint(_drawHand.position), weight);

            bool holding = weight > 0f && weight >= _weight;
            _weight = weight;
            if (holding || deltaTime <= 0f)
            {
                _nockLocal = target;
                _velocity = Vector3.zero;
            }
            else
            {
                for (float t = 0f; t < deltaTime; t += SpringStep)
                {
                    float step = Mathf.Min(SpringStep, deltaTime - t);
                    _velocity += (-(_nockLocal - target) * stiffness - _velocity * damping) * step;
                    _nockLocal += _velocity * step;
                }
            }

            _line.SetPosition(0, top.position);
            _line.SetPosition(1, Nock);
            _line.SetPosition(2, bottom.position);
        }
    }
}
