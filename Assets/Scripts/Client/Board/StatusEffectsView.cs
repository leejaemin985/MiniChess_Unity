using MiniChess.Client.Units;
using MiniChess.Client.Visuals;
using MiniChess.Client.Visuals.Effects;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 상태 이펙트를 재생한다. 상태별 이펙트는 StatusVisual 에서 찾고, 상태를 건 유닛(EffectCaster)의 풀에서 꺼낸다.
    ///   - 걸림/풀림(PlayApplied/PlayRemoved): 걸린 유닛 자리에서 한 번 재생한다(그 유닛의 풀).
    ///   - 반응(PlayReaction): 상태가 걸린 유닛 → 상태를 건 유닛으로 재생한다(호위: 보호 대상 → 호위자).
    /// </summary>
    public class StatusEffectsView : MonoBehaviour
    {
        [Tooltip("상태별 이펙트. 비우면 Resources/StatusVisualCatalog 를 불러온다.")]
        [SerializeField] private StatusVisualCatalog statusVisuals;
        [Tooltip("유닛 한 명이 상태 이펙트별로 미리 만들어 두는 수. 모자라면 늘어난다.")]
        [SerializeField, Min(1)] private int poolSize = 1;

        private EffectCasterRegistry _casters;
        private UnitsView _units;

        public void Initialize(EffectCasterRegistry casters, UnitsView units)
        {
            _casters = casters;
            _units = units;
            if (statusVisuals == null)
                statusVisuals = Resources.Load<StatusVisualCatalog>(StatusVisualCatalog.ResourcePath);
        }

        /// <summary>상태가 새로 걸린 순간의 이펙트를 걸린 유닛 자리에서 재생한다(봉인 → 마법석 문양과 돌가루). 이펙트가 없거나 유닛이 보드에 없으면 재생하지 않는다.</summary>
        public void PlayApplied(string statusId, int targetId)
        {
            PlayOnUnit(statusVisuals != null ? statusVisuals.Find(statusId)?.ApplyEffect : null, targetId);
        }

        /// <summary>상태가 풀린 순간의 이펙트를 걸렸던 유닛 자리에서 재생한다(봉인 → 마법석 파편).</summary>
        public void PlayRemoved(string statusId, int targetId)
        {
            PlayOnUnit(statusVisuals != null ? statusVisuals.Find(statusId)?.RemoveEffect : null, targetId);
        }

        private void PlayOnUnit(CastEffect prefab, int targetId)
        {
            if (prefab == null || _casters == null || _units == null)
                return;
            if (!_units.TryGetView(targetId, out UnitView target) || !target.Unit.IsPlaced)
                return;

            string targetName = $"{target.Unit.Stats.Base.Id}#{target.Unit.Id} ({target.Unit.Team})";
            _casters.Get(targetId, targetName).GetPool(prefab, poolSize).Get().Place(target.transform.position);
        }

        /// <summary>상태 반응 이펙트를 걸린 유닛 → 건 유닛으로 재생한다. 이펙트가 없거나 두 유닛 중 하나라도 보드에 없으면 재생하지 않는다.</summary>
        public void PlayReaction(string statusId, int targetId, int sourceId)
        {
            LineEffect prefab = statusVisuals != null ? statusVisuals.Find(statusId)?.ReactionEffect : null;
            if (prefab == null || _casters == null || _units == null)
                return;
            if (!_units.TryGetView(targetId, out UnitView target) || !_units.TryGetView(sourceId, out UnitView source))
                return;

            string sourceName = $"{source.Unit.Stats.Base.Id}#{source.Unit.Id} ({source.Unit.Team})";
            LineEffect effect = _casters.Get(sourceId, sourceName).GetPool(prefab, poolSize).Get();
            effect.Place(target.transform.position, source.transform.position);
        }
    }
}
