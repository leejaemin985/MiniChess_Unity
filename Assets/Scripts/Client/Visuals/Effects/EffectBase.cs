using System;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 풀링되는 이펙트의 기반. EffectPool 이 만들고 꺼내며(Spawn), 다 쓰면 Release 로 돌려준다.
    /// Release 는 파티클 방출만 멈추고, 남은 입자가 모두 사라진 뒤(또는 releaseTimeout 이 지나면) 비활성화해 풀로 돌아간다.
    /// </summary>
    public abstract class EffectBase : MonoBehaviour
    {
        [Tooltip("Release 후 남은 입자를 기다리는 최대 시간(초). 지나면 바로 풀로 돌아간다.")]
        [SerializeField, Min(0f)] private float releaseTimeout = 3f;

        private ParticleSystem[] _particles;
        private Action<EffectBase> _returnToPool;
        private float? _releaseDeadline;

        /// <summary>풀에서 꺼내져 쓰이는 중인지(사라지는 중 포함).</summary>
        public bool IsInUse { get; private set; }

        /// <summary>Release 되어 남은 입자가 사라지기를 기다리는 중인지.</summary>
        public bool IsReleasing => _releaseDeadline.HasValue;

        internal void Bind(Action<EffectBase> returnToPool)
        {
            _returnToPool = returnToPool;
        }

        /// <summary>풀에서 꺼낼 때 EffectPool 이 호출한다. 처음부터 다시 재생한다.</summary>
        internal void Spawn()
        {
            IsInUse = true;
            _releaseDeadline = null;
            gameObject.SetActive(true);

            foreach (ParticleSystem particle in Particles)
            {
                particle.Clear(false);
                if (PlayParticlesOnSpawn)
                    particle.Play(false);
            }

            OnSpawned();
        }

        /// <summary>방출을 멈추고, 남은 입자가 사라지면 풀로 돌아간다.</summary>
        public void Release()
        {
            if (!IsInUse || IsReleasing)
                return;

            _releaseDeadline = Time.time + releaseTimeout;
            foreach (ParticleSystem particle in Particles)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            OnReleased();
        }

        /// <summary>남은 입자를 기다리지 않고 바로 풀로 돌려준다.</summary>
        public void ReleaseImmediate()
        {
            if (!IsInUse)
                return;

            if (!IsReleasing)
                OnReleased();

            Despawn();
        }

        protected virtual void OnSpawned() { }

        protected virtual void OnReleased() { }

        /// <summary>재생이 끝나면(IsFinished) 스스로 풀로 돌아가는 일회성 이펙트인지.</summary>
        protected virtual bool ReleaseWhenFinished => false;

        /// <summary>꺼낼 때 모든 파티클을 바로 재생하는지. 파티클을 직접 재생하는 이펙트(타이밍 연출 등)는 false.</summary>
        protected virtual bool PlayParticlesOnSpawn => true;

        /// <summary>재생이 끝났는지(기본: 살아 있는 입자가 없음). 파티클 외 연출이 있으면 그것까지 포함해 판단한다.</summary>
        protected virtual bool IsFinished => !IsAnyParticleAlive();

        protected ParticleSystem[] Particles => _particles ?? (_particles = GetComponentsInChildren<ParticleSystem>(true));

        /// <summary>파생 클래스가 Update 를 쓰면 base.Update() 를 호출해야 반납 처리가 된다.</summary>
        protected virtual void Update()
        {
            if (IsReleasing)
            {
                if (Time.time >= _releaseDeadline.Value || !IsAnyParticleAlive())
                    Despawn();
                return;
            }

            if (IsInUse && ReleaseWhenFinished && IsFinished)
                Despawn();
        }

        protected bool IsAnyParticleAlive()
        {
            foreach (ParticleSystem particle in Particles)
            {
                if (particle.IsAlive(false))
                    return true;
            }

            return false;
        }

        private void Despawn()
        {
            IsInUse = false;
            _releaseDeadline = null;
            gameObject.SetActive(false);
            _returnToPool?.Invoke(this);
        }
    }
}
