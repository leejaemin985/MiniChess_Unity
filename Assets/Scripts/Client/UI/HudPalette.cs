using UnityEngine;

namespace MiniChess.Client.UI
{
    /// <summary>전투 HUD 공용 색(시안 기준). AP 는 글자 대신 라벤더 마름모 ◆ 로 표기한다.</summary>
    public static class HudPalette
    {
        /// <summary>AP 마름모와 채워진 AP 칸(라벤더 #B99CFF).</summary>
        public static readonly Color ApFilled = new Color32(0xB9, 0x9C, 0xFF, 0xFF);

        /// <summary>빈 AP 칸(짙은 회보라 #353344).</summary>
        public static readonly Color ApEmpty = new Color32(0x35, 0x33, 0x44, 0xFF);

        /// <summary>쓸 AP 미리보기(호박색 #F2B65F, 사선).</summary>
        public static readonly Color ApPreview = new Color32(0xF2, 0xB6, 0x5F, 0xFF);

        /// <summary>HUD 바/버튼 바탕과 테두리.</summary>
        public static readonly Color Panel = new Color32(0x1C, 0x1B, 0x22, 0xF2);
        public static readonly Color ButtonFill = new Color32(0x24, 0x23, 0x2B, 0xF2);
        public static readonly Color ButtonBorder = new Color32(0x8A, 0x88, 0x92, 0xFF);

        /// <summary>"◆ 3" 처럼 라벤더 마름모를 붙인 AP 표기(TMP 리치 텍스트).</summary>
        public static string Ap(int amount) => $"{Diamond} {amount}";

        public static string Diamond => $"<color=#{ColorUtility.ToHtmlStringRGB(ApFilled)}>◆</color>";
    }
}
