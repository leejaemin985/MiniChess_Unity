using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>장판 이펙트의 기반. 칸 하나 크기로 만들고, 칸 윗면 중심에 놓는다.</summary>
    public abstract class FieldEffect : EffectBase
    {
        /// <summary>칸 윗면 중심(월드)에 놓는다.</summary>
        public void Place(Vector3 cellTop)
        {
            transform.SetPositionAndRotation(cellTop, Quaternion.identity);
        }
    }
}
