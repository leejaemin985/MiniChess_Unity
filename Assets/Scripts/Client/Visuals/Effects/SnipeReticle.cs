using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 장거리 조준(AimedShotEffect)의 조준 문양. 조각(호 3개, 초침 3개, 침 4개)을 조립해 하나의 문양으로 보인다. 조각은 흰색 스프라이트이고 색은 각 SpriteRenderer.color 로 입힌다.
    ///   - Show: 크게 벌어진 채 나타나 closeTime 동안 제자리로 조여든다.
    ///   - Shatter: 조각마다 중심에서 바깥으로 흩어지며 돌고, shatterTime 동안 흐려져 사라진다(shatterSparks 함께).
    /// 루트는 늘 카메라를 바라본다(로컬 XY = 화면 평면, 로컬 +Y = 화면 위). 조각은 로컬 XY 평면 위 스프라이트이고, 조각 중심의 방향이 흩어지는 방향이다.
    /// </summary>
    public sealed class SnipeReticle : MonoBehaviour
    {
        [Tooltip("조각들. 각 SpriteRenderer 의 Color 가 그 조각의 색이다(알파는 나타나고 사라질 때 곱해진다).")]
        [SerializeField] private SpriteRenderer[] pieces = new SpriteRenderer[0];
        [SerializeField] private ParticleSystem shatterSparks;

        [Header("Aim")]
        [SerializeField, Min(0.01f)] private float closeTime = 0.35f;
        [Tooltip("나타날 때 크기(배율). closeTime 동안 1 로 조여든다.")]
        [SerializeField, Min(1f)] private float startScale = 1.7f;
        [SerializeField, Min(0.01f)] private float fadeInTime = 0.12f;

        [Header("Shatter")]
        [SerializeField, Min(0.01f)] private float shatterTime = 0.4f;
        [Tooltip("바깥으로 흩어지는 처음 속도(월드 단위/초) 범위.")]
        [SerializeField] private Vector2 speed = new Vector2(1.2f, 2.2f);
        [Tooltip("흩어질 때 옆으로 어긋나는 속도 범위(±).")]
        [SerializeField, Min(0f)] private float sideSpeed = 0.5f;
        [Tooltip("속도 감쇠(클수록 빨리 멈춘다).")]
        [SerializeField, Min(0f)] private float drag = 4f;
        [Tooltip("조각이 도는 속도(도/초) 범위(±).")]
        [SerializeField, Min(0f)] private float spin = 540f;
        [Tooltip("사라질 때 크기(배율).")]
        [SerializeField, Range(0f, 1f)] private float endScale = 0.6f;

        private enum Phase { Hidden, Aiming, Shattering }

        private Phase _phase;
        private float _time;
        private Vector3[] _homePositions;
        private Quaternion[] _homeRotations;
        private Vector3[] _homeScales;
        private Vector3[] _velocities;
        private float[] _spins;
        private Color[] _colors;

        /// <summary>보이는 중인지(조여드는 중이거나 깨지는 중).</summary>
        public bool IsVisible => _phase != Phase.Hidden;

        private void Awake()
        {
            int count = pieces.Length;
            _homePositions = new Vector3[count];
            _homeRotations = new Quaternion[count];
            _homeScales = new Vector3[count];
            _velocities = new Vector3[count];
            _spins = new float[count];
            _colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                Transform piece = pieces[i].transform;
                _homePositions[i] = piece.localPosition;
                _homeRotations[i] = piece.localRotation;
                _homeScales[i] = piece.localScale;
                _colors[i] = pieces[i].color;
            }

            Hide();
        }

        /// <summary>조준 시작: 벌어진 채 나타나 조여든다.</summary>
        public void Show()
        {
            _phase = Phase.Aiming;
            _time = 0f;
            if (shatterSparks != null)
                shatterSparks.Clear(true);
            FaceCamera();
            SetVisible(true);
            Evaluate();
        }

        /// <summary>명중: 조각을 흩어 깨뜨린다.</summary>
        public void Shatter()
        {
            if (_phase == Phase.Hidden)
                return;

            for (int i = 0; i < pieces.Length; i++)
            {
                Transform piece = pieces[i].transform;
                // 조여드는 중이었어도 제자리에서 시작한다.
                Vector3 out2D = _homePositions[i];
                out2D.z = 0f;
                Vector3 outward = out2D.sqrMagnitude > 0.0001f ? out2D.normalized : Vector3.up;
                Vector3 side = new Vector3(-outward.y, outward.x, 0f);
                _velocities[i] = outward * Random.Range(speed.x, speed.y) + side * Random.Range(-sideSpeed, sideSpeed);
                _spins[i] = Random.Range(-spin, spin);
                piece.localPosition = _homePositions[i];
                piece.localScale = _homeScales[i];
            }

            _phase = Phase.Shattering;
            _time = 0f;
            if (shatterSparks != null)
                shatterSparks.Play(true);
            Evaluate();
        }

        public void Hide()
        {
            _phase = Phase.Hidden;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            FaceCamera();
        }

        private void Update()
        {
            if (_phase == Phase.Hidden)
                return;

            float dt = Time.deltaTime;
            _time += dt;
            if (_phase == Phase.Shattering)
            {
                float damping = Mathf.Exp(-drag * dt);
                for (int i = 0; i < pieces.Length; i++)
                {
                    Transform piece = pieces[i].transform;
                    piece.localPosition += _velocities[i] * dt;
                    piece.localRotation = Quaternion.Euler(0f, 0f, _spins[i] * dt) * piece.localRotation;
                    _velocities[i] *= damping;
                }

                if (_time >= shatterTime)
                {
                    Hide();
                    return;
                }
            }

            Evaluate();
        }

        private void FaceCamera()
        {
            Camera view = Camera.main;
            if (view != null)
                transform.rotation = view.transform.rotation;
        }

        private void Evaluate()
        {
            float alpha;
            if (_phase == Phase.Aiming)
            {
                float t = Mathf.Clamp01(_time / closeTime);
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                float scale = Mathf.Lerp(startScale, 1f, ease);
                for (int i = 0; i < pieces.Length; i++)
                {
                    Transform piece = pieces[i].transform;
                    piece.localPosition = _homePositions[i] * scale;
                    piece.localRotation = _homeRotations[i];
                    piece.localScale = _homeScales[i] * scale;
                }

                alpha = Mathf.Clamp01(_time / fadeInTime);
            }
            else
            {
                float t = Mathf.Clamp01(_time / shatterTime);
                alpha = 1f - t * t;
                float scale = Mathf.Lerp(1f, endScale, t);
                for (int i = 0; i < pieces.Length; i++)
                    pieces[i].transform.localScale = _homeScales[i] * scale;
            }

            for (int i = 0; i < pieces.Length; i++)
            {
                Color color = _colors[i];
                color.a *= alpha;
                pieces[i].color = color;
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (SpriteRenderer piece in pieces)
            {
                if (piece != null)
                    piece.enabled = visible;
            }
        }
    }
}
