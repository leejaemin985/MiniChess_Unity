using System.Collections.Generic;
using System.Linq;
using MiniChess.Client.Common;
using MiniChess.Client.Visuals.Effects;
using MiniChess.Core.Common;
using MiniChess.Core.Effects;
using MiniChess.Core.Effects.Zones;
using MiniChess.Core.Skills;
using MiniChess.Core.Skills.Areas;
using MiniChess.Core.Skills.Conditions;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 칸 효과(장판 층, 덫 층)가 있는 칸에 이펙트를 깐다. 이펙트는 설치한 시전자(EffectCaster)의 풀에서 꺼내고, 칸 효과가 사라지면 돌려준다.
    /// 깔린 이펙트는 칸·층별로 추적하고, 어느 칸 효과의 것인지는 구역 Id 로 구분한다
    /// (코어는 Unity 를 모르고, 온라인은 상태가 바뀔 때마다 구역 객체가 새로 만들어지므로 코어 객체에 이펙트를 붙이지 않는다).
    /// 상태에 없는 칸 효과(숨겨진 상대 덫)는 깔지 않는다. 발동 순간의 연출은 PlayTrigger 로 따로 재생한다.
    /// 풀 크기는 시전자 스킬의 동시 구역 수 × 범위 칸 수 × poolMultiplier 이다.
    /// </summary>
    public class FieldEffectsView : MonoBehaviour
    {
        /// <summary>이펙트를 까는 칸 효과 층.</summary>
        private static readonly CellEffectLayer[] Layers = { CellEffectLayer.AreaEffect, CellEffectLayer.Trap };

        [Tooltip("풀 여유 배수. 사라지는 중인 이펙트와 새 이펙트가 겹쳐도 모자라지 않게 한다.")]
        [SerializeField, Min(1)] private int poolMultiplier = 2;
        [Tooltip("스킬 정의에서 크기를 알 수 없을 때(설치자 사망 후 복원 등)의 풀 크기.")]
        [SerializeField, Min(1)] private int fallbackPoolSize = 8;

        /// <summary>칸·층마다 지금 깔린 이펙트와 그 구역 Id.</summary>
        private readonly Dictionary<(Position, CellEffectLayer), (int ZoneId, FieldEffect Effect)> _cellEffects =
            new Dictionary<(Position, CellEffectLayer), (int, FieldEffect)>();
        private readonly HashSet<(Position, CellEffectLayer)> _seen = new HashSet<(Position, CellEffectLayer)>();
        private readonly List<(Position, CellEffectLayer)> _emptied = new List<(Position, CellEffectLayer)>();

        private EffectCasterRegistry _casters;
        private FieldEffectCatalog _catalog;
        private BoardCoordinates _coordinates;
        private GameState _state;

        public void Initialize(EffectCasterRegistry casters, FieldEffectCatalog catalog)
        {
            _casters = casters;
            _catalog = catalog;
        }

        /// <summary>이 종류의 칸 효과를 이펙트로 그리는지(그리면 칸 색조는 쓰지 않고, 덫 표식은 테두리로 줄인다).</summary>
        public bool HasVisual(string kind)
        {
            return _catalog != null && _catalog.Find(kind) != null;
        }

        /// <summary>새 경기: 이전 경기의 이펙트 기록을 지운다(풀은 EffectCasterRegistry.Clear 로 함께 지운다).</summary>
        public void Build(BoardCoordinates coordinates)
        {
            Clear();
            _coordinates = coordinates;
        }

        /// <summary>
        /// 칸 효과 이펙트를 현재 코어 상태대로 맞춘다(층마다 따로).
        ///   새 칸 효과: 시전자 풀에서 꺼내 깐다. 같은 층에 다른 칸 효과가 있던 칸이면 이전 이펙트를 바로 끈다(덮어쓰기).
        ///   칸 효과가 사라진 칸(만료, 덫 소모 등): 이전 이펙트를 서서히 끄고 풀로 돌려준다.
        /// </summary>
        public void Sync(GameState state)
        {
            _state = state;
            if (_casters == null || _catalog == null)
                return;

            _seen.Clear();
            foreach (BoardCell cell in state.Board.AllCells())
            {
                foreach (CellEffectLayer layer in Layers)
                {
                    if (!(cell.GetEffect(layer) is ZoneCellEffect effect))
                        continue;

                    FieldEffect prefab = _catalog.Find(effect.Kind);
                    if (prefab == null)
                        continue;

                    var key = (cell.Position, layer);
                    _seen.Add(key);
                    if (_cellEffects.TryGetValue(key, out var current))
                    {
                        if (current.ZoneId == effect.Zone.Id)
                            continue;

                        current.Effect.ReleaseImmediate();
                    }

                    _cellEffects[key] = (effect.Zone.Id, Spawn(cell.Position, effect.Zone, prefab));
                }
            }

            _emptied.Clear();
            foreach (var key in _cellEffects.Keys)
            {
                if (!_seen.Contains(key))
                    _emptied.Add(key);
            }

            foreach (var key in _emptied)
            {
                _cellEffects[key].Effect.Release();
                _cellEffects.Remove(key);
            }
        }

        /// <summary>
        /// 칸 효과가 발동한 순간의 연출(덫이 닫힘 등)을 그 칸에서 재생한다. 숨겨져 있던 상대 덫도 발동은 보인다.
        /// 발동 이벤트는 설치자를 알려 주지 않으므로 설치한 팀의 공용 풀에서 꺼낸다.
        /// </summary>
        public void PlayTrigger(string kind, Position cell, Team? ownerTeam)
        {
            TriggerEffect prefab = _catalog != null ? _catalog.FindTrigger(kind) : null;
            if (prefab == null || _casters == null || _coordinates == null)
                return;

            int key = ownerTeam.HasValue ? -1 - (int)ownerTeam.Value : -3;
            string name = ownerTeam.HasValue ? $"Unknown ({ownerTeam.Value})" : "Unknown";
            _casters.Get(key, name).GetPool(prefab, poolMultiplier).Get().Place(_coordinates.ToWorld(cell));
        }

        public void Clear()
        {
            _cellEffects.Clear();
            _state = null;
        }

        private FieldEffect Spawn(Position cell, FieldZone zone, FieldEffect prefab)
        {
            EffectCaster caster = _casters.Get(GetCasterKey(zone), GetCasterName(zone));
            FieldEffect effect = caster.GetPool(prefab, GetPoolSize(zone.SkillId)).Get();
            effect.Place(_coordinates.ToWorld(cell));
            return effect;
        }

        /// <summary>시전자 유닛 Id. 설치자를 모르는 구역(사망한 설치자의 구역을 복원)은 팀별 공용 키(음수, EffectCasterRegistry 참고).</summary>
        private static int GetCasterKey(FieldZone zone)
        {
            return zone.Source != null ? zone.Source.Id : -1 - (int)zone.OwnerTeam;
        }

        private static string GetCasterName(FieldZone zone)
        {
            return zone.Source != null
                ? $"{zone.Source.Stats.Base.Id}#{zone.Source.Id} ({zone.OwnerTeam})"
                : $"Unknown ({zone.OwnerTeam})";
        }

        /// <summary>동시 구역 수 × 범위 칸 수 × poolMultiplier. 스킬 정의에서 알 수 없으면 fallbackPoolSize.</summary>
        private int GetPoolSize(string skillId)
        {
            SkillDefinition skill = _state?.Skills.Find(skillId);
            int? maxActive = skill?.Conditions.OfType<MaxActiveZonesCondition>().FirstOrDefault()?.Max;
            AreaShape shape = (skill?.Area as PatternArea)?.Shape;
            if (maxActive == null || shape == null)
                return fallbackPoolSize;

            return maxActive.Value * shape.GetOffsets(AreaRotation.R0).Count() * poolMultiplier;
        }
    }
}
