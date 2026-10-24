using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Client.Units;
using MiniChess.Client.Visuals.Effects;
using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 스킬 이펙트를 재생한다. 이펙트는 시전자(EffectCaster)의 풀에서 꺼낸다.
    ///   - 라인(Play): 범위 칸 순서(모양 정의 순)의 첫 칸에서 마지막 칸으로 쓸고 지나가며, 양 끝 칸의 바깥 가장자리까지 늘린다.
    ///   - 시전(PlayCast): 시전자 자리에서 재생한다.
    ///   - 타격(PlayHit): 범위 각 칸에서 시전자 → 칸 방향으로 재생한다.
    ///   - 투사체(PlayProjectile): 시전자에서 범위 각 칸으로 날아가 명중한다.
    ///   - 경로(PlayPath): 시전자가 직접 이동한 경로의 출발 칸에서 도착 칸까지 재생한다.
    ///   - 착탄(PlayStrike): 범위 중심에 놓고 각 칸 위치를 함께 넘긴다.
    ///   - 워프(PlayWarp): 시전자가 일으킨 순간이동마다 시전자/출발 칸/도착 칸을 넘긴다.
    ///   - 소환/소멸(PlaySummon/PlayVanish): 소환자 캐릭터 데이터(CharacterVisual)의 소환물 이펙트. 소환은 소환자 → 소환 칸, 소멸은 사라진 자리.
    ///   - 밀기(PlayPush): 대상과 방향을 고르는 밀기 스킬에서 대상 칸 → 고른 방향으로, 실제 도착 칸까지 재생한다.
    /// </summary>
    public class SkillEffectsView : MonoBehaviour
    {
        [Tooltip("시전자 한 명이 스킬 이펙트별로 미리 만들어 두는 수. 모자라면 늘어난다.")]
        [SerializeField, Min(1)] private int poolSize = 2;

        private EffectCasterRegistry _casters;
        private SkillEffectCatalog _catalog;
        private UnitsView _units;
        private BoardCoordinates _coordinates;

        public void Initialize(EffectCasterRegistry casters, SkillEffectCatalog catalog, UnitsView units)
        {
            _casters = casters;
            _catalog = catalog;
            _units = units;
        }

        public void Build(BoardCoordinates coordinates)
        {
            _coordinates = coordinates;
        }

        /// <summary>스킬 사용 연출. 이펙트가 없는 스킬이거나 범위가 한 칸뿐이면 재생하지 않는다.</summary>
        public void Play(int casterId, string skillId, IReadOnlyList<Position> cells)
        {
            LineEffect prefab = _catalog != null ? _catalog.Find(skillId) : null;
            if (prefab == null || _casters == null || _coordinates == null || cells == null || cells.Count < 2)
                return;

            Vector3 from = _coordinates.ToWorld(cells[0]);
            Vector3 to = _coordinates.ToWorld(cells[cells.Count - 1]);
            Vector3 edge = (to - from).normalized * (_coordinates.CellSize * 0.5f);

            LineEffect effect = _casters.Get(casterId, GetCasterName(casterId)).GetPool(prefab, poolSize).Get();
            effect.Place(from - edge, to + edge);
        }

        /// <summary>스킬 사용 때 시전자 자리에서 재생하는 연출(박격포 발사 등). 이펙트가 없거나 시전자가 보드에 없으면 재생하지 않는다.</summary>
        public void PlayCast(int casterId, string skillId)
        {
            CastEffect prefab = _catalog != null ? _catalog.FindCast(skillId) : null;
            if (prefab == null || _casters == null || _coordinates == null || _units == null)
                return;
            if (!_units.TryGetView(casterId, out UnitView view) || !view.Unit.IsPlaced)
                return;

            CastEffect effect = _casters.Get(casterId, GetCasterName(casterId)).GetPool(prefab, poolSize).Get();
            effect.Place(_coordinates.ToWorld(view.Unit.Position.Value));
        }

        /// <summary>
        /// 스킬 사용 때 범위 각 칸에서 시전자 → 칸 방향으로 재생하는 타격 연출(강타 등).
        /// 이펙트가 없거나 시전자가 보드에 없으면 재생하지 않는다.
        /// </summary>
        public void PlayHit(int casterId, string skillId, IReadOnlyList<Position> cells)
        {
            HitEffect prefab = _catalog != null ? _catalog.FindHit(skillId) : null;
            if (prefab == null || _casters == null || _coordinates == null || _units == null || cells == null)
                return;
            if (!_units.TryGetView(casterId, out UnitView caster) || !caster.Unit.IsPlaced)
                return;

            EffectPool<HitEffect> pool = _casters.Get(casterId, GetCasterName(casterId)).GetPool(prefab, poolSize);
            foreach (Position cell in cells)
            {
                Vector3 hitPoint = _coordinates.ToWorld(cell);
                pool.Get().Place(hitPoint, hitPoint - caster.transform.position);
            }
        }

        /// <summary>
        /// 스킬 사용 때 시전자에서 범위 각 칸으로 날아가는 투사체 연출(장거리 조준 등).
        /// pushedTo 는 범위 칸에 있던 유닛이 이 스킬로 밀려 도착한 칸(밀리지 않았으면 없음)이며, 밀린 자리에 이어 붙이는 연출(충격탄 등)에 넘긴다.
        /// 이펙트가 없거나 시전자가 보드에 없으면 재생하지 않는다.
        /// </summary>
        public void PlayProjectile(int casterId, string skillId, IReadOnlyList<Position> cells, IReadOnlyDictionary<Position, Position> pushedTo = null)
        {
            ProjectileEffect prefab = _catalog != null ? _catalog.FindProjectile(skillId) : null;
            if (prefab == null || _casters == null || _coordinates == null || _units == null || cells == null)
                return;
            if (!_units.TryGetView(casterId, out UnitView caster) || !caster.Unit.IsPlaced)
                return;

            Vector3 from = _coordinates.ToWorld(caster.Unit.Position.Value);
            EffectPool<ProjectileEffect> pool = _casters.Get(casterId, GetCasterName(casterId)).GetPool(prefab, poolSize);
            foreach (Position cell in cells)
            {
                ProjectileEffect effect = pool.Get();
                effect.Place(from, _coordinates.ToWorld(cell));
                if (pushedTo != null && pushedTo.TryGetValue(cell, out Position destination))
                    effect.SetPushedTo(_coordinates.ToWorld(destination));
            }
        }

        /// <summary>
        /// 스킬로 시전자가 일으킨 순간이동(from 칸 → to 칸) 연출(실 관문 등)을 재생하고 그 이펙트를 돌려준다(옮겨지는 유닛의 가라앉기 시점에 쓴다).
        /// 이펙트가 없거나 시전자 뷰가 없으면 재생하지 않고 null.
        /// </summary>
        public WarpEffect PlayWarp(int casterId, string skillId, Position from, Position to)
        {
            WarpEffect prefab = _catalog != null ? _catalog.FindWarp(skillId) : null;
            if (prefab == null || _casters == null || _coordinates == null || _units == null)
                return null;
            if (!_units.TryGetView(casterId, out UnitView caster))
                return null;

            WarpEffect effect = _casters.Get(casterId, GetCasterName(casterId)).GetPool(prefab, poolSize).Get();
            effect.Place(caster.transform.position, _coordinates.ToWorld(from), _coordinates.ToWorld(to));
            return effect;
        }

        /// <summary>소환 연출(분신: 그림자가 소환 칸으로 미끄러져 가 연기로 솟음)을 소환자 → 소환 칸으로 재생한다. 소환물 데이터가 없거나 소환자 뷰가 없으면 재생하지 않는다.</summary>
        public void PlaySummon(int ownerId, Position cell)
        {
            LineEffect prefab = _units != null ? _units.FindSummonVisualByOwner(ownerId)?.SummonSpawnEffect : null;
            if (prefab == null || _casters == null || _coordinates == null || !_units.TryGetView(ownerId, out UnitView owner))
                return;

            _casters.Get(ownerId, GetCasterName(ownerId)).GetPool(prefab, poolSize).Get()
                .Place(owner.transform.position, _coordinates.ToWorld(cell));
        }

        /// <summary>소환물이 사라지는(사망) 연출을 그 자리에서 재생한다. 소환물이 아니거나 데이터가 없으면 재생하지 않는다. 뷰를 숨기기 전에 호출한다.</summary>
        public void PlayVanish(int unitId)
        {
            if (_units == null || _casters == null || !_units.TryGetView(unitId, out UnitView view))
                return;

            CastEffect prefab = _units.FindSummonVisual(view.Unit)?.SummonVanishEffect;
            if (prefab != null)
                _casters.Get(unitId, GetCasterName(unitId)).GetPool(prefab, 1).Get().Place(view.transform.position);
        }

        /// <summary>
        /// 대상과 방향을 고르는 밀기 스킬(자리 교정 등) 연출. targets[0] 이 대상 칸, targets[1] 쪽이 미는 방향이다.
        /// pushedTo 는 이 스킬로 밀려난 유닛의 처음 칸 → 도착 칸이며, 대상 칸이 없으면 막혀서 안 밀린 것으로 본다. 이펙트가 없으면 재생하지 않는다.
        /// </summary>
        public void PlayPush(int casterId, string skillId, IReadOnlyList<Position> targets, IReadOnlyDictionary<Position, Position> pushedTo)
        {
            PushEffect prefab = _catalog != null ? _catalog.FindPush(skillId) : null;
            if (prefab == null || _casters == null || _coordinates == null || targets == null || targets.Count < 2)
                return;

            Position target = targets[0];
            Position landing = pushedTo != null && pushedTo.TryGetValue(target, out Position to) ? to : target;
            int cells = System.Math.Max(System.Math.Abs(landing.X - target.X), System.Math.Abs(landing.Y - target.Y));
            Vector3 from = _coordinates.ToWorld(target);

            _casters.Get(casterId, GetCasterName(casterId)).GetPool(prefab, poolSize).Get()
                .Place(from, _coordinates.ToWorld(targets[1]) - from, _coordinates.ToWorld(landing), cells);
        }

        /// <summary>스킬 사용 직후 시전자가 이동한 경로(from 칸 → to 칸)를 따라 재생하는 연출(돌진 등). 이펙트가 없으면 재생하지 않는다.</summary>
        public void PlayPath(int casterId, string skillId, Position from, Position to)
        {
            LineEffect prefab = _catalog != null ? _catalog.FindPath(skillId) : null;
            if (prefab == null || _casters == null || _coordinates == null || from.Equals(to))
                return;

            _casters.Get(casterId, GetCasterName(casterId)).GetPool(prefab, poolSize).Get()
                .Place(_coordinates.ToWorld(from), _coordinates.ToWorld(to));
        }

        /// <summary>예약 공격이 떨어질 때 범위 중심과 각 칸에서 재생하는 연출(박격포 착탄 등). 이펙트가 없거나 범위가 비면 재생하지 않는다.</summary>
        public void PlayStrike(int casterId, string skillId, IReadOnlyList<Position> cells)
        {
            StrikeEffect prefab = _catalog != null ? _catalog.FindStrike(skillId) : null;
            if (prefab == null || _casters == null || _coordinates == null || cells == null || cells.Count == 0)
                return;

            var points = new List<Vector3>(cells.Count);
            Vector3 center = Vector3.zero;
            foreach (Position cell in cells)
            {
                Vector3 point = _coordinates.ToWorld(cell);
                points.Add(point);
                center += point;
            }

            StrikeEffect effect = _casters.Get(casterId, GetCasterName(casterId)).GetPool(prefab, poolSize).Get();
            effect.Place(center / points.Count, points);
        }

        private string GetCasterName(int casterId)
        {
            return _units != null && _units.TryGetView(casterId, out UnitView view)
                ? $"{view.Unit.Stats.Base.Id}#{view.Unit.Id} ({view.Unit.Team})"
                : $"Unit#{casterId}";
        }
    }
}
