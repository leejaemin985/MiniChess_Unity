using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// UI 요소에 마우스가 올라오고 나가는 것을 알린다(설명 말풍선용). 누를 수 없는 버튼에서도 동작한다.
    /// 레이캐스트를 받을 그래픽이 없으면 투명 이미지를 붙인다.
    /// </summary>
    public class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action Entered;
        public event Action Exited;

        /// <summary>target 에 붙이고(이미 있으면 그것을) 돌려준다.</summary>
        public static HoverRelay Attach(GameObject target)
        {
            if (target.TryGetComponent(out HoverRelay existing))
                return existing;

            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                var image = target.AddComponent<Image>();
                image.color = Color.clear;
                graphic = image;
            }

            graphic.raycastTarget = true;
            return target.AddComponent<HoverRelay>();
        }

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

        private void OnDisable() => Exited?.Invoke();
    }
}
