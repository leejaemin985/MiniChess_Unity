using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 코어 스킬 Id → 스킬 이펙트 프리팹. 한 스킬에 종류별로 넣을 수 있고, 비운 종류는 재생하지 않는다.
    ///   - 라인(prefab): 사용 때 범위 양 끝을 잇는다(일자 베기 등).
    ///   - 시전(castEffect): 사용 때 시전자 자리에서 재생한다(박격포 발사 등).
    ///   - 타격(hitEffect): 사용 때 범위 각 칸에서 시전자 → 칸 방향으로 재생한다(강타 등).
    ///   - 투사체(projectileEffect): 사용 때 시전자에서 범위 각 칸으로 날아가 명중한다(장거리 조준 등).
    ///   - 경로(pathEffect): 사용 직후 시전자가 직접 이동한 경로(출발 칸 → 도착 칸)를 따라 재생한다(돌진 등).
    ///   - 착탄(strikeEffect): 예약 공격이 떨어질 때 범위에서 재생한다(박격포 착탄 등).
    ///   - 워프(warpEffect): 사용으로 시전자가 일으킨 순간이동/위치 교환마다 시전자/출발 칸/도착 칸에 재생하고, 옮겨지는 유닛은 가라앉았다 솟아오른다(실 관문 등).
    ///   - 밀기(pushEffect): 대상과 방향을 고르는 밀기 스킬 사용 때 대상 칸에서 고른 방향으로, 실제로 밀려난 도착 칸까지 재생한다(자리 교정 등).
    /// 목록에 없는 스킬은 이펙트 없이 시전 아이콘만 띄운다.
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
            [Tooltip("범위 양 끝을 잇는 라인 이펙트. 비우면 재생 안 함.")]
            public LineEffect prefab;
            [Tooltip("사용 때 시전자 자리에서 재생하는 이펙트. 비우면 재생 안 함.")]
            public CastEffect castEffect;
            [Tooltip("사용 때 범위 각 칸에서 시전자 → 칸 방향으로 재생하는 타격 이펙트. 비우면 재생 안 함.")]
            public HitEffect hitEffect;
            [Tooltip("사용 때 시전자에서 범위 각 칸으로 날아가는 투사체 이펙트. 비우면 재생 안 함.")]
            public ProjectileEffect projectileEffect;
            [Tooltip("사용 직후 시전자가 이동한 경로(출발 → 도착)를 따라 재생하는 이펙트. 비우면 재생 안 함.")]
            public LineEffect pathEffect;
            [Tooltip("예약 공격이 떨어질 때 범위에서 재생하는 이펙트. 비우면 재생 안 함.")]
            public StrikeEffect strikeEffect;
            [Tooltip("사용으로 시전자가 일으킨 순간이동(출발 → 도착)마다 재생하는 이펙트. 지정하면 옮겨지는 유닛이 가라앉았다 솟아오른다. 비우면 재생 안 함(바로 옮겨짐).")]
            public WarpEffect warpEffect;
            [Tooltip("대상과 방향을 고르는 밀기 스킬 사용 때 대상 칸에서 고른 방향으로 재생하는 이펙트(실제 도착 칸 포함). 비우면 재생 안 함.")]
            public PushEffect pushEffect;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>스킬의 라인 이펙트 프리팹. 없으면 null.</summary>
        public LineEffect Find(string skillId)
        {
            return FindEntry(skillId, entry => entry.prefab != null)?.prefab;
        }

        /// <summary>스킬의 시전 이펙트 프리팹. 없으면 null.</summary>
        public CastEffect FindCast(string skillId)
        {
            return FindEntry(skillId, entry => entry.castEffect != null)?.castEffect;
        }

        /// <summary>스킬의 타격 이펙트 프리팹. 없으면 null.</summary>
        public HitEffect FindHit(string skillId)
        {
            return FindEntry(skillId, entry => entry.hitEffect != null)?.hitEffect;
        }

        /// <summary>스킬의 투사체 이펙트 프리팹. 없으면 null.</summary>
        public ProjectileEffect FindProjectile(string skillId)
        {
            return FindEntry(skillId, entry => entry.projectileEffect != null)?.projectileEffect;
        }

        /// <summary>스킬의 경로 이펙트 프리팹. 없으면 null.</summary>
        public LineEffect FindPath(string skillId)
        {
            return FindEntry(skillId, entry => entry.pathEffect != null)?.pathEffect;
        }

        /// <summary>스킬의 착탄 이펙트 프리팹. 없으면 null.</summary>
        public StrikeEffect FindStrike(string skillId)
        {
            return FindEntry(skillId, entry => entry.strikeEffect != null)?.strikeEffect;
        }

        /// <summary>스킬의 워프 이펙트 프리팹. 없으면 null.</summary>
        public WarpEffect FindWarp(string skillId)
        {
            return FindEntry(skillId, entry => entry.warpEffect != null)?.warpEffect;
        }

        /// <summary>스킬의 밀기 이펙트 프리팹. 없으면 null.</summary>
        public PushEffect FindPush(string skillId)
        {
            return FindEntry(skillId, entry => entry.pushEffect != null)?.pushEffect;
        }

        private Entry FindEntry(string skillId, Predicate<Entry> hasEffect)
        {
            foreach (Entry entry in entries)
            {
                if (entry.skillId == skillId && hasEffect(entry))
                    return entry;
            }

            return null;
        }
    }
}
