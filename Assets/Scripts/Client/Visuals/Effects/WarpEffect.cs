using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 시전자가 유닛 하나를 순간이동시키는 연출의 기반(실 관문 등). 시전자 자리, 출발 칸, 도착 칸에서 한 번 재생되고 끝나면 스스로 풀로 돌아간다.
    /// 옮겨지는 유닛은 SinkDelay 뒤 SinkTime 동안 출발 칸으로 가라앉고, RiseTime 동안 도착 칸에서 솟아오른다(UnitView.BeginPortal).
    /// </summary>
    public abstract class WarpEffect : EffectBase
    {
        [Tooltip("유닛이 가라앉기 시작하는 시간(초). 관문이 다 열리는 시점과 맞춘다.")]
        [SerializeField, Min(0f)] private float sinkDelay = 0.45f;
        [SerializeField, Min(0.01f)] private float sinkTime = 0.2f;
        [SerializeField, Min(0.01f)] private float riseTime = 0.2f;

        protected override bool ReleaseWhenFinished => true;

        public float SinkDelay => sinkDelay;
        public float SinkTime => sinkTime;
        public float RiseTime => riseTime;

        /// <param name="caster">시전자 칸 윗면 중심(월드).</param>
        /// <param name="from">옮겨지는 유닛의 출발 칸 윗면 중심(월드).</param>
        /// <param name="to">도착 칸 윗면 중심(월드).</param>
        public abstract void Place(Vector3 caster, Vector3 from, Vector3 to);
    }
}
