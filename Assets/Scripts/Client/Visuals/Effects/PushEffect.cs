using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 밀기 이펙트의 기반(자리 교정 등). 대상 칸에서 고른 방향으로 한 번 재생되고, 끝나면 스스로 풀로 돌아간다.
    /// 프리팹은 +Z 가 미는 방향이 되게 만든다(Place 가 +Z 를 미는 방향으로 돌린다).
    /// </summary>
    public abstract class PushEffect : EffectBase
    {
        protected override bool ReleaseWhenFinished => true;

        /// <summary>대상이 실제로 밀려난 칸 수(막혀서 안 밀렸으면 0).</summary>
        protected int PushedCells { get; private set; }

        /// <summary>칸 하나의 간격(월드). 도착 칸이 없으면(안 밀림) 1.</summary>
        protected float CellSpacing { get; private set; }

        /// <param name="target">대상 칸 윗면 중심(월드).</param>
        /// <param name="direction">미는 방향(월드, 수평).</param>
        /// <param name="landing">대상이 실제로 도착한 칸 윗면 중심(월드). 안 밀렸으면 target 과 같다.</param>
        /// <param name="pushedCells">실제로 밀려난 칸 수.</param>
        public void Place(Vector3 target, Vector3 direction, Vector3 landing, int pushedCells)
        {
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity;
            transform.SetPositionAndRotation(target, rotation);
            PushedCells = pushedCells;
            CellSpacing = pushedCells > 0 ? Vector3.Distance(target, landing) / pushedCells : 1f;
            OnPlaced();
        }

        /// <summary>Place 직후(위치/방향/칸 수가 정해진 뒤) 연출을 시작한다.</summary>
        protected abstract void OnPlaced();
    }
}
