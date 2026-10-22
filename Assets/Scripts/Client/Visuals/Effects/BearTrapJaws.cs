using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 곰덫 모델의 양 턱. 턱은 받침의 지름(로컬 X 축)에 경첩으로 달려 있고, 열리면 바닥에 펼쳐져 원을 이루고 닫히면 세워져 맞물린다.
    /// jawA 는 +Z 쪽, jawB 는 -Z 쪽 반원이며 각 피벗이 경첩 축(X) 위에 있어야 한다.
    /// </summary>
    public class BearTrapJaws : MonoBehaviour
    {
        [SerializeField] private Transform jawA;
        [SerializeField] private Transform jawB;
        [Tooltip("열린 상태에서 턱이 바닥에서 들린 각도(도).")]
        [SerializeField, Range(0f, 45f)] private float openAngle = 8f;
        [Tooltip("닫힌 상태의 턱 각도(도). 90 이면 똑바로 서서 맞물리고, 작을수록 V 자로 벌어져 위에 선 유닛 몸통 밖을 문다.")]
        [SerializeField, Range(20f, 100f)] private float closedAngle = 90f;

        /// <summary>0 = 열림(펼침), 1 = 닫힘(맞물림). 1 을 조금 넘기면 맞부딪혀 튕기는 모습이 된다.</summary>
        public void SetClosedness(float closedness)
        {
            float angle = Mathf.LerpUnclamped(openAngle, closedAngle, closedness);
            jawA.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            jawB.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
