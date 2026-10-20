using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Client.Units;
using MiniChess.Client.Visuals.Effects;
using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 스킬 범위의 양 끝을 잇는 이펙트(일자 베기 등)를 재생한다. 이펙트는 시전자(EffectCaster)의 풀에서 꺼낸다.
    /// 범위 칸 순서(모양 정의 순)의 첫 칸에서 마지막 칸으로 쓸고 지나가며, 양 끝 칸의 바깥 가장자리까지 늘린다.
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

        private string GetCasterName(int casterId)
        {
            return _units != null && _units.TryGetView(casterId, out UnitView view)
                ? $"{view.Unit.Stats.Base.Id}#{view.Unit.Id} ({view.Unit.Team})"
                : $"Unit#{casterId}";
        }
    }
}
