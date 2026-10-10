using TMPro;
using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>
    /// 한글 표시용 TMP 폰트. 기본 TMP 폰트(LiberationSans)에 한글 글리프가 없어,
    /// Resources 의 나눔고딕(OFL)으로 실행 중에 동적 폰트 에셋을 만들어 쓴다(에디터에서 폰트 에셋을 굽지 않아도 됨).
    /// 폰트를 불러오지 못하면 기본 폰트를 그대로 둔다.
    /// </summary>
    public static class KoreanFont
    {
        private const string ResourcePath = "Fonts/NanumGothic-Regular";

        private static TMP_FontAsset _asset;
        private static bool _loaded;

        /// <summary>한글 폰트 에셋. 불러오지 못했으면 null.</summary>
        public static TMP_FontAsset Asset
        {
            get
            {
                if (!_loaded)
                    Load();

                return _asset;
            }
        }

        /// <summary>텍스트에 한글 폰트를 적용한다. 폰트가 없으면 아무것도 하지 않는다.</summary>
        public static void Apply(TMP_Text text)
        {
            TMP_FontAsset asset = Asset;
            if (asset != null)
                text.font = asset;
        }

        private static void Load()
        {
            _loaded = true;

            var font = Resources.Load<Font>(ResourcePath);
            if (font == null)
            {
                Debug.LogWarning($"[KoreanFont] Resources/{ResourcePath} 를 찾을 수 없어 기본 폰트를 사용합니다.");
                return;
            }

            _asset = TMP_FontAsset.CreateFontAsset(font);
            if (_asset == null)
            {
                Debug.LogWarning("[KoreanFont] 동적 폰트 에셋을 만들지 못해 기본 폰트를 사용합니다.");
                return;
            }

            _asset.name = $"{font.name} (Runtime SDF)";
        }
    }
}
