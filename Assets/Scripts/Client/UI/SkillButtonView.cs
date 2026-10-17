using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 하단 패널의 스킬 버튼 하나(프리팹 템플릿). 모양은 프리팹에서 자유롭게 바꾸고, 아래 참조만 유지하면 된다.
    /// 아이콘은 스킬 아이콘이 정해지기 전까지 비어 있을 수 있다(비면 숨김).
    /// </summary>
    public class SkillButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Tooltip("사용 가능/불가/지정 중 색을 칠할 배경.")]
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;

        public Button Button => button;

        public void Set(string skillName, string cost, Sprite iconSprite, Color backgroundColor)
        {
            nameText.text = skillName;
            costText.text = cost;
            background.color = backgroundColor;

            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.gameObject.SetActive(iconSprite != null);
            }
        }
    }
}
