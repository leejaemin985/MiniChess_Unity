using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 휘두르는 사슬/채찍 궤적의 점 하나(ChainWeapon 의 whipPath 아래). 하이어라키 순서대로 두고, 사슬이 이 점을 향할 공격 프레임을 적는다.
    /// 점 사이는 프레임에 맞춰 선형(Lerp)으로 잇는다. 몸체 루트 아래에 두면 캐릭터가 돌거나 움직여도 궤적이 따라간다.
    /// </summary>
    public sealed class WhipPoint : MonoBehaviour
    {
        [Tooltip("사슬이 이 점을 향하는 공격 프레임(30fps).")]
        [SerializeField] private float frame;

        public float Frame => frame;
    }
}
