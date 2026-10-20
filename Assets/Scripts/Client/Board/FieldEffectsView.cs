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
    /// 장판(AreaEffect 층) 칸에 이펙트를 깐다. 이펙트는 설치한 시전자(EffectCaster)의 풀에서 꺼내고, 장판이 사라지면 돌려준다.
    /// 깔린 이펙트는 칸별로 추적하고, 어느 장판의 것인지는 구역 Id 로 구분한다
    /// (코어는 Unity 를 모르고, 온라인은 상태가 바뀔 때마다 구역 객체가 새로 만들어지므로 코어 객체에 이펙트를 붙이지 않는다).
    /// 풀 크기는 시전자 스킬의 동시 구역 수 × 범위 칸 수 × poolMultiplier 이다.
    /// </summary>
    public class FieldEffectsView : MonoBehaviour
    {
        [Tooltip("풀 여유 배수. 사라지는 중인 이펙트와 새 이펙트가 겹쳐도 모자라지 않게 한다.")]
        [SerializeField, Min(1)] private int poolMultiplier = 2;
        [Tooltip("스킬 정의에서 크기를 알 수 없을 때(설치자 사망 후 복원 등)의 풀 크기.")]
        [SerializeField, Min(1)] private int fallbackPoolSize = 8;

        private readonly Dictionary<int, EffectCaster> _casters = new Dictionary<int, EffectCaster>();

        /// <summary>칸마다 지금 깔린 장판 이펙트와 그 구역 Id.</summary>
        private readonly Dictionary<Position, (int ZoneId, FieldEffect Effect)> _cellEffects = new Dictionary<Position, (int, FieldEffect)>();
        private readonly HashSet<Position> _seen = new HashSet<Position>();
        private readonly List<Position> _emptied = new List<Position>();
        private readonly List<int> _disposable = new List<int>();

        private EffectPoolSystem _pools;
        private FieldEffectCatalog _catalog;
        private BoardCoordinates _coordinates;
        private GameState _state;

        public void Initialize(EffectPoolSystem pools, FieldEffectCatalog catalog)
        {
            _pools = pools;
            _catalog = catalog;
        }

        /// <summary>이 종류의 장판을 이펙트로 그리는지(그리면 칸 색조는 쓰지 않는다).</summary>
        public bool HasVisual(string kind)
        {
            return _catalog != null && _catalog.Find(kind) != null;
        }

        /// <summary>새 경기: 이전 경기의 이펙트와 풀을 모두 지운다.</summary>
        public void Build(BoardCoordinates coordinates)
        {
            Clear();
            _coordinates = coordinates;
        }

        /// <summary>
        /// 장판 이펙트를 현재 코어 상태대로 맞춘다.
        ///   새 장판: 시전자 풀에서 꺼내 깐다. 다른 장판이 있던 칸이면 이전 이펙트를 바로 끈다(덮어쓰기).
        ///   장판이 사라진 칸(만료 등): 이전 이펙트를 서서히 끄고 풀로 돌려준다.
        /// </summary>
        public void Sync(GameState state)
        {
            _state = state;
            if (_pools == null || _catalog == null)
                return;

            _seen.Clear();
            foreach (BoardCell cell in state.Board.AllCells())
            {
                if (!(cell.GetEffect(CellEffectLayer.AreaEffect) is ZoneCellEffect area))
                    continue;

                FieldEffect prefab = _catalog.Find(area.Kind);
                if (prefab == null)
                    continue;

                _seen.Add(cell.Position);
                if (_cellEffects.TryGetValue(cell.Position, out var current))
                {
                    if (current.ZoneId == area.Zone.Id)
                        continue;

                    current.Effect.ReleaseImmediate();
                }

                _cellEffects[cell.Position] = (area.Zone.Id, Spawn(cell.Position, area.Zone, prefab));
            }

            _emptied.Clear();
            foreach (Position position in _cellEffects.Keys)
            {
                if (!_seen.Contains(position))
                    _emptied.Add(position);
            }

            foreach (Position position in _emptied)
            {
                _cellEffects[position].Effect.Release();
                _cellEffects.Remove(position);
            }
        }

        public void Clear()
        {
            foreach (EffectCaster caster in _casters.Values)
                caster.Dispose();

            _casters.Clear();
            _cellEffects.Clear();
            _state = null;
        }

        private FieldEffect Spawn(Position cell, FieldZone zone, FieldEffect prefab)
        {
            int casterKey = GetCasterKey(zone);
            if (!_casters.TryGetValue(casterKey, out EffectCaster caster))
            {
                caster = new EffectCaster(_pools, GetCasterName(zone));
                _casters[casterKey] = caster;
            }

            FieldEffect effect = caster.GetPool(prefab, GetPoolSize(zone.SkillId)).Get();
            effect.Place(_coordinates.ToWorld(cell));
            return effect;
        }

        /// <summary>시전자 유닛 Id. 설치자를 모르는 구역(사망한 설치자의 구역을 복원)은 팀별 공용 키(음수).</summary>
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

        /// <summary>죽은 시전자의 풀은 남은 이펙트가 모두 사라진 뒤 지운다.</summary>
        private void Update()
        {
            if (_state == null || _casters.Count == 0)
                return;

            _disposable.Clear();
            foreach (KeyValuePair<int, EffectCaster> pair in _casters)
            {
                if (pair.Key < 0 || !pair.Value.IsIdle)
                    continue;

                Unit unit = _state.FindUnit(pair.Key);
                if (unit == null || !unit.IsAlive)
                    _disposable.Add(pair.Key);
            }

            foreach (int key in _disposable)
            {
                _casters[key].Dispose();
                _casters.Remove(key);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
