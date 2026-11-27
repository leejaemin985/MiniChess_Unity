using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 사슬 속박(FX_ChainBind). 스킬 모션과 함께 Place 되고(LaunchDelay), 그 순간부터 프레임(30fps) 타임라인으로 진행한다.
    ///   - launchFrame : 시전자 손 사슬(ChainWeapon)의 늘어진 부분을 숨기고, 같은 자리(손)에서 쐐기(projectile)가 날아가며 고리가 뒤로 이어진다.
    ///   - hitFrame    : 명중(impact, 피해/상태 반영 시점). 쐐기가 대상 몸을 나선으로 돌며 사슬이 감긴다(wrapEndFrame 까지).
    ///                   이때 손 ↔ 감긴 사슬 사이 사슬 길이를 "명중 때 거리 + slackLength" 로 정한다(거리가 매번 달라도 같은 여유로 처진다).
    ///   - pullFrame   : 사슬 길이를 지금 거리까지 감아 들여(pullFrames) 팽팽해지고, 감긴 사슬이 pullRadius 로 조이며 tighten 을 재생한다(압력).
    ///   - releaseFrame: fadeFrames 동안 고리가 작아지며 사라지고, 끝나면 손 사슬을 다시 보인다.
    /// 처짐은 사슬 길이와 지금 거리의 차이로 정한다(포물선 근사). 손이 움직이면 처짐도 따라 변하고, 거리보다 짧아지지 않는다.
    /// 고리는 쐐기 쪽 끝(감긴 사슬 끝)부터 손 쪽으로 한 줄로 깐다. 감긴 사슬과 손까지 사슬이 끊김 없이 이어지고, 남는 틈은 손 쪽에 생긴다.
    /// 고리 크기는 손 쪽 linkScale.x 에서 growLinks 개에 걸쳐 linkScale.y 로 커진다(손 사슬과 자연스럽게 잇고 멀리서도 보이게).
    /// 시전자(SetCaster)에 ChainWeapon 이 없으면 시전자 칸 위 발사 지점(LaunchPoint)에서 출발한다. 고리는 linkMesh 로 실행 중에 만든다.
    /// </summary>
    public sealed class ChainBindEffect : ProjectileEffect
    {
        [Header("Links")]
        [SerializeField] private Mesh linkMesh;
        [SerializeField] private Material linkMaterial;
        [Tooltip("고리 크기 배율 1 일 때 고리 사이 간격(월드 단위). 실제 간격 = 이 값 × 고리 크기.")]
        [SerializeField, Min(0.01f)] private float linkSpacing = 0.12f;
        [Tooltip("고리 크기: 손 쪽(x) → growLinks 개 안쪽부터(y). 감긴 사슬은 y.")]
        [SerializeField] private Vector2 linkScale = new Vector2(0.3f, 0.6f);
        [SerializeField, Min(1)] private int growLinks = 6;
        [Tooltip("고리 최대 개수(감긴 사슬 + 손까지 사슬).")]
        [SerializeField, Min(1)] private int maxLinks = 160;

        [Header("Coil")]
        [Tooltip("감기는 사슬의 아래/위 높이(대상 칸 윗면 기준).")]
        [SerializeField] private Vector2 coilHeight = new Vector2(0.2f, 0.85f);
        [SerializeField, Min(0.1f)] private float coilTurns = 2.2f;
        [Tooltip("감기 시작할 때 반지름 → 다 감긴(느슨한) 반지름.")]
        [SerializeField] private Vector2 coilRadius = new Vector2(0.6f, 0.36f);
        [Tooltip("당겨 조였을 때 반지름.")]
        [SerializeField, Min(0.01f)] private float pullRadius = 0.27f;
        [Tooltip("조이는 순간 한 번 더 파고드는 깊이(철컥).")]
        [SerializeField, Min(0f)] private float pullSnap = 0.04f;

        [Header("Slack")]
        [Tooltip("다 감겼을 때 손 ↔ 감긴 사슬 사이 사슬이 거리보다 긴 만큼(월드 단위). 거리와 상관없이 이 여유로 처진다.")]
        [SerializeField, Min(0f)] private float slackLength = 0.1f;
        [Tooltip("날아가는 동안 처짐 깊이.")]
        [SerializeField, Min(0f)] private float flightSag = 0.05f;

        [Header("Effects")]
        [Tooltip("다 감겼을 때 감긴 끝(쐐기 자리) 바닥에서 재생.")]
        [SerializeField] private ParticleSystem anchor;
        [Tooltip("당겨 조일 때 대상 몸 중심에서 재생.")]
        [SerializeField] private ParticleSystem tighten;

        [Header("Timeline (30fps, 스킬 모션 시작 기준)")]
        [SerializeField] private float launchFrame = 6f;
        [SerializeField] private float hitFrame = 12f;
        [SerializeField] private float wrapEndFrame = 24f;
        [SerializeField] private float pullFrame = 28f;
        [Tooltip("감아 들여 팽팽해지기까지 걸리는 프레임.")]
        [SerializeField, Min(0.1f)] private float pullFrames = 3f;
        [SerializeField] private float releaseFrame = 38f;
        [SerializeField, Min(0.1f)] private float fadeFrames = 6f;

        private const float Fps = 30f;
        private const int SpanSamples = 48;
        private const int CoilSamples = 120;

        private readonly List<MeshRenderer> _links = new List<MeshRenderer>();
        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly List<float> _lengths = new List<float>();
        private ParticleSystem[] _projectileParticles;
        private ChainWeapon _weapon;
        private Transform _hand;
        private float _startAngle;
        private Vector3 _handAtHit;
        private float _slackRope;
        private int _coilPointCount;
        private float _frame;
        private bool _playing;
        private bool _launched;
        private bool _hit;
        private bool _wrapped;
        private bool _pulled;

        public override float LaunchDelay => launchFrame / Fps;

        protected override float TravelTime => Mathf.Max(hitFrame - launchFrame, 0.1f) / Fps;

        protected override bool IsFinished => base.IsFinished && !_playing;

        private Vector3 HandPoint => _hand != null ? _hand.position : LaunchPoint;

        private void Awake()
        {
            _projectileParticles = Projectile.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < maxLinks; i++)
                _links.Add(CreateLink("Link" + i));
        }

        protected override void OnAimStarted()
        {
            _frame = 0f;
            _playing = true;
            _launched = false;
            _hit = false;
            _wrapped = false;
            _pulled = false;
            _weapon = null;
            _hand = null;
            Projectile.gameObject.SetActive(false);
            SetLinksVisible(0);
        }

        protected override void OnLaunched()
        {
            _launched = true;
            _weapon = Caster != null ? Caster.GetComponentInChildren<ChainWeapon>() : null;
            _hand = _weapon != null ? _weapon.Anchor : null;
            if (_weapon != null)
                _weapon.SetDangleVisible(false);

            Vector3 toCaster = HandPoint - TargetCell;
            _startAngle = Mathf.Atan2(toCaster.z, toCaster.x);

            Projectile.gameObject.SetActive(true);
            Projectile.localScale = Vector3.one;
            foreach (ParticleSystem particle in _projectileParticles)
                particle.Play(false);
        }

        protected override void OnArrived()
        {
            _hit = true;
            _handAtHit = HandPoint;
            if (Impact != null)
                Impact.transform.SetPositionAndRotation(HitPoint, Quaternion.LookRotation(_handAtHit - HitPoint));
        }

        protected override void OnReleased()
        {
            base.OnReleased();
            Stop();
        }

        protected override void Update()
        {
            if (_playing)
                _frame += Time.deltaTime * Fps;

            base.Update();

            if (_playing && _launched)
                Tick(_frame);
        }

        private void Tick(float frame)
        {
            Vector3 handPoint = HandPoint;

            // 1) 날아간다: 쐐기 → 손.
            if (!_hit)
            {
                float t = Mathf.InverseLerp(launchFrame, hitFrame, frame);
                Vector3 head = Vector3.Lerp(handPoint, HitPoint, t);
                Vector3 flight = HitPoint - handPoint;
                Projectile.SetPositionAndRotation(head, flight.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(flight) : Projectile.rotation);
                _points.Clear();
                _coilPointCount = 0;
                AddSpan(head, handPoint, flightSag * t);
                LayLinks(1f);
                return;
            }

            // 2) 감긴다: 쐐기가 나선을 따라 돌고, 사슬 길이에 여유가 생겨 처진다.
            float wrap = Mathf.Clamp01(Mathf.InverseLerp(hitFrame, wrapEndFrame, frame));
            float wrapEased = 1f - (1f - wrap) * (1f - wrap);
            float radius = Mathf.Lerp(coilRadius.x, coilRadius.y, wrapEased);
            Vector3 coilStart = CoilPoint(0f, radius);
            float distance = Vector3.Distance(handPoint, coilStart);
            // 사슬 길이 = 명중 때 손 자리에서 감긴 사슬 시작까지 거리 + 여유. 그 뒤 손이 움직인 만큼만 처짐이 변한다.
            float rope = Vector3.Distance(_handAtHit, coilStart) + slackLength * wrapEased;

            // 3) 당긴다: 사슬 길이를 지금 거리까지 감아 들이고, 감긴 사슬이 조인다.
            if (frame < pullFrame)
            {
                _slackRope = rope;
            }
            else
            {
                float pull = Mathf.Clamp01((frame - pullFrame) / pullFrames);
                float pullEased = 1f - (1f - pull) * (1f - pull) * (1f - pull);
                float snap = Mathf.Sin(Mathf.Clamp01((frame - pullFrame) / (pullFrames * 2f)) * Mathf.PI);
                radius = Mathf.Lerp(coilRadius.y, pullRadius, pullEased) - pullSnap * snap;
                coilStart = CoilPoint(0f, radius);
                distance = Vector3.Distance(handPoint, coilStart);
                rope = Mathf.Lerp(_slackRope, distance, pullEased);
                if (!_pulled)
                {
                    _pulled = true;
                    if (tighten != null)
                    {
                        tighten.transform.position = TargetCell + Vector3.up * Mathf.Lerp(coilHeight.x, coilHeight.y, 0.5f);
                        tighten.Play(true);
                    }
                }
            }

            // 4) 풀린다.
            float fade = Mathf.Clamp01((frame - releaseFrame) / fadeFrames);
            float scale = 1f - fade * fade;

            // 쐐기(감긴 끝) → 감긴 사슬 → 손 순서로 한 줄.
            _points.Clear();
            for (int s = 0; s <= CoilSamples; s++)
                _points.Add(CoilPoint(wrapEased * (1f - s / (float)CoilSamples), radius));
            _coilPointCount = _points.Count;
            AddSpan(coilStart, handPoint, SagFor(distance, rope));
            LayLinks(scale);

            Vector3 tip = _points[0];
            Vector3 tangent = _points[0] - _points[1];
            Projectile.SetPositionAndRotation(tip, tangent.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(tangent) : Projectile.rotation);
            Projectile.localScale = Vector3.one * scale;

            if (!_wrapped && wrap >= 1f)
            {
                _wrapped = true;
                if (anchor != null)
                {
                    anchor.transform.position = new Vector3(tip.x, TargetCell.y, tip.z);
                    anchor.Play(true);
                }
            }

            if (fade >= 1f)
                Stop();
        }

        /// <summary>쐐기와 고리를 숨기고 손 사슬을 다시 보인다(끝까지 재생했거나 중간에 반납될 때).</summary>
        private void Stop()
        {
            _playing = false;
            Projectile.gameObject.SetActive(false);
            SetLinksVisible(0);
            if (_weapon != null)
                _weapon.SetDangleVisible(true);
            _weapon = null;
            _hand = null;
        }

        // 길이 rope 인 사슬이 distance 만큼 떨어진 두 점에 걸렸을 때 가운데 처짐(포물선 근사: 길이 ≈ d + 8s²/3d). 거리보다 짧으면 0.
        private static float SagFor(float distance, float rope)
        {
            return rope > distance ? Mathf.Sqrt(3f * distance * (rope - distance) / 8f) : 0f;
        }

        // from → to 포물선(가운데가 sag 만큼 처짐)을 _points 에 잇는다.
        private void AddSpan(Vector3 from, Vector3 to, float sag)
        {
            int first = _points.Count == 0 ? 0 : 1;
            for (int s = first; s <= SpanSamples; s++)
            {
                float u = s / (float)SpanSamples;
                _points.Add(Vector3.Lerp(from, to, u) + Vector3.down * (sag * 4f * u * (1f - u)));
            }
        }

        // _points 를 따라 앞(쐐기 쪽)에서부터 고리를 깐다. 손 쪽 growLinks 개 구간은 작아진다.
        private void LayLinks(float scale)
        {
            _lengths.Clear();
            float total = 0f;
            _lengths.Add(0f);
            for (int s = 1; s < _points.Count; s++)
            {
                total += Vector3.Distance(_points[s - 1], _points[s]);
                _lengths.Add(total);
            }

            float growLength = linkSpacing * (linkScale.x + linkScale.y) * 0.5f * growLinks;
            Vector3 side = Vector3.Cross(Vector3.up, _points[_points.Count - 1] - _points[0]).normalized;
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.right;

            int count = 0;
            float walked = 0f;
            while (count < _links.Count)
            {
                float fromHand = total - walked;
                float size = Mathf.Lerp(linkScale.x, linkScale.y, fromHand / growLength);
                float step = linkSpacing * size;
                if (walked + step > total)
                    break;

                int index = IndexAt(walked + step * 0.5f);
                Vector3 a = PointAt(walked);
                Vector3 b = PointAt(walked + step);
                Vector3 dir = b - a;
                if (dir.sqrMagnitude < 1e-10f)
                    dir = Vector3.forward;
                Vector3 up;
                if (index < _coilPointCount)
                {
                    up = (a + b) * 0.5f - TargetCell;
                    up.y = 0f;
                }
                else
                {
                    up = Vector3.Cross(dir, side);
                }

                Quaternion look = Quaternion.LookRotation(dir, up.sqrMagnitude > 1e-8f ? up : Vector3.up);
                if ((count & 1) == 1)
                    look *= Quaternion.AngleAxis(90f, Vector3.forward);
                Transform link = _links[count].transform;
                link.SetPositionAndRotation((a + b) * 0.5f, look);
                link.localScale = Vector3.one * (size * scale);
                walked += step;
                count++;
            }

            SetLinksVisible(count);
        }

        private int IndexAt(float distance)
        {
            for (int s = 1; s < _lengths.Count; s++)
            {
                if (_lengths[s] >= distance)
                    return s - 1;
            }
            return _lengths.Count - 1;
        }

        private Vector3 PointAt(float distance)
        {
            int s = IndexAt(distance) + 1;
            if (s >= _points.Count)
                return _points[_points.Count - 1];
            float span = _lengths[s] - _lengths[s - 1];
            return Vector3.Lerp(_points[s - 1], _points[s], span > 1e-6f ? (distance - _lengths[s - 1]) / span : 0f);
        }

        private Vector3 CoilPoint(float t, float radius)
        {
            float angle = _startAngle - t * coilTurns * Mathf.PI * 2f;
            float height = Mathf.Lerp(coilHeight.y, coilHeight.x, t);
            return TargetCell + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
        }

        private MeshRenderer CreateLink(string linkName)
        {
            var link = new GameObject(linkName);
            link.transform.SetParent(transform, false);
            link.AddComponent<MeshFilter>().sharedMesh = linkMesh;
            var renderer = link.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = linkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.enabled = false;
            return renderer;
        }

        private void SetLinksVisible(int count)
        {
            for (int i = 0; i < _links.Count; i++)
            {
                bool visible = i < count;
                if (_links[i].enabled != visible)
                    _links[i].enabled = visible;
            }
        }
    }
}
