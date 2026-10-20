using System;
using System.Collections.Generic;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 경기 안의 이펙트 주체(EffectCaster)들. 키는 유닛 Id 이며, 장판/평타 등 여러 뷰가 같은 주체의 풀 묶음을 함께 쓴다.
    /// 설치자를 모르는 효과(사망한 설치자의 장판을 복원)는 팀별 공용 키(음수)를 쓴다.
    /// </summary>
    public class EffectCasterRegistry
    {
        private readonly EffectPoolSystem _system;
        private readonly Dictionary<int, EffectCaster> _casters = new Dictionary<int, EffectCaster>();
        private readonly List<int> _disposable = new List<int>();

        public EffectCasterRegistry(EffectPoolSystem system)
        {
            _system = system;
        }

        /// <summary>key 의 주체. 없으면 name 으로 새로 만든다.</summary>
        public EffectCaster Get(int key, string name)
        {
            if (!_casters.TryGetValue(key, out EffectCaster caster))
            {
                caster = new EffectCaster(_system, name);
                _casters[key] = caster;
            }

            return caster;
        }

        /// <summary>사라진(죽은) 유닛의 주체 중, 쓰이는 이펙트가 모두 끝난 것의 풀을 지운다. 공용 키(음수)는 경기 끝까지 둔다.</summary>
        public void DisposeIdle(Predicate<int> isGone)
        {
            _disposable.Clear();
            foreach (KeyValuePair<int, EffectCaster> pair in _casters)
            {
                if (pair.Key >= 0 && pair.Value.IsIdle && isGone(pair.Key))
                    _disposable.Add(pair.Key);
            }

            foreach (int key in _disposable)
            {
                _casters[key].Dispose();
                _casters.Remove(key);
            }
        }

        /// <summary>모든 주체의 풀을 지운다(새 경기).</summary>
        public void Clear()
        {
            foreach (EffectCaster caster in _casters.Values)
                caster.Dispose();

            _casters.Clear();
        }
    }
}
