using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 코어 칸 효과 종류(CellEffectKinds) → 장판 이펙트 프리팹. 목록에 없는 종류는 칸 색조로만 표시한다.
    /// GameSession 에 직접 연결하거나, Resources/FieldEffectCatalog 에 두면 자동으로 불러온다.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Field Effect Catalog", fileName = "FieldEffectCatalog")]
    public class FieldEffectCatalog : ScriptableObject
    {
        public const string ResourcePath = "FieldEffectCatalog";

        [Serializable]
        private class Entry
        {
            [Tooltip("코어 CellEffectKinds 값(예: DAMAGE_FIELD, POISON_FIELD).")]
            public string kind;
            public FieldEffect prefab;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>종류의 이펙트 프리팹. 없으면 null.</summary>
        public FieldEffect Find(string kind)
        {
            foreach (Entry entry in entries)
            {
                if (entry.prefab != null && entry.kind == kind)
                    return entry.prefab;
            }

            return null;
        }
    }
}
