using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>칸 효과가 발동한 순간 그 칸에서 한 번 재생되는 이펙트의 기반(덫이 닫힘 등). 끝나면 스스로 풀로 돌아간다.</summary>
    public abstract class TriggerEffect : EffectBase
    {
        protected override bool ReleaseWhenFinished => true;

        /// <summary>칸 윗면 중심(월드)에 놓는다.</summary>
        public void Place(Vector3 cellTop)
        {
            transform.SetPositionAndRotation(cellTop, Quaternion.identity);
            OnPlaced();
        }

        /// <summary>Place 직후 호출된다(Spawn 이후). 위치가 정해진 뒤 시작할 연출을 여기서 시작한다.</summary>
        protected virtual void OnPlaced() { }
    }
}
