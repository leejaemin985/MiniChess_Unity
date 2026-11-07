using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 팀 공용 AP 게이지(하단 패널, 스킬 버튼 위). 1 AP = 1칸, 칸 수는 최대 AP.
    ///   채워진 칸 = 라벤더, 빈 칸 = 짙은 회보라. 위 글자는 "◆ 12 / 14".
    ///   미리보기(쓸 AP): 채워진 칸의 오른쪽 끝부터 비용만큼 호박색 사선으로 바꾸고 오른쪽에 "→ 9"(쓰고 남을 AP).
    ///   남은 AP 보다 비싸면 남은 칸만 사선으로 칠하고 "→ 부족"을 경고색으로 띄운다.
    /// 칸은 템플릿(비활성 자식)을 복제해 만든다. 모양은 프리팹(UnitInfoPanel 의 ApGauge)에서 바꾸고 참조만 유지하면 된다.
    /// </summary>
    public class ApGaugeView : MonoBehaviour
    {
        [Tooltip("\"◆ 12 / 14\".")]
        [SerializeField] private TMP_Text amountText;
        [Tooltip("미리보기 중 \"→ 9\". 미리보기가 없으면 숨긴다.")]
        [SerializeField] private TMP_Text afterText;
        [Tooltip("칸을 가로로 늘어놓는 묶음(HorizontalLayoutGroup).")]
        [SerializeField] private Transform cellContainer;
        [Tooltip("칸 하나(Image). 비활성으로 두면 복제해 쓴다. 자식 Image 하나가 미리보기 사선이다.")]
        [SerializeField] private Image cellTemplate;
        [SerializeField] private Color shortColor = new Color(1f, 0.42f, 0.36f);

        private readonly List<(Image Cell, Image Hatch)> _cells = new List<(Image, Image)>();
        private static Sprite _hatchSprite;

        private void Awake()
        {
            cellTemplate.gameObject.SetActive(false);
        }

        /// <param name="preview">미리보는 행동의 AP 비용. 0 이면 미리보기 없음.</param>
        public void Show(int current, int max, int preview)
        {
            max = Mathf.Max(0, max);
            current = Mathf.Clamp(current, 0, max);
            preview = Mathf.Max(0, preview);
            int hatched = Mathf.Min(preview, current);

            amountText.text = $"{HudPalette.Diamond} {current} / {max}";
            afterText.gameObject.SetActive(preview > 0);
            if (preview > 0)
            {
                bool enough = preview <= current;
                afterText.text = enough ? $"→ {current - preview}" : "→ 부족";
                afterText.color = enough ? HudPalette.ApPreview : shortColor;
            }

            EnsureCells(max);
            for (int i = 0; i < _cells.Count; i++)
            {
                (Image cell, Image hatch) = _cells[i];
                bool used = i < max;
                cell.gameObject.SetActive(used);
                if (!used)
                    continue;

                bool filled = i < current;
                bool spending = filled && i >= current - hatched;
                cell.color = spending ? HudPalette.ApPreview * new Color(1f, 1f, 1f, 0.28f) : filled ? HudPalette.ApFilled : HudPalette.ApEmpty;
                hatch.enabled = spending;
            }
        }

        private void EnsureCells(int count)
        {
            while (_cells.Count < count)
            {
                Image cell = Instantiate(cellTemplate, cellContainer);
                cell.gameObject.SetActive(true);
                Image hatch = cell.transform.childCount > 0 ? cell.transform.GetChild(0).GetComponent<Image>() : null;
                if (hatch == null)
                    hatch = CreateHatch(cell.transform);

                hatch.sprite = HatchSprite;
                hatch.type = Image.Type.Tiled;
                hatch.color = HudPalette.ApPreview;
                hatch.raycastTarget = false;
                _cells.Add((cell, hatch));
            }
        }

        private static Image CreateHatch(Transform cell)
        {
            RectTransform rect = UiFactory.CreateRect(cell, "Hatch");
            UiFactory.Stretch(rect);
            return rect.gameObject.AddComponent<Image>();
        }

        /// <summary>호박색 사선 무늬(오른쪽 위로 기운 줄). 흰 줄에 Image 색을 곱해 쓴다. 한 번 만들어 공유한다.</summary>
        private static Sprite HatchSprite
        {
            get
            {
                if (_hatchSprite != null)
                    return _hatchSprite;

                const int size = 16;
                const int stripe = 5;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "ApHatch",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                        pixels[y * size + x] = (x - y + size) % size < stripe ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _hatchSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _hatchSprite.hideFlags = HideFlags.DontSave;
                return _hatchSprite;
            }
        }
    }
}
