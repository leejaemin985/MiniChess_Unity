using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 그림자 분신 소환(FX_ShadowCloneSpawn). 소환자 발밑 그림자(shadow, 바닥에 눕힌 짙은 보라 얼룩 + 연기 꼬리)가 떨어져 나와
    /// 바닥을 타고 소환 칸으로 미끄러져 가고(slideTime), 닿는 순간 보라 연기가 솟구친다(burst). 분신 몸체는 그때부터 바닥에서 솟아오른다
    /// (CharacterVisual.summonAppearDelay 를 slideTime 과 맞춘다).
    /// </summary>
    public sealed class ShadowCloneSpawnEffect : LineEffect
    {
        [Tooltip("미끄러져 가는 그림자(하위 파티클 포함, 연기 꼬리는 월드 시뮬레이션). 출발할 때 재생하고 닿으면 방출을 멈춘다.")]
        [SerializeField] private ParticleSystem shadow;
        [Tooltip("소환 칸에서 솟구치는 연기(하위 파티클 포함). 그림자가 닿을 때 재생한다.")]
        [SerializeField] private ParticleSystem burst;
        [SerializeField, Min(0.01f)] private float slideTime = 0.25f;

        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private bool _sliding;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_sliding && !IsAnyParticleAlive();

        public override void Place(Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
            transform.SetPositionAndRotation(from, Quaternion.identity);
            shadow.transform.position = from;
            burst.transform.position = to;
            _elapsed = 0f;
            _sliding = true;
            shadow.Play(true);
        }

        protected override void OnReleased()
        {
            _sliding = false;
        }

        protected override void Update()
        {
            if (_sliding)
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / slideTime);
                float eased = t * t * (3f - 2f * t);
                shadow.transform.position = Vector3.Lerp(_from, _to, eased);
                if (t >= 1f)
                {
                    _sliding = false;
                    shadow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    burst.Play(true);
                }
            }

            base.Update();
        }
    }
}
