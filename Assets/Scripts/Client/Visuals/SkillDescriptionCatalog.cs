using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 코어 스킬 Id → 짧은 설명(스킬을 눌러 대상을 고르는 동안 스킬 버튼 위 말풍선에 띄운다). 목록에 없는 스킬은 말풍선을 띄우지 않는다.
    /// Resources/SkillDescriptionCatalog 에 두면 자동으로 불러온다. 한 줄로 짧게 쓴다(말풍선이 길어지지 않게).
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Skill Description Catalog", fileName = "SkillDescriptionCatalog")]
    public class SkillDescriptionCatalog : ScriptableObject
    {
        public const string ResourcePath = "SkillDescriptionCatalog";

        [Serializable]
        private class Entry
        {
            [Tooltip("코어 스킬 Id(예: SCYTHE_SHADOW_CLONE).")]
            public string skillId;
            [Tooltip("말풍선에 띄울 짧은 설명.")]
            [TextArea(1, 3)] public string description;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>스킬 설명. 없으면 null.</summary>
        public string Find(string skillId)
        {
            foreach (Entry entry in entries)
            {
                if (entry.skillId == skillId && !string.IsNullOrWhiteSpace(entry.description))
                    return entry.description;
            }

            return null;
        }
    }
}
