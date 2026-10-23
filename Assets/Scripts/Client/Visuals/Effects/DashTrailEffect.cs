using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 돌진 경로(FX_Dash). 출발 칸 흙먼지, 경로 바닥 은빛 줄기와 속도선, 미끄러지는 유닛 뒤에 남는 잔상, 도착 직후 한 칸 앞 베기 섬광.
    /// 유닛은 UnitsView.dashDuration 동안 미끄러지므로 travelTime 을 그 값과 맞춘다(잔상 위치, 앞 베기 시점).
    /// 하위 오브젝트 방향: start 는 +Z 가 뒤(출발 반대쪽), path/front 는 +Z 가 돌진 방향이 되게 놓는다.
    /// </summary>
    public sealed class DashTrailEffect : LineEffect
    {
        [Tooltip("출발 칸 흙먼지(+Z 가 뒤쪽으로 놓인다).")]
        [SerializeField] private Transform start;
        [Tooltip("경로 전체를 덮는 부분. 경로 중간에 놓이고 +Z 가 돌진 방향, 로컬 Z 크기를 경로 길이에 맞춘다.")]
        [SerializeField] private Transform path;
        [Tooltip("경로 줄기 파티클(크기 3D, Y 가 길이). 시작 크기 Y 를 경로 길이로 맞춘다.")]
        [SerializeField] private ParticleSystem streak;
        [Tooltip("속도선 파티클(Box 모양). 모양의 Z 크기를 경로 길이로 맞춘다.")]
        [SerializeField] private ParticleSystem speedLines;
        [Tooltip("잔상(캡슐 메시 파티클, World 시뮬레이션, 자체 방출 없음).")]
        [SerializeField] private ParticleSystem ghost;
        [Tooltip("도착 직후 한 칸 앞 베기 루트. +Z 가 돌진 방향으로 놓이고 travelTime 이 지나면 재생한다.")]
        [SerializeField] private ParticleSystem front;

        [Tooltip("유닛이 미끄러지는 시간(초). UnitsView.dashDuration 과 맞춘다.")]
        [SerializeField, Min(0.01f)] private float travelTime = 0.15f;
        [Tooltip("잔상을 남기는 간격(초).")]
        [SerializeField, Min(0.01f)] private float ghostInterval = 0.035f;
        [Tooltip("잔상 높이(캡슐 중심).")]
        [SerializeField] private float ghostHeight = 0.5f;
        [Tooltip("앞 베기 거리(월드 단위, 보통 칸 한 개).")]
        [SerializeField] private float frontDistance = 1f;

        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private float _nextGhost;
        private bool _playing;
        private bool _frontPlayed;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        public override void Place(Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
            Vector3 direction = to - from;
            direction.y = 0f;
            float length = direction.magnitude;
            Quaternion facing = length > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;

            transform.SetPositionAndRotation(from, Quaternion.identity);
            if (start != null)
                start.SetPositionAndRotation(from, facing * Quaternion.Euler(0f, 180f, 0f));
            if (path != null)
                path.SetPositionAndRotation((from + to) * 0.5f, facing);
            if (streak != null)
            {
                var main = streak.main;
                main.startSizeY = length + 0.6f;
            }
            if (speedLines != null)
            {
                var shape = speedLines.shape;
                shape.scale = new Vector3(shape.scale.x, shape.scale.y, length);
            }
            if (front != null)
                front.transform.SetPositionAndRotation(to + facing * Vector3.forward * frontDistance, facing);

            _elapsed = 0f;
            _nextGhost = 0f;
            _playing = true;
            _frontPlayed = false;

            if (start != null)
            {
                foreach (ParticleSystem particle in start.GetComponentsInChildren<ParticleSystem>())
                    particle.Play(false);
            }

            foreach (ParticleSystem particle in new[] { streak, speedLines, ghost })
            {
                if (particle != null)
                    particle.Play(false);
            }
        }

        protected override void OnReleased()
        {
            _playing = false;
        }

        protected override void Update()
        {
            if (_playing)
            {
                _elapsed += Time.deltaTime;

                // 미끄러지는 유닛(UnitView 와 같은 감속 곡선) 뒤에 잔상을 남긴다.
                while (ghost != null && _nextGhost <= Mathf.Min(_elapsed, travelTime))
                {
                    float t = Mathf.Clamp01(_nextGhost / travelTime);
                    float eased = 1f - (1f - t) * (1f - t);
                    var emit = new ParticleSystem.EmitParams { position = Vector3.Lerp(_from, _to, eased) + Vector3.up * ghostHeight };
                    ghost.Emit(emit, 1);
                    _nextGhost += ghostInterval;
                }

                if (!_frontPlayed && _elapsed >= travelTime)
                {
                    _frontPlayed = true;
                    if (front != null)
                        front.Play(true);
                }

                if (_frontPlayed)
                    _playing = false;
            }

            base.Update();
        }
    }
}
