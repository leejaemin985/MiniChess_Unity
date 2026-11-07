using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 하단 패널의 스킬 버튼 하나(프리팹 템플릿): 짧은 스킬 이름과 비용(◆ 3). 상세 설명은 마우스를 올리면 말풍선으로 본다.
    /// 모양은 프리팹에서 자유롭게 바꾸고, 아래 참조만 유지하면 된다.
    /// 아이콘은 스킬 아이콘이 정해지기 전까지 비어 있을 수 있다(비면 숨김).
    /// </summary>
    public class SkillButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Tooltip("사용 가능/불가/지정 중 색을 칠할 배경.")]
        [SerializeField] private Image background;
        [Tooltip("지정 중이면 강조색으로 칠할 테두리. 비워도 된다.")]
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;

        public Button Button => button;

        public void Set(string skillName, string cost, Sprite iconSprite, Color backgroundColor, Color frameColor)
        {
            nameText.text = skillName;
            costText.text = cost;
            costText.gameObject.SetActive(!string.IsNullOrEmpty(cost)); // 평타 버튼은 비용 글자 없이 아이콘 + 이름
            background.color = backgroundColor;
            if (frame != null)
                frame.color = frameColor;

            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.gameObject.SetActive(iconSprite != null);
            }
        }
    }
}
