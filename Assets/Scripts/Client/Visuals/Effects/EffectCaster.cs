using System.Collections.Generic;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 이펙트 주체(시전자 하나)가 가진 풀 묶음. 프리팹마다 풀 하나를 처음 쓸 때 만든다.
    /// 유닛 뷰와 따로 살아서, 시전자가 죽어도 남은 장판 이펙트는 유지된다(모두 사라진 뒤 Dispose).
    /// </summary>
    public class EffectCaster
    {
        private readonly EffectPoolSystem _system;
        private readonly Dictionary<EffectBase, IEffectPool> _pools = new Dictionary<EffectBase, IEffectPool>();

        public EffectCaster(EffectPoolSystem system, string name)
        {
            _system = system;
            Name = name;
        }

        /// <summary>풀 오브젝트 이름(예: "FLAME#3 (Player1)").</summary>
        public string Name { get; }

        /// <summary>쓰이는 중인 이펙트가 하나도 없는지(사라지는 중 포함).</summary>
        public bool IsIdle
        {
            get
            {
                foreach (IEffectPool pool in _pools.Values)
                {
                    if (pool.InUseCount > 0)
                        return false;
                }

                return true;
            }
        }

        /// <summary>prefab 의 풀. 없으면 prewarm 개를 미리 만든 풀을 새로 만든다.</summary>
        public EffectPool<T> GetPool<T>(T prefab, int prewarm) where T : EffectBase
        {
            if (!_pools.TryGetValue(prefab, out IEffectPool pool))
            {
                pool = _system.CreatePool(prefab, Name, prewarm);
                _pools[prefab] = pool;
            }

            return (EffectPool<T>)pool;
        }

        public void Dispose()
        {
            foreach (IEffectPool pool in _pools.Values)
                pool.Dispose();

            _pools.Clear();
        }
    }
}
