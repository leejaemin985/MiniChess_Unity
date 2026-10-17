using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 하단 패널의 상태 칸 하나(프리팹 템플릿): 위에 아이콘, 아래에 남은 횟수, 상태 색 테두리와 옅은 배경.
    /// 아이콘이 없는 상태는 아이콘 대신 이름을 글자로 보여준다. 테두리/배경은 비워도 된다.
    /// 아이콘은 흰색 그림을 상태 색으로 칠하는 것을 전제로 한다.
    /// </summary>
    public class StatusChipView : MonoBehaviour
    {
        [Tooltip("상태 색으로 칠할 테두리. 비워도 된다.")]
        [SerializeField] private Graphic frame;

        [Tooltip("상태 색을 옅게 칠할 배경. 비워도 된다.")]
        [SerializeField] private Graphic background;

        [SerializeField, Range(0f, 1f)] private float backgroundAlpha = 0.15f;

        [SerializeField] private Image icon;

        [FormerlySerializedAs("text")]
        [SerializeField] private TMP_Text countText;

        /// <param name="iconSprite">상태 아이콘. null 이면 아이콘 대신 label 을 글자로 보여준다.</param>
        /// <param name="label">아이콘이 없을 때 보일 이름.</param>
        /// <param name="count">아래에 보일 숫자(남은 횟수, 보호막 양). 비우면 숨긴다(영구 상태 등).</param>
        public void Set(Sprite iconSprite, string label, string count, Color color)
        {
            bool hasIcon = iconSprite != null && icon != null;
            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.color = color;
                icon.gameObject.SetActive(hasIcon);
            }

            string text = hasIcon ? count : string.IsNullOrEmpty(count) ? label : $"{label} {count}";
            countText.text = text;
            countText.color = color;
            countText.gameObject.SetActive(!string.IsNullOrEmpty(text));

            if (frame != null)
                frame.color = color;
            if (background != null)
                background.color = new Color(color.r, color.g, color.b, backgroundAlpha);
        }
    }
}
