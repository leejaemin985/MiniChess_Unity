using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 무기 날을 따라가는 궤적 리본. 날의 두 점(bladeBase = 날 안쪽, bladeTip = 날 끝)을 매 프레임 기록해 그 사이를 띠로 잇는다.
    ///   - 자동(Auto): 날 끝이 빠르게 움직일 때만 남긴다(minSpeed ~ fullSpeed 사이에서 서서히 진해짐). 휘두르기마다 따로 켜고 끌 필요가 없다.
    ///   - 수동: Emitting 을 켜고 끈다(애니메이션 이벤트 등).
    /// 프레임 사이가 벌어지는 빠른 휘두르기에서도 매끈하도록 기록 사이를 Catmull-Rom 으로 나눠 채운다.
    /// 메시 UV: x = 나이(0 = 방금, 1 = lifetime), y = 0 날 안쪽 ~ 1 날 끝. 정점 색 알파 = 그 순간의 세기.
    /// 띠는 월드 좌표로 그리므로 캐릭터가 움직여도 지나간 자리에 남는다.
    /// 애니메이션과 IK 가 뼈를 옮긴 뒤 기록하도록 LateUpdate 에서 처리한다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class WeaponTrail : MonoBehaviour
    {
        private struct Sample
        {
            public Vector3 Base;
            public Vector3 Tip;
            public float Time;
            public float Weight;
        }

        [SerializeField] private Transform bladeBase;
        [SerializeField] private Transform bladeTip;
        [SerializeField] private Material material;

        [Tooltip("지나간 자리가 남는 시간(초).")]
        [SerializeField, Min(0.02f)] private float lifetime = 0.28f;
        [Tooltip("켜면 날 끝 속도로 자동으로 남긴다. 끄면 Emitting 으로 켜고 끈다.")]
        [SerializeField] private bool auto = true;
        [Tooltip("자동: 날 끝 속도(m/s)가 이보다 느리면 남기지 않는다.")]
        [SerializeField, Min(0f)] private float minSpeed = 3f;
        [Tooltip("자동: 날 끝 속도(m/s)가 이 이상이면 최대 세기.")]
        [SerializeField, Min(0.01f)] private float fullSpeed = 7f;
        [Tooltip("세기가 따라가는 속도(초당). 클수록 바로 켜지고 꺼진다.")]
        [SerializeField, Min(0.1f)] private float weightSpeed = 14f;
        [Tooltip("프레임 사이를 몇 조각으로 나눠 채울지.")]
        [SerializeField, Range(1, 12)] private int subdivisions = 6;
        [Tooltip("최대 기록 수(메모리 상한).")]
        [SerializeField, Min(8)] private int maxSamples = 160;
        [Tooltip("한 프레임에 날 끝이 이보다 멀리 옮겨지면 순간이동으로 보고 궤적을 끊는다(워프, 풀에서 꺼냄 등).")]
        [SerializeField, Min(0.1f)] private float teleportDistance = 1.5f;

        private readonly List<Sample> _samples = new List<Sample>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        private GameObject _ribbon;
        private Mesh _mesh;
        private Sample _previous;
        private Sample _beforePrevious;
        private int _recorded;
        private float _weight;

        /// <summary>수동 모드에서 남길지. 자동 모드에서는 무시된다.</summary>
        public bool Emitting { get; set; }

        public Material Material
        {
            get => material;
            set
            {
                material = value;
                if (_ribbon != null)
                    _ribbon.GetComponent<MeshRenderer>().sharedMaterial = value;
            }
        }

        /// <summary>남은 궤적을 바로 지운다(풀 반환, 순간이동 등).</summary>
        public void Clear()
        {
            _samples.Clear();
            _recorded = 0;
            _weight = 0f;
            if (_ribbon != null)
                _ribbon.SetActive(false);
        }

        private void OnEnable()
        {
            Clear();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            if (_ribbon != null)
                Destroy(_ribbon);
            if (_mesh != null)
                Destroy(_mesh);
        }

        private void LateUpdate()
        {
            if (bladeBase == null || bladeTip == null)
                return;

            float now = Time.time;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var current = new Sample { Base = bladeBase.position, Tip = bladeTip.position, Time = now };
            if (_recorded > 0 && Vector3.Distance(current.Tip, _previous.Tip) > teleportDistance)
                Clear();

            float target = Emitting ? 1f : 0f;
            if (auto)
            {
                float speed = _recorded > 0 ? Vector3.Distance(current.Tip, _previous.Tip) / dt : 0f;
                target = Mathf.InverseLerp(minSpeed, fullSpeed, speed);
            }

            _weight = Mathf.MoveTowards(_weight, target, weightSpeed * dt);
            current.Weight = _weight;

            Record(current);
            RemoveExpired(now);
            Rebuild(now);
        }

        /// <summary>새 기록을 넣는다. 직전 기록과의 사이는 Catmull-Rom 으로 나눠 채운다(세기가 0 인 구간은 채우지 않는다).</summary>
        private void Record(Sample current)
        {
            if (_recorded > 0 && (current.Weight > 0f || _previous.Weight > 0f))
            {
                Sample p0 = _recorded > 1 ? _beforePrevious : _previous;
                Sample p1 = _previous;
                Sample p2 = current;
                // 다음 기록은 아직 모르므로 지금 방향으로 한 걸음 더 간 것으로 본다.
                var p3 = new Sample { Base = p2.Base * 2f - p1.Base, Tip = p2.Tip * 2f - p1.Tip };

                // 쉬었다 다시 남기기 시작하면 출발점을 먼저 넣는다(앞 궤적과는 세기 0 인 면으로 이어져 보이지 않는다).
                if (_samples.Count == 0 || _samples[_samples.Count - 1].Time < p1.Time)
                    _samples.Add(p1);

                for (int i = 1; i <= subdivisions; i++)
                {
                    float t = i / (float)subdivisions;
                    _samples.Add(new Sample
                    {
                        Base = CatmullRom(p0.Base, p1.Base, p2.Base, p3.Base, t),
                        Tip = CatmullRom(p0.Tip, p1.Tip, p2.Tip, p3.Tip, t),
                        Time = Mathf.Lerp(p1.Time, p2.Time, t),
                        Weight = Mathf.Lerp(p1.Weight, p2.Weight, t),
                    });
                }
            }

            _beforePrevious = _previous;
            _previous = current;
            _recorded++;

            while (_samples.Count > maxSamples)
                _samples.RemoveAt(0);
        }

        private void RemoveExpired(float now)
        {
            int expired = 0;
            while (expired < _samples.Count && now - _samples[expired].Time > lifetime)
                expired++;

            // 마지막 하나는 남겨 두어 띠가 끊기지 않게 한다(다음 기록과 이어진다).
            if (expired > 0)
                _samples.RemoveRange(0, Mathf.Min(expired, _samples.Count - 1));
            if (_samples.Count == 1 && now - _samples[0].Time > lifetime)
                _samples.Clear();
        }

        private void Rebuild(float now)
        {
            if (_samples.Count < 2)
            {
                if (_ribbon != null)
                    _ribbon.SetActive(false);
                return;
            }

            EnsureRibbon();
            _vertices.Clear();
            _uvs.Clear();
            _colors.Clear();
            _triangles.Clear();

            for (int i = 0; i < _samples.Count; i++)
            {
                Sample sample = _samples[i];
                float age = Mathf.Clamp01((now - sample.Time) / lifetime);
                _vertices.Add(sample.Base);
                _vertices.Add(sample.Tip);
                _uvs.Add(new Vector2(age, 0f));
                _uvs.Add(new Vector2(age, 1f));
                var color = new Color(1f, 1f, 1f, sample.Weight);
                _colors.Add(color);
                _colors.Add(color);

                if (i == 0)
                    continue;

                int v = i * 2;
                _triangles.Add(v - 2);
                _triangles.Add(v - 1);
                _triangles.Add(v);
                _triangles.Add(v);
                _triangles.Add(v - 1);
                _triangles.Add(v + 1);
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
            _ribbon.SetActive(true);
        }

        private void EnsureRibbon()
        {
            if (_ribbon != null)
                return;

            _mesh = new Mesh { name = "WeaponTrail" };
            _mesh.MarkDynamic();
            _ribbon = new GameObject($"{name} Trail");
            _ribbon.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _ribbon.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
