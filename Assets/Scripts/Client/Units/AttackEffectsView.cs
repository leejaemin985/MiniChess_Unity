using MiniChess.Client.Common;
using MiniChess.Client.Visuals.Effects;
using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 기본 공격 타격 이펙트를 맞은 자리에서 공격 방향(공격자 → 피격자)으로 재생한다. 이펙트는 공격자(EffectCaster)의 풀에서 꺼낸다.
    /// 위치는 유닛 뷰에서 읽는다(코어에서는 결정타를 맞은 유닛이 이미 보드에서 빠져 있으므로). 따라서 뷰를 새 상태로 맞추기 전에 호출해야 한다.
    /// </summary>
    public class AttackEffectsView : MonoBehaviour
    {
        [Tooltip("공격자 한 명이 미리 만들어 두는 타격 이펙트 수. 모자라면 늘어난다.")]
        [SerializeField, Min(1)] private int poolSize = 2;

        private EffectCasterRegistry _casters;
        private UnitsView _units;
        private BoardCoordinates _coordinates;

        public void Initialize(EffectCasterRegistry casters, UnitsView units)
        {
            _casters = casters;
            _units = units;
        }

        public void Build(BoardCoordinates coordinates)
        {
            _coordinates = coordinates;
        }

        /// <summary>유닛을 친 공격. 피격자 뷰가 없으면 재생하지 않는다.</summary>
        public void PlayOnUnit(HitEffect prefab, int attackerId, int targetId)
        {
            if (_units != null && _units.TryGetView(targetId, out UnitView target))
                Play(prefab, attackerId, target.transform.position);
        }

        /// <summary>칸(장애물 등)을 친 공격.</summary>
        public void PlayOnCell(HitEffect prefab, int attackerId, Position cell)
        {
            if (_coordinates != null)
                Play(prefab, attackerId, _coordinates.ToWorld(cell));
        }

        private void Play(HitEffect prefab, int attackerId, Vector3 hitPoint)
        {
            if (prefab == null || _casters == null || !_units.TryGetView(attackerId, out UnitView attacker))
                return;

            string name = $"{attacker.Unit.Stats.Base.Id}#{attacker.Unit.Id} ({attacker.Unit.Team})";
            HitEffect effect = _casters.Get(attackerId, name).GetPool(prefab, poolSize).Get();
            effect.Place(hitPoint, hitPoint - attacker.transform.position);
        }
    }
}
