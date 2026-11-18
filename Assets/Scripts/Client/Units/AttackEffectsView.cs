using System.Collections;
using MiniChess.Client.Common;
using MiniChess.Client.Visuals.Effects;
using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 기본 공격 타격 이펙트를 맞은 자리에서 공격 방향(공격자 → 피격자)으로 재생한다(이펙트가 AttachToAttacker 면 공격자 오브젝트 아래에 붙여 재생). 이펙트는 공격자(EffectCaster)의 풀에서 꺼낸다.
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

        /// <summary>
        /// 유닛을 친 공격. 피격자 뷰가 없으면 재생하지 않는다.
        /// delay 가 있으면 맞은 자리는 지금 읽어 두고 delay 초 뒤에 재생한다(돌진 도착 뒤 등). 공격 방향은 재생할 때 공격자 자리로 정한다.
        /// </summary>
        public void PlayOnUnit(HitEffect prefab, int attackerId, int targetId, float delay = 0f)
        {
            if (_units == null || !_units.TryGetView(targetId, out UnitView target))
                return;

            if (delay > 0f)
                StartCoroutine(PlayLater(prefab, attackerId, target.transform.position, delay));
            else
                Play(prefab, attackerId, target.transform.position);
        }

        /// <summary>칸(장애물 등)을 친 공격.</summary>
        public void PlayOnCell(HitEffect prefab, int attackerId, Position cell)
        {
            if (_coordinates != null)
                Play(prefab, attackerId, _coordinates.ToWorld(cell));
        }

        /// <summary>
        /// 기본 공격 투사체(화살 등)를 delay 초 뒤에 공격자 자리 → target(칸 윗면 높이 월드 좌표)으로 쏜다. 날아가는 시간(초)을 돌려준다.
        /// 쏠 수 없으면(이펙트/공격자 뷰 없음) 0 을 돌려주고 쏘지 않는다.
        /// </summary>
        public float PlayProjectile(ProjectileEffect prefab, int attackerId, Vector3 target, float delay)
        {
            if (prefab == null || _casters == null || _units == null || !_units.TryGetView(attackerId, out UnitView attacker))
                return 0f;

            Vector3 from = attacker.transform.position;
            float flightTime = prefab.GetFlightTime(from, target);
            StartCoroutine(LaunchLater(prefab, attackerId, from, target, delay));
            return flightTime;
        }

        /// <summary>기다리는 중인 타격 이펙트를 모두 취소한다(새 경기 등).</summary>
        public void CancelPending()
        {
            StopAllCoroutines();
        }

        private IEnumerator PlayLater(HitEffect prefab, int attackerId, Vector3 hitPoint, float delay)
        {
            yield return new WaitForSeconds(delay);
            Play(prefab, attackerId, hitPoint);
        }

        private IEnumerator LaunchLater(ProjectileEffect prefab, int attackerId, Vector3 from, Vector3 to, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            if (!_units.TryGetView(attackerId, out UnitView attacker))
                yield break;

            ProjectileEffect effect = _casters.Get(attackerId, GetCasterName(attacker)).GetPool(prefab, poolSize).Get(from);
            effect.Place(from, to);
        }

        private void Play(HitEffect prefab, int attackerId, Vector3 hitPoint)
        {
            if (prefab == null || _casters == null || !_units.TryGetView(attackerId, out UnitView attacker))
                return;

            Vector3 from = attacker.transform.position;
            if (prefab.AttachToAttacker)
            {
                _casters.Get(attackerId, GetCasterName(attacker)).GetPool(prefab, poolSize).Get(from).Attach(attacker.transform, hitPoint - from);
                return;
            }

            HitEffect effect = _casters.Get(attackerId, GetCasterName(attacker)).GetPool(prefab, poolSize).Get(hitPoint);
            effect.Place(hitPoint, hitPoint - from);
        }

        private static string GetCasterName(UnitView attacker) =>
            $"{attacker.Unit.Stats.Base.Id}#{attacker.Unit.Id} ({attacker.Unit.Team})";
    }
}
