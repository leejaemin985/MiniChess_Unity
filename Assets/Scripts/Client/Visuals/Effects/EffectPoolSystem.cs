using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 모든 이펙트 풀 오브젝트의 최상단 부모. 풀은 이펙트(프리팹)별 그룹 아래, 소유자 이름의 오브젝트로 만든다.
    ///   EffectPoolSystem / FX_FireField / FLAME#3 (Player1) / FX_FireField 0, 1, ...
    /// 풀 자체는 이펙트 주체(EffectCaster)가 갖고, 이 클래스는 오브젝트 배치만 맡는다.
    /// </summary>
    public class EffectPoolSystem : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> _groups = new Dictionary<string, Transform>();

        public static EffectPoolSystem Create(string name = "EffectPoolSystem")
        {
            return new GameObject(name).AddComponent<EffectPoolSystem>();
        }

        public EffectPool<T> CreatePool<T>(T prefab, string ownerName, int prewarm) where T : EffectBase
        {
            var owner = new GameObject(ownerName).transform;
            owner.SetParent(GetGroup(prefab.name), false);
            return new EffectPool<T>(prefab, owner, prewarm);
        }

        private Transform GetGroup(string effectName)
        {
            if (!_groups.TryGetValue(effectName, out Transform group) || group == null)
            {
                group = new GameObject(effectName).transform;
                group.SetParent(transform, false);
                _groups[effectName] = group;
            }

            return group;
        }
    }
}
