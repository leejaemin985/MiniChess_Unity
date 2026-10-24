using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 재봉사 장애물(FX_ThreadWeb). 칸 네 모서리에 시침핀(pins)이 위에서 꽂히고, 핀 사이로 마법실이 차례로 감겨
    /// 칸 둘레 네 면과 윗면 대각선을 그물처럼 막는다(재봉사 일러스트의 골목을 막은 실 그물). 실 매듭엔 빛 점(knots)이 맺히고 핀 끝엔 실 끝이 늘어진다.
    /// 맞으면(React) 그물이 출렁이며 불티(hitSparks)가 튀고, 부서지면(Release) 실이 툭 끊겨 풀리고 핀이 가라앉으며 실 조각(snap)이 흩어진다.
    /// 실은 strandTemplate 을 복제한 LineRenderer 들이며, 칸 크기 1 기준 로컬 좌표로 그린다.
    /// </summary>
    public sealed class ThreadWebEffect : FieldEffect
    {
        /// <summary>실 한 가닥: a → b 를 sag 만큼 처지게 잇는다. 감기는 순서대로 나열한다.</summary>
        private readonly struct Strand
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly float Sag;

            public Strand(Vector3 a, Vector3 b, float sag)
            {
                A = a;
                B = b;
                Sag = sag;
            }
        }

        [Tooltip("실 모양(재질/폭/색). 숨겨 두고 가닥마다 복제한다. 시작/끝 색을 가닥마다 번갈아 쓴다.")]
        [SerializeField] private LineRenderer strandTemplate;
        [Tooltip("모서리 시침핀 4개(앞왼, 앞오, 뒤오, 뒤왼 순). 위에서 떨어져 꽂힌다.")]
        [SerializeField] private Transform[] pins = new Transform[4];
        [SerializeField] private ParticleSystem pinFlash;
        [SerializeField] private ParticleSystem knots;
        [SerializeField] private ParticleSystem shimmer;
        [SerializeField] private ParticleSystem hitSparks;
        [SerializeField] private ParticleSystem snap;

        [Header("Shape (칸 크기 1 기준)")]
        [Tooltip("핀이 꽂히는 모서리의 중심에서의 거리.")]
        [SerializeField] private float corner = 0.38f;
        [Tooltip("핀 높이(실을 거는 꼭대기).")]
        [SerializeField] private float pinHeight = 0.55f;
        [SerializeField, Range(2, 24)] private int segments = 10;

        [Header("Timing")]
        [Tooltip("핀이 떨어져 꽂히는 시간(초). 핀마다 pinStagger 만큼 늦게 떨어진다.")]
        [SerializeField, Min(0.01f)] private float pinDropTime = 0.12f;
        [SerializeField, Min(0f)] private float pinStagger = 0.05f;
        [SerializeField] private float pinDropHeight = 0.6f;
        [Tooltip("가닥이 하나 감기기 시작하는 간격(초).")]
        [SerializeField, Min(0f)] private float weaveStep = 0.02f;
        [Tooltip("가닥 하나가 끝까지 감기는 시간(초).")]
        [SerializeField, Min(0.01f)] private float weaveTime = 0.1f;
        [SerializeField, Min(0.01f)] private float shakeTime = 0.45f;
        [SerializeField] private float shakeAmplitude = 0.06f;
        [SerializeField] private float shakeFrequency = 28f;
        [SerializeField, Min(0.01f)] private float unravelTime = 0.4f;

        private readonly List<Strand> _strands = new List<Strand>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly List<int> _unravelFromEnd = new List<int>();
        private Vector3[] _pinRest;
        private Vector3[] _pinScale;
        private Color _colorA, _colorB;
        private float _elapsed;
        private float _shake = -1f;
        private float _unravel = -1f;
        private bool _pinFlashed;
        private bool _knotsEmitted;
        private bool _settled;

        protected override bool PlayParticlesOnSpawn => false;

        private float WeaveStart => pinDropTime + pinStagger * (pins.Length - 1);
        private float WeaveEnd => WeaveStart + weaveStep * (_strands.Count - 1) + weaveTime;

        private void Awake()
        {
            BuildStrands();
            _colorA = strandTemplate.startColor;
            _colorB = strandTemplate.endColor;
            strandTemplate.enabled = false;
            for (int i = 0; i < _strands.Count; i++)
            {
                LineRenderer line = Instantiate(strandTemplate, strandTemplate.transform.parent);
                line.name = $"Strand {i}";
                line.useWorldSpace = false;
                line.positionCount = segments + 1;
                line.enabled = false;
                _lines.Add(line);
                _unravelFromEnd.Add(i % 2);
            }

            _pinRest = new Vector3[pins.Length];
            _pinScale = new Vector3[pins.Length];
            for (int i = 0; i < pins.Length; i++)
            {
                _pinRest[i] = pins[i].localPosition;
                _pinScale[i] = pins[i].localScale;
            }
        }

        protected override void OnSpawned()
        {
            _elapsed = 0f;
            _shake = -1f;
            _unravel = -1f;
            _pinFlashed = false;
            _knotsEmitted = false;
            _settled = false;
            shimmer.Play(false);
            Apply();
        }

        /// <summary>맞았다: 그물이 출렁이고 불티가 튄다.</summary>
        public override void React()
        {
            if (!IsInUse || IsReleasing)
                return;

            _shake = 0f;
            _settled = false;
            hitSparks.Play(false);
        }

        protected override void OnReleased()
        {
            // 부서짐/만료: 실이 끊겨 풀리고 핀이 가라앉는다. 맺힌 매듭 빛은 바로 지우고, 흩어지는 실 조각이 남는 동안 반납을 기다린다.
            _unravel = 0f;
            _settled = false;
            knots.Clear(false);
            snap.Play(false);
        }

        protected override void Update()
        {
            if (IsInUse && !_settled)
            {
                _elapsed += Time.deltaTime;
                if (_shake >= 0f)
                    _shake += Time.deltaTime;
                if (_unravel >= 0f)
                    _unravel += Time.deltaTime;
                Apply();
            }

            base.Update();
        }

        private void Apply()
        {
            float unravel = _unravel < 0f ? 0f : Mathf.Clamp01(_unravel / unravelTime);
            ApplyPins(unravel);

            float shake = 0f;
            if (_shake >= 0f && _shake < shakeTime)
            {
                float t = _shake / shakeTime;
                shake = shakeAmplitude * (1f - t) * (1f - t) * Mathf.Sin(_shake * shakeFrequency);
            }

            for (int i = 0; i < _strands.Count; i++)
            {
                float reach = Mathf.Clamp01((_elapsed - WeaveStart - weaveStep * i) / weaveTime);
                reach = 1f - (1f - reach) * (1f - reach);
                DrawStrand(i, reach, shake, unravel);
            }

            if (!_knotsEmitted && _unravel < 0f && _elapsed >= WeaveEnd)
            {
                _knotsEmitted = true;
                EmitKnots();
            }

            // 다 감겼고 흔들림도 끝났으면 다시 그리지 않는다(풀리는 중엔 반납까지 계속 그린다).
            bool shaking = _shake >= 0f && _shake < shakeTime;
            _settled = _unravel < 0f && _knotsEmitted && !shaking;
        }

        private void ApplyPins(float unravel)
        {
            for (int i = 0; i < pins.Length; i++)
            {
                float drop = Mathf.Clamp01((_elapsed - pinStagger * i) / pinDropTime);
                Vector3 position = _pinRest[i] + Vector3.up * (pinDropHeight * (1f - drop * drop));
                position += Vector3.down * (pinHeight * unravel);
                pins[i].localPosition = position;
                pins[i].localScale = _pinScale[i] * (1f - unravel);
                pins[i].gameObject.SetActive(_elapsed >= pinStagger * i && unravel < 1f);
            }

            if (!_pinFlashed && _elapsed >= pinDropTime)
            {
                _pinFlashed = true;
                pinFlash.Play(false);
            }
        }

        // 가닥 i 를 reach 만큼 그린다. 흔들림은 가운데가 가장 크게 위아래로, 풀릴 땐 한쪽 끝에서 감겨 들어가며 처지고 흐려진다.
        private void DrawStrand(int i, float reach, float shake, float unravel)
        {
            LineRenderer line = _lines[i];
            Strand strand = _strands[i];
            float shown = reach * (1f - unravel);
            line.enabled = shown > 0f;
            if (!line.enabled)
                return;

            bool fromEnd = _unravelFromEnd[i] == 1 && unravel > 0f;
            float sag = strand.Sag + unravel * 0.15f;
            float phase = i * 0.7f;
            for (int k = 0; k <= segments; k++)
            {
                float u = (float)k / segments * shown;
                if (fromEnd)
                    u = 1f - shown + (float)k / segments * shown;

                float bow = Mathf.Sin(Mathf.PI * u);
                Vector3 point = Vector3.Lerp(strand.A, strand.B, u) + Vector3.down * (sag * bow);
                point += Vector3.up * (shake * bow * Mathf.Cos(phase));
                line.SetPosition(k, point);
            }

            Color start = i % 2 == 0 ? _colorA : _colorB;
            Color end = i % 2 == 0 ? _colorB : _colorA;
            start.a *= 1f - unravel;
            end.a *= 1f - unravel;
            line.startColor = start;
            line.endColor = end;
        }

        // 매듭 빛: 네 면 가로 실이 핀에 묶인 자리(첫 KnotStrands 가닥의 시작점)와 윗면 대각선 가운데.
        private const int KnotStrands = 12;
        private const int TopDiagonal = 20;

        private void EmitKnots()
        {
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < KnotStrands && i < _strands.Count; i++)
            {
                emit.position = _strands[i].A;
                knots.Emit(emit, 1);
            }

            if (TopDiagonal < _strands.Count)
            {
                Strand diagonal = _strands[TopDiagonal];
                emit.position = (diagonal.A + diagonal.B) * 0.5f + Vector3.down * diagonal.Sag;
                knots.Emit(emit, 1);
            }
        }

        private Vector3[] Corners(float y)
        {
            float c = corner;
            return new[] { new Vector3(-c, y, -c), new Vector3(c, y, -c), new Vector3(c, y, c), new Vector3(-c, y, c) };
        }

        // 감기는 순서: 네 면의 가로 실(아래→위) → 네 면의 X 자 → 윗면/안쪽 대각선 → 엉킨 실 → 핀 끝에 늘어진 실 끝.
        // 손으로 감은 듯 걸리는 높이와 처짐을 가닥마다 조금씩 흔든다(고정 시드라 매번 같은 모양).
        private void BuildStrands()
        {
            _strands.Clear();
            var random = new System.Random(7);
            float h = pinHeight;
            float Jitter(float range) => ((float)random.NextDouble() * 2f - 1f) * range;
            Vector3 At(int corner, float level) => Corners(h * Mathf.Clamp(level + Jitter(0.06f), 0.05f, 0.95f))[corner % 4];
            void Add(Vector3 a, Vector3 b, float sag) => _strands.Add(new Strand(a, b, sag + Jitter(sag * 0.6f)));

            foreach (float level in new[] { 0.28f, 0.55f, 0.82f })
            {
                for (int i = 0; i < 4; i++)
                    Add(At(i, level), At(i + 1, level), 0.03f);
            }

            for (int i = 0; i < 4; i++)
            {
                Add(At(i, 0.15f), At(i + 1, 0.88f), 0.02f);
                Add(At(i, 0.88f), At(i + 1, 0.15f), 0.02f);
            }

            Add(At(0, 0.88f), At(2, 0.88f), 0.07f);
            Add(At(1, 0.88f), At(3, 0.88f), 0.07f);
            Add(At(0, 0.45f), At(2, 0.45f), 0.05f);
            Add(At(1, 0.45f), At(3, 0.45f), 0.05f);

            // 엉킨 실: 마주 보는 모서리를 높이를 엇갈려 잇고 크게 처지게 한다.
            Add(At(0, 0.7f), At(2, 0.3f), 0.1f);
            Add(At(1, 0.25f), At(3, 0.75f), 0.1f);
            Add(At(2, 0.65f), At(0, 0.2f), 0.08f);
            Add(At(3, 0.35f), At(1, 0.7f), 0.08f);

            Vector3[] top = Corners(h * 0.95f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 outward = new Vector3(top[i].x, 0f, top[i].z).normalized * 0.06f;
                _strands.Add(new Strand(top[i], top[i] + outward + Vector3.down * (h * (0.3f + Jitter(0.08f))), -0.02f));
            }
        }
    }
}
