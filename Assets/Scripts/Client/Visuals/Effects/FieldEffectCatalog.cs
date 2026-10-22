using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 코어 칸 효과 종류(CellEffectKinds) → 칸 이펙트 프리팹. 목록에 없는 종류는 칸 색조/표식으로만 표시한다.
    ///   - 상주(prefab): 칸 효과가 있는 동안 깔려 있다(장판, 설치된 덫).
    ///   - 발동(triggerEffect): 칸 효과가 발동한 순간 그 칸에서 한 번 재생한다(덫이 닫힘 등). 숨겨진 상대 덫도 발동은 보인다.
    /// GameSession 에 직접 연결하거나, Resources/FieldEffectCatalog 에 두면 자동으로 불러온다.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Field Effect Catalog", fileName = "FieldEffectCatalog")]
    public class FieldEffectCatalog : ScriptableObject
    {
        public const string ResourcePath = "FieldEffectCatalog";

        [Serializable]
        private class Entry
        {
            [Tooltip("코어 CellEffectKinds 값(예: DAMAGE_FIELD, POISON_FIELD, ROOT_TRAP).")]
            public string kind;
            [Tooltip("칸 효과가 있는 동안 깔리는 이펙트. 비우면 칸 색조/표식으로 표시.")]
            public FieldEffect prefab;
            [Tooltip("칸 효과가 발동할 때 한 번 재생하는 이펙트. 비우면 재생 안 함.")]
            public TriggerEffect triggerEffect;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>종류의 상주 이펙트 프리팹. 없으면 null.</summary>
        public FieldEffect Find(string kind)
        {
            foreach (Entry entry in entries)
            {
                if (entry.prefab != null && entry.kind == kind)
                    return entry.prefab;
            }

            return null;
        }

        /// <summary>종류의 발동 이펙트 프리팹. 없으면 null.</summary>
        public TriggerEffect FindTrigger(string kind)
        {
            foreach (Entry entry in entries)
            {
                if (entry.triggerEffect != null && entry.kind == kind)
                    return entry.triggerEffect;
            }

            return null;
        }
    }
}
