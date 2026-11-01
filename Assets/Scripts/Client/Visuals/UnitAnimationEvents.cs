using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 클립의 타이밍 이벤트(Hit, Release)를 받는 곳. Animator 와 같은 오브젝트에 둔다(UnitBody 가 자동으로 붙인다).
    /// 연출 타이밍은 코드가 클립에서 이벤트 시간을 미리 읽어 정하므로 여기서는 아무것도 하지 않는다.
    /// 받는 메서드가 없으면 Unity 가 이벤트마다 "has no receiver" 에러를 내므로 둔다.
    /// </summary>
    public class UnitAnimationEvents : MonoBehaviour
    {
        public void Hit()
        {
        }

        public void Release()
        {
        }
    }
}
