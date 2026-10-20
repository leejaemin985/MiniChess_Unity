using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>타입을 모르는 쪽(시전자 소유 객체)이 풀을 정리할 때 쓰는 공통 면.</summary>
    public interface IEffectPool
    {
        /// <summary>꺼내져 쓰이는 중인 수(사라지는 중 포함).</summary>
        int InUseCount { get; }

        /// <summary>모든 인스턴스와 풀 오브젝트를 지운다.</summary>
        void Dispose();
    }

    /// <summary>
    /// 이펙트 프리팹 하나의 풀. 만들 때 prewarm 개를 미리 만들어 container 아래에 꺼 두고, 모자라면 하나씩 늘린다.
    /// 꺼낸 이펙트는 EffectBase.Release 로 돌려준다.
    /// </summary>
    public class EffectPool<T> : IEffectPool where T : EffectBase
    {
        private readonly T _prefab;
        private readonly Transform _container;
        private readonly Stack<T> _idle = new Stack<T>();
        private readonly List<T> _all = new List<T>();

        public EffectPool(T prefab, Transform container, int prewarm)
        {
            _prefab = prefab;
            _container = container;

            for (int i = 0; i < prewarm; i++)
                _idle.Push(CreateInstance());
        }

        public int Count => _all.Count;
        public int InUseCount => _all.Count - _idle.Count;

        public T Get()
        {
            T effect = _idle.Count > 0 ? _idle.Pop() : CreateInstance();
            effect.Spawn();
            return effect;
        }

        public void Dispose()
        {
            _idle.Clear();
            _all.Clear();
            if (_container != null)
                Object.Destroy(_container.gameObject);
        }

        private T CreateInstance()
        {
            T effect = Object.Instantiate(_prefab, _container);
            effect.name = $"{_prefab.name} {_all.Count}";
            effect.gameObject.SetActive(false);
            effect.Bind(Return);
            _all.Add(effect);
            return effect;
        }

        private void Return(EffectBase effect)
        {
            _idle.Push((T)effect);
        }
    }
}
