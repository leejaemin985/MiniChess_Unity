using MiniChess.Client.Common;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 유닛 머리 위의 작은 HP 바(월드 공간, 항상 카메라를 바라봄). 어두운 바탕 위에 HP 비율만큼 초록, 보호막이 있으면 그 양만큼 하늘색을 왼쪽부터 덮는다(비율 기준은 최대 HP).
    /// 정확한 숫자와 이름은 하단 유닛 카드에서 본다.
    /// </summary>
    public class UnitHpBar : MonoBehaviour
    {
        private static readonly Color BackColor = new Color(0.06f, 0.06f, 0.08f, 0.85f);
        private static readonly Color HpColor = new Color(0.4f, 0.8f, 0.5f);
        private static readonly Color ShieldColor = new Color(0.5f, 0.83f, 1f);
        private static Sprite _white;

        private SpriteRenderer _back;
        private SpriteRenderer _hp;
        private SpriteRenderer _shield;
        private float _width;
        private float _height;

        /// <summary>바 높이(월드 단위). 위아래 배치 계산용.</summary>
        public float Height => _height;

        /// <param name="width">바 폭(월드 단위). 테두리 포함.</param>
        /// <param name="sortingOrder">얼굴, 상태 배지와 겹칠 때의 정렬 기준.</param>
        public static UnitHpBar Create(Transform parent, float width, float height, int sortingOrder = 15)
        {
            var root = new GameObject("HpBar");
            root.transform.SetParent(parent, false);
            root.AddComponent<Billboard>();

            var bar = root.AddComponent<UnitHpBar>();
            bar._width = width;
            bar._height = height;
            bar._back = bar.CreatePart("Back", BackColor, sortingOrder);
            bar._hp = bar.CreatePart("Hp", HpColor, sortingOrder + 1);
            bar._shield = bar.CreatePart("Shield", ShieldColor, sortingOrder + 2);
            bar._back.transform.localScale = new Vector3(width, height, 1f);
            return bar;
        }

        public void Set(int hp, int maxHp, int shield)
        {
            // 테두리 두께만큼 안쪽에 채운다.
            float border = _height * 0.2f;
            float inner = _width - border * 2f;
            SetFill(_hp, inner, max: maxHp, value: hp);
            SetFill(_shield, inner, max: maxHp, value: shield);
        }

        private void SetFill(SpriteRenderer fill, float inner, int max, int value)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
            fill.enabled = ratio > 0f;
            float width = inner * ratio;
            float height = _height * 0.6f;
            fill.transform.localScale = new Vector3(width, height, 1f);
            // 왼쪽 끝을 맞춘다. 얼굴/배지보다 카메라 쪽으로 살짝 앞에.
            fill.transform.localPosition = new Vector3(-inner * 0.5f + width * 0.5f, 0f, -0.001f);
        }

        private SpriteRenderer CreatePart(string name, Color color, int order)
        {
            var part = new GameObject(name);
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = White;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>1 x 1 월드 단위 흰 사각형(스케일로 크기를 정한다).</summary>
        private static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    Texture2D texture = Texture2D.whiteTexture;
                    _white = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
                    _white.name = "UnitHpBarWhite";
                }

                return _white;
            }
        }
    }
}
