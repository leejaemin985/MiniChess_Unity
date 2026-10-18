using System;
using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Client.UI;
using MiniChess.Client.Visuals;
using MiniChess.Core.Statuses;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>유닛 머리 위 상태 배지의 모양 설정. UnitsView 인스펙터에서 조정하며, 플레이 중에 바꾸면 바로 반영된다.</summary>
    [Serializable]
    public class StatusBadgeStyle
    {
        [Tooltip("배지 한 변(월드 단위). 1 픽셀 = 0.01 이므로 0.3 은 UI 의 30 픽셀 칸과 같은 비율이다.")]
        [Min(0.05f)] public float size = 0.3f;

        [Tooltip("배지 사이 간격(월드 단위).")]
        [Min(0f)] public float gap = 0.05f;

        [Tooltip("모서리 반지름(픽셀). RoundedFrame 의 Radius 와 같은 단위.")]
        [Min(0f)] public float cornerRadius = 6f;

        [Tooltip("테두리 두께(픽셀). RoundedFrame 의 Thickness 와 같은 단위.")]
        [Min(0f)] public float frameThickness = 1.5f;

        [Tooltip("배지 대비 아이콘 크기.")]
        [Range(0.1f, 1f)] public float iconRatio = 0.72f;

        [Tooltip("테두리 안쪽 바탕 색.")]
        public Color backingColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);

        [Tooltip("남은 횟수 글자 크기.")]
        [Min(0.1f)] public float countFontSize = 1.3f;
    }

    /// <summary>
    /// 유닛 머리 위의 상태 아이콘 줄(월드 공간, 항상 카메라를 바라봄). 상태마다 배지 하나:
    /// 어두운 바탕 → 상태 색 테두리 → 상태 색으로 칠한 아이콘, 오른쪽 아래에 남은 횟수(영구 상태는 없음).
    /// 아이콘이 없는 상태는 아이콘 대신 이름 앞 두 글자를 쓴다. 상태 이름/색/아이콘은 StatusVisualCatalog 에서 찾는다.
    /// 테두리/바탕은 UI 와 같은 둥근 사각형(RoundedFrame.GetShape)을 9-slice 로 그린다. 모양 값은 StatusBadgeStyle.
    /// </summary>
    public class StatusBadgeRow : MonoBehaviour
    {
        private static readonly Color FallbackColor = new Color(1f, 0.82f, 0.3f);

        private readonly List<Badge> _badges = new List<Badge>();
        private StatusVisualCatalog _visuals;
        private StatusBadgeStyle _style;
        private int _sortingBase;

        private sealed class Badge
        {
            public GameObject Root;
            public SpriteRenderer Backing;
            public SpriteRenderer Frame;
            public SpriteRenderer Icon;
            public TextMeshPro Count;
            public TextMeshPro Name;
        }

        /// <param name="visuals">상태 표시 데이터. null 이면 모든 상태를 기본 색과 Id 글자로 그린다.</param>
        /// <param name="style">모양 설정(공유 객체라 인스펙터에서 바꾸면 다음 Sync 에 반영된다).</param>
        /// <param name="sortingBase">다른 표시(얼굴, 행동 아이콘)와 겹칠 때의 정렬 기준.</param>
        public static StatusBadgeRow Create(Transform parent, StatusVisualCatalog visuals, StatusBadgeStyle style, int sortingBase = 20)
        {
            var root = new GameObject("Statuses");
            root.transform.SetParent(parent, false);
            root.AddComponent<Billboard>();

            var row = root.AddComponent<StatusBadgeRow>();
            row._visuals = visuals;
            row._style = style ?? new StatusBadgeStyle();
            row._sortingBase = sortingBase;
            return row;
        }

        /// <summary>배지 한 변(월드 단위). 위아래 배치 계산용.</summary>
        public float BadgeSize => _style.size;

        /// <summary>걸린 상태(부여 순)대로 배지를 맞춘다. 상태가 없으면 줄 전체를 숨긴다.</summary>
        public void Sync(IReadOnlyList<StatusEffect> statuses)
        {
            gameObject.SetActive(statuses.Count > 0);

            while (_badges.Count < statuses.Count)
                _badges.Add(CreateBadge(_badges.Count));

            float size = _style.size;
            float width = statuses.Count * size + (statuses.Count - 1) * _style.gap;
            for (int i = 0; i < _badges.Count; i++)
            {
                Badge badge = _badges[i];
                bool used = i < statuses.Count;
                badge.Root.SetActive(used);
                if (!used)
                    continue;

                badge.Root.transform.localPosition = new Vector3(-width * 0.5f + size * 0.5f + i * (size + _style.gap), 0f, 0f);
                ApplyStyle(badge);
                Apply(badge, statuses[i]);
            }
        }

        /// <summary>모양 설정(크기, 둥글기, 두께, 바탕 색, 글자 크기)을 배지에 반영한다.</summary>
        private void ApplyStyle(Badge badge)
        {
            float size = _style.size;
            SetRounded(badge.Backing, 0f, size);
            SetRounded(badge.Frame, _style.frameThickness, size);
            badge.Backing.color = _style.backingColor;

            badge.Count.fontSize = _style.countFontSize;
            badge.Count.rectTransform.sizeDelta = new Vector2(size, size);
            badge.Count.transform.localPosition = new Vector3(size * 0.62f, -size * 0.62f, -0.01f);
            badge.Name.rectTransform.sizeDelta = new Vector2(size, size);
        }

        private void Apply(Badge badge, StatusEffect status)
        {
            StatusVisual visual = _visuals != null ? _visuals.Find(status.Definition.Id) : null;
            Color color = visual != null ? visual.Color : FallbackColor;
            Sprite icon = visual != null ? visual.Icon : null;

            badge.Frame.color = color;

            badge.Icon.gameObject.SetActive(icon != null);
            if (icon != null)
            {
                badge.Icon.sprite = icon;
                badge.Icon.color = color;
                badge.Icon.transform.localScale = Vector3.one * ScaleFor(icon, _style.size * _style.iconRatio);
            }

            badge.Name.gameObject.SetActive(icon == null);
            if (icon == null)
            {
                string name = visual != null ? visual.DisplayName : status.Definition.Id;
                badge.Name.text = name.Length > 2 ? name.Substring(0, 2) : name;
                badge.Name.color = color;
            }

            badge.Count.text = status.Definition.IsPermanent ? string.Empty : status.Remaining.ToString();
        }

        private Badge CreateBadge(int index)
        {
            var root = new GameObject($"Badge {index}");
            root.transform.SetParent(transform, false);

            // Billboard 는 카메라 회전을 따르므로 +z 가 카메라에서 먼 쪽이다. 뒤에 놓일 것일수록 z 를 크게.
            // 어두운 둥근 바탕 위에 상태 색 둥근 테두리(가운데 빈 것)를 겹친다.
            var badge = new Badge
            {
                Root = root,
                Backing = CreateSprite(root.transform, "Backing", 0.02f, _sortingBase),
                Frame = CreateSprite(root.transform, "Frame", 0.01f, _sortingBase + 1),
                Icon = CreateSprite(root.transform, "Icon", 0f, _sortingBase + 2),
                Name = CreateText(root.transform, "Name", 1.1f, TextAlignmentOptions.Center, _sortingBase + 2),
                Count = CreateText(root.transform, "Count", _style.countFontSize, TextAlignmentOptions.BottomRight, _sortingBase + 3),
            };
            badge.Count.color = Color.white;
            return badge;
        }

        /// <summary>둥근 사각형(9-slice 로 늘려 모서리 반지름/두께 유지). thickness 0 이면 채움.</summary>
        private void SetRounded(SpriteRenderer renderer, float thickness, float size)
        {
            renderer.sprite = RoundedFrame.GetShape(_style.cornerRadius, thickness);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(size, size);
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, float depth, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, depth);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }

        private static TextMeshPro CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshPro>();
            KoreanFont.Apply(text);
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.outlineWidth = 0.3f;
            text.outlineColor = Color.black;
            text.rectTransform.pivot = alignment == TextAlignmentOptions.BottomRight ? new Vector2(1f, 0f) : new Vector2(0.5f, 0.5f);
            text.sortingOrder = order;
            return text;
        }

        /// <summary>스프라이트 가로가 size(월드 단위)가 되는 배율.</summary>
        private static float ScaleFor(Sprite sprite, float size)
        {
            float width = sprite != null ? sprite.bounds.size.x : 0f;
            return width > 0f ? size / width : size;
        }
    }
}
