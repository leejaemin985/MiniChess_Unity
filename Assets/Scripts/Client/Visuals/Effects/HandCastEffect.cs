using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 스킬 모션이 시작될 때 시전자 손 소켓(CharacterHandRig)에 붙어 손을 따라다니는 시전 이펙트(정원사 실드의 손 빛 등).
    /// 모션 시작에서 startFrame 뒤에 파티클을 켜고, stopFrame 에 새 입자 방출을 멈춘다. 남은 입자가 사라지면 스스로 풀로 돌아간다.
    /// 프레임은 캐릭터 스킬 클립 기준(30fps)이다.
    /// </summary>
    public sealed class HandCastEffect : EffectBase
    {
        private const float FrameRate = 30f;

        [Tooltip("붙일 손.")]
        [SerializeField] private WeaponHand hand = WeaponHand.Left;
        [Tooltip("스킬 모션 시작부터 파티클을 켤 때까지의 프레임(30fps).")]
        [SerializeField, Min(0)] private int startFrame = 4;
        [Tooltip("스킬 모션 시작부터 새 입자 방출을 멈출 때까지의 프레임(30fps). 이미 나온 입자는 수명대로 사라진다.")]
        [SerializeField, Min(0)] private int stopFrame = 20;

        private float _elapsed;
        private bool _started;
        private bool _stopped;

        public WeaponHand Hand => hand;

        protected override bool ReleaseWhenFinished => true;
        protected override bool PlayParticlesOnSpawn => false;
        protected override bool IsFinished => _stopped && !IsAnyParticleAlive();

        /// <summary>손 소켓 아래 로컬 0 에 붙인다. 끝나면 풀로 돌아간다(EffectBase).</summary>
        public void Attach(Transform socket)
        {
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        protected override void OnSpawned()
        {
            _elapsed = 0f;
            _started = false;
            _stopped = false;
        }

        protected override void Update()
        {
            if (IsInUse && !IsReleasing && !_stopped)
            {
                _elapsed += Time.deltaTime;
                float frame = _elapsed * FrameRate;

                if (!_started && frame >= startFrame)
                {
                    _started = true;
                    foreach (ParticleSystem particle in Particles)
                        particle.Play(false);
                }

                if (_started && frame >= stopFrame)
                {
                    _stopped = true;
                    foreach (ParticleSystem particle in Particles)
                        particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            base.Update();
        }
    }
}
