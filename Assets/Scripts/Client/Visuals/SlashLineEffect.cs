using System;
using MiniChess.Client.Visuals.Effects;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 두 지점을 잇는 슬래시. 빛줄기 머리가 시작점에서 끝점으로 쓸고 지나가고 꼬리가 뒤따르며,
    /// 지나간 자리에 잔광이 남고 끝점에서 타격 파티클이 터진다.
    /// 모양은 경과 시간만으로 정해지므로(Evaluate) 풀링해서 Play 를 반복 호출해도 된다.
    /// 풀에서는 Place 로 재생하고, 빛줄기와 파티클이 모두 끝나면 스스로 돌아간다. 파티클은 직접 재생한다(타격은 끝점 도착 때).
    /// </summary>
    public class SlashLineEffect : LineEffect
    {
        [Header("Preset (Play() 로 재생)")]
        [SerializeField] private Transform fromPoint;
        [SerializeField] private Transform toPoint;
        [SerializeField] private bool playOnStart = true;

        [Tooltip("넘겨받은 좌표에 더하는 높이. 셀 중심(y = 0)을 넘기면 기물 높이에서 그려진다.")]
        [SerializeField] private float heightOffset = 0.4f;

        [Header("Timing")]
        [SerializeField] private float sweepDuration = 0.12f;
        [Tooltip("꼬리가 머리를 따라오기까지의 지연.")]
        [SerializeField] private float tailDelay = 0.06f;
        [SerializeField] private float afterglowDuration = 0.25f;
        [SerializeField] private AnimationCurve sweepCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("Parts")]
        [SerializeField] private LineRenderer streak;
        [SerializeField] private LineRenderer afterglow;
        [SerializeField] private ParticleSystem trailSparks;
        [SerializeField] private ParticleSystem impact;
        [SerializeField] private float sparksPerUnit = 10f;

        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private float _sparkCarry;
        private bool _impactPlayed;
        private Color _afterglowStart;
        private Color _afterglowEnd;

        public bool IsPlaying { get; private set; }

        public float Duration => sweepDuration + Mathf.Max(tailDelay, afterglowDuration);

        /// <summary>재생이 끝나면 호출된다(풀 반환 등).</summary>
        public event Action Finished;

        private void Awake()
        {
            _afterglowStart = afterglow.startColor;
            _afterglowEnd = afterglow.endColor;
            SetLinesVisible(false);
        }

        private void Start()
        {
            // 풀에서 꺼낸 것은 Place 로 이미 재생 중이므로 프리셋 재생을 하지 않는다.
            if (playOnStart && !IsInUse && fromPoint != null && toPoint != null)
                Play();
        }

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !IsPlaying && !IsAnyParticleAlive();

        public override void Place(Vector3 from, Vector3 to)
        {
            Play(from, to);
        }

        protected override void OnReleased()
        {
            Stop();
        }

        /// <summary>Inspector 에 지정한 두 Transform 사이로 재생한다.</summary>
        public void Play()
        {
            Play(fromPoint.position, toPoint.position);
        }

        /// <summary>월드 좌표 두 점 사이로 재생한다. heightOffset 이 더해진다.</summary>
        public void Play(Vector3 from, Vector3 to)
        {
            _from = from + Vector3.up * heightOffset;
            _to = to + Vector3.up * heightOffset;
            _elapsed = 0f;
            _sparkCarry = 0f;
            _impactPlayed = false;
            IsPlaying = true;

            if (impact != null)
            {
                impact.transform.position = _to;
                impact.Clear(true);
            }

            SetLinesVisible(true);
            Evaluate(0f);
        }

        public void Stop()
        {
            IsPlaying = false;
            SetLinesVisible(false);
        }

        protected override void Update()
        {
            base.Update();
            if (!IsPlaying)
                return;

            float previousHead = HeadProgress(_elapsed);
            _elapsed += Time.deltaTime;
            float head = HeadProgress(_elapsed);

            EmitTrailSparks(previousHead, head);
            Evaluate(_elapsed);

            if (!_impactPlayed && _elapsed >= sweepDuration)
            {
                _impactPlayed = true;
                if (impact != null)
                    impact.Play(true);
            }

            if (_elapsed >= Duration)
            {
                Stop();
                Finished?.Invoke();
            }
        }

        /// <summary>경과 시간 time 에서의 빛줄기/잔광 모양을 적용한다.</summary>
        public void Evaluate(float time)
        {
            float head = HeadProgress(time);
            float tail = HeadProgress(time - tailDelay);

            streak.SetPosition(0, PointAt(tail));
            streak.SetPosition(1, PointAt(head));
            streak.enabled = head - tail > 0.001f;

            // 잔광: 시작점부터 머리까지 드러나고, 머리가 도착한 뒤 서서히 사라진다.
            float fade = 1f - Mathf.Clamp01((time - sweepDuration) / afterglowDuration);
            afterglow.SetPosition(0, _from);
            afterglow.SetPosition(1, PointAt(head));
            afterglow.startColor = ScaleAlpha(_afterglowStart, fade);
            afterglow.endColor = ScaleAlpha(_afterglowEnd, fade);
            afterglow.enabled = fade > 0f && head > 0.001f;
        }

        private float HeadProgress(float time)
        {
            return sweepCurve.Evaluate(Mathf.Clamp01(time / sweepDuration));
        }

        private Vector3 PointAt(float progress)
        {
            return Vector3.LerpUnclamped(_from, _to, progress);
        }

        private void EmitTrailSparks(float fromProgress, float toProgress)
        {
            if (trailSparks == null || toProgress <= fromProgress)
                return;

            float distance = (toProgress - fromProgress) * Vector3.Distance(_from, _to);
            _sparkCarry += distance * sparksPerUnit;

            var emitParams = new ParticleSystem.EmitParams();
            while (_sparkCarry >= 1f)
            {
                _sparkCarry -= 1f;
                emitParams.position = PointAt(UnityEngine.Random.Range(fromProgress, toProgress));
                emitParams.applyShapeToPosition = true;
                trailSparks.Emit(emitParams, 1);
            }
        }

        private void SetLinesVisible(bool visible)
        {
            streak.enabled = visible;
            afterglow.enabled = visible;
        }

        private static Color ScaleAlpha(Color color, float scale)
        {
            color.a *= scale;
            return color;
        }
    }
}
