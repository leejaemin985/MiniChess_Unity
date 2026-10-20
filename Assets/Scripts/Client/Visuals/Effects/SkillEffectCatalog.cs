using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 코어 스킬 Id → 범위 양 끝을 잇는 이펙트 프리팹(일자 베기 등). 목록에 없는 스킬은 이펙트 없이 시전 아이콘만 띄운다.
    /// GameSession 에 직접 연결하거나, Resources/SkillEffectCatalog 에 두면 자동으로 불러온다.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Skill Effect Catalog", fileName = "SkillEffectCatalog")]
    public class SkillEffectCatalog : ScriptableObject
    {
        public const string ResourcePath = "SkillEffectCatalog";

        [Serializable]
        private class Entry
        {
            [Tooltip("코어 스킬 Id(예: SCYTHE_SLASH).")]
            public string skillId;
            public LineEffect prefab;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>스킬의 이펙트 프리팹. 없으면 null.</summary>
        public LineEffect Find(string skillId)
        {
            foreach (Entry entry in entries)
            {
                if (entry.prefab != null && entry.skillId == skillId)
                    return entry.prefab;
            }

            return null;
        }
    }
}
