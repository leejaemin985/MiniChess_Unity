using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 범위에 떨어지는 이펙트의 기반(박격포 착탄 등). 범위 중심에 놓이고, 범위 각 칸 위치도 받는다. 끝나면 스스로 풀로 돌아간다.
    /// </summary>
    public abstract class StrikeEffect : EffectBase
    {
        protected override bool ReleaseWhenFinished => true;

        /// <param name="center">범위 칸들의 중심(칸 윗면 높이, 월드).</param>
        /// <param name="cells">범위 각 칸 윗면 중심(월드).</param>
        public void Place(Vector3 center, IReadOnlyList<Vector3> cells)
        {
            transform.SetPositionAndRotation(center, Quaternion.identity);
            OnPlaced(cells);
        }

        /// <summary>Place 직후 호출된다(Spawn 이후). 칸별 연출을 준비한다.</summary>
        protected virtual void OnPlaced(IReadOnlyList<Vector3> cells) { }
    }
}
