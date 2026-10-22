using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>시전자 자리에서 한 번 재생되는 이펙트의 기반(박격포 발사 등). 끝나면 스스로 풀로 돌아간다.</summary>
    public abstract class CastEffect : EffectBase
    {
        protected override bool ReleaseWhenFinished => true;

        /// <param name="position">시전자 칸 윗면 중심(월드).</param>
        public void Place(Vector3 position)
        {
            transform.SetPositionAndRotation(position, Quaternion.identity);
        }
    }
}
