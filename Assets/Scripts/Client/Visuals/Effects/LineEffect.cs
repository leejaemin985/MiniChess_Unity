using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>두 점을 잇는 이펙트의 기반(일자 베기 등). 한 번 재생되고, 끝나면 스스로 풀로 돌아간다.</summary>
    public abstract class LineEffect : EffectBase
    {
        protected override bool ReleaseWhenFinished => true;

        /// <summary>월드 좌표 from 에서 to 로 재생한다(칸 윗면 높이 기준).</summary>
        public abstract void Place(Vector3 from, Vector3 to);
    }
}
