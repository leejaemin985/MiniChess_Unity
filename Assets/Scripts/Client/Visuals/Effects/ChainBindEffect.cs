using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 사슬 속박(FX_ChainBind). 쐐기(projectile)가 시전자 손에서 대상으로 날아가며 사슬 고리가 뒤로 이어지고,
    /// 도착하면 사슬이 대상 쪽으로 빨려 들어가며 몸을 나선으로 감아 조이고(tighten), 쐐기는 대상 앞 바닥에 박힌다(anchor).
    /// hold 동안 묶인 뒤 고리가 작아지며 사라진다. 고리는 linkMesh 로 실행 중에 만든다(프리팹에 두지 않는다).
    /// </summary>
    public sealed class ChainBindEffect : ProjectileEffect
    {
        [Header("Links")]
        [SerializeField] private Mesh linkMesh;
        [SerializeField] private Material linkMaterial;
        [Tooltip("고리 사이 간격(월드 단위).")]
        [SerializeField, Min(0.01f)] private float linkSpacing = 0.12f;
        [Tooltip("시전자 → 대상 사슬에 쓰는 고리 최대 개수.")]
        [SerializeField, Min(1)] private int maxPathLinks = 32;

        [Header("Coil")]
        [Tooltip("감기는 사슬의 아래/위 높이(대상 칸 윗면 기준).")]
        [SerializeField] private Vector2 coilHeight = new Vector2(0.2f, 0.85f);
        [SerializeField, Min(0.1f)] private float coilTurns = 2.2f;
        [Tooltip("감기 시작할 때 반지름 → 조인 뒤 반지름.")]
        [SerializeField] private Vector2 coilRadius = new Vector2(0.6f, 0.36f);

        [Header("Anchor")]
        [Tooltip("쐐기가 박히는 바닥 지점: 대상 칸 중심에서 시전자 쪽으로의 거리.")]
        [SerializeField] private float anchorDistance = 0.45f;
        [Tooltip("쐐기가 바닥에 박혔을 때 재생(바닥 위치로 옮긴다).")]
        [SerializeField] private ParticleSystem anchor;
        [Tooltip("다 조였을 때 재생(대상 몸 중심으로 옮긴다).")]
        [SerializeField] private ParticleSystem tighten;

        [Header("Timing")]
        [Tooltip("사슬이 빨려 들어가며 감기는 시간(초).")]
        [SerializeField, Min(0.01f)] private float wrapTime = 0.25f;
        [Tooltip("쐐기가 바닥에 박히는 시간(초).")]
        [SerializeField, Min(0.01f)] private float plungeTime = 0.08f;
        [SerializeField, Min(0f)] private float holdTime = 0.7f;
        [SerializeField, Min(0.01f)] private float fadeTime = 0.25f;

        private readonly List<MeshRenderer> _pathLinks = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _coilLinks = new List<MeshRenderer>();
        private Vector3 _anchorPoint;
        private Quaternion _headFlight;
        private float _bindElapsed;
        private bool _binding;
        private bool _tightened;
        private bool _anchored;

        protected override bool IsFinished => base.IsFinished && !_binding;

        private void Awake()
        {
            int coilCount = Mathf.CeilToInt(CoilLength(coilRadius.y) / linkSpacing);
            for (int i = 0; i < maxPathLinks; i++)
                _pathLinks.Add(CreateLink("PathLink" + i));
            for (int i = 0; i < coilCount; i++)
                _coilLinks.Add(CreateLink("CoilLink" + i));
        }

        protected override void OnLaunched()
        {
            _binding = false;
            _tightened = false;
            _anchored = false;
            _headFlight = Projectile.rotation;
            Projectile.localScale = Vector3.one;
            SetLinksVisible(_coilLinks, 0);
        }

        protected override void OnArrived()
        {
            _binding = true;
            _bindElapsed = 0f;

            Vector3 toCaster = LaunchPoint - TargetCell;
            toCaster.y = 0f;
            _anchorPoint = TargetCell + (toCaster.sqrMagnitude > 0.0001f ? toCaster.normalized * anchorDistance : Vector3.zero);
            if (anchor != null)
                anchor.transform.position = _anchorPoint;
            if (tighten != null)
                tighten.transform.position = TargetCell + Vector3.up * Mathf.Lerp(coilHeight.x, coilHeight.y, 0.5f);
        }

        protected override void OnReleased()
        {
            base.OnReleased();
            _binding = false;
            SetLinksVisible(_pathLinks, 0);
            SetLinksVisible(_coilLinks, 0);
        }

        protected override void Update()
        {
            base.Update();

            if (IsFlying)
            {
                LayPath(LaunchPoint, Projectile.position);
                return;
            }

            if (!_binding)
                return;

            _bindElapsed += Time.deltaTime;
            float wrap = Mathf.Clamp01(_bindElapsed / wrapTime);
            float eased = 1f - (1f - wrap) * (1f - wrap);

            // 시전자 쪽 끝이 대상 쪽으로 빨려 들어간다.
            LayPath(Vector3.Lerp(LaunchPoint, HitPoint, eased), HitPoint);

            float fade = Mathf.Clamp01((_bindElapsed - wrapTime - holdTime) / fadeTime);
            float scale = 1f - fade * fade;
            LayCoil(eased, wrap < 1f ? Mathf.Lerp(coilRadius.x, coilRadius.y, eased) : TightRadius(_bindElapsed - wrapTime), scale);

            float plunge = Mathf.Clamp01(_bindElapsed / plungeTime);
            Projectile.position = Vector3.Lerp(HitPoint, _anchorPoint, plunge * plunge) + Vector3.down * (0.12f * fade);
            Projectile.rotation = Quaternion.Slerp(_headFlight, Quaternion.LookRotation(Vector3.down, _headFlight * Vector3.forward), plunge);
            Projectile.localScale = Vector3.one * scale;

            if (!_anchored && plunge >= 1f)
            {
                _anchored = true;
                if (anchor != null)
                    anchor.Play(true);
            }

            if (!_tightened && wrap >= 1f)
            {
                _tightened = true;
                if (tighten != null)
                    tighten.Play(true);
            }

            if (fade >= 1f)
            {
                _binding = false;
                SetLinksVisible(_coilLinks, 0);
                SetLinksVisible(_pathLinks, 0);
            }
        }

        // 다 감긴 뒤 "철컥": 한 번 더 조였다가 제자리로 돌아온다.
        private float TightRadius(float afterWrap)
        {
            float snap = Mathf.Sin(Mathf.Clamp01(afterWrap / 0.12f) * Mathf.PI);
            return coilRadius.y - 0.04f * snap;
        }

        // from → to 직선에 고리를 늘어놓는다(to 쪽 끝을 기준으로 맞춘다).
        private void LayPath(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            int count = Mathf.Min(_pathLinks.Count, Mathf.FloorToInt(length / linkSpacing));
            if (count > 0)
            {
                Quaternion facing = Quaternion.LookRotation(direction / length);
                for (int i = 0; i < count; i++)
                {
                    Transform link = _pathLinks[i].transform;
                    link.position = to - direction / length * (linkSpacing * (i + 0.5f));
                    link.rotation = facing * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 90f);
                    link.localScale = Vector3.one;
                }
            }

            SetLinksVisible(_pathLinks, count);
        }

        // 대상 몸 둘레 나선에 고리를 늘어놓는다. 위(시전자 쪽 방향)에서부터 visible 비율만큼 보인다.
        private void LayCoil(float visible, float radius, float scale)
        {
            Vector3 toCaster = LaunchPoint - TargetCell;
            float startAngle = Mathf.Atan2(toCaster.z, toCaster.x);
            int count = Mathf.Min(_coilLinks.Count, Mathf.CeilToInt(_coilLinks.Count * visible));
            int total = _coilLinks.Count;
            for (int i = 0; i < count; i++)
            {
                float t0 = (float)i / total;
                float t1 = (float)(i + 1) / total;
                Vector3 a = CoilPoint(t0, radius, startAngle);
                Vector3 b = CoilPoint(t1, radius, startAngle);
                Transform link = _coilLinks[i].transform;
                link.position = (a + b) * 0.5f;
                link.rotation = Quaternion.LookRotation(b - a) * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 90f);
                link.localScale = Vector3.one * scale;
            }

            SetLinksVisible(_coilLinks, count);
        }

        private Vector3 CoilPoint(float t, float radius, float startAngle)
        {
            float angle = startAngle - t * coilTurns * Mathf.PI * 2f;
            float height = Mathf.Lerp(coilHeight.y, coilHeight.x, t);
            return TargetCell + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
        }

        private float CoilLength(float radius)
        {
            float around = coilTurns * Mathf.PI * 2f * radius;
            float rise = coilHeight.y - coilHeight.x;
            return Mathf.Sqrt(around * around + rise * rise);
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

        private static void SetLinksVisible(List<MeshRenderer> links, int count)
        {
            for (int i = 0; i < links.Count; i++)
            {
                bool visible = i < count;
                if (links[i].enabled != visible)
                    links[i].enabled = visible;
            }
        }
    }
}
