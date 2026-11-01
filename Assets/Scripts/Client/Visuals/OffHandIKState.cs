using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// Animator 상태에 붙여 그 상태에 들어갈 때 반대손 IK 세기(CharacterHandRig)의 기본값을 정한다(0 = 반대손 자유, 1 = 양손으로 쥠).
    /// 재생 중인 클립에 OffHandIK 커브가 있으면 커브가 우선한다(캐릭터별 클립마다 다르게). 이 값은 커브 없는 클립용이다.
    /// 붙이지 않은 상태는 직전 세기를 그대로 쓰므로, 공용 컨트롤러의 상태마다 붙여 둔다.
    /// </summary>
    public class OffHandIKState : StateMachineBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float weight = 1f;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (animator.TryGetComponent(out CharacterHandRig rig))
                rig.SetOffHandWeight(weight);
        }
    }
}
