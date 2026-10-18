using System.Collections;
using MiniChess.Client.Common;
using MiniChess.Client.Visuals;
using MiniChess.Core.State;
using MiniChess.Core.Statuses;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 유닛 하나의 표시(캡슐 + 얼굴 + 이름/HP 라벨). 얼굴은 시각 데이터가 있을 때만 캡슐 위에 띄운다. 코어 Unit 을 읽기만 하며, Sync 로 상태를 그대로 반영한다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private const float BodyHeight = 1f;
        private const float BodyWidth = 0.6f;
        /// <summary>소환물(분신)은 본체보다 작게 그린다.</summary>
        private const float SummonScale = 0.75f;
        /// <summary>얼굴 한 변 길이와 테두리 두께(월드 단위).</summary>
        private const float FaceSize = 0.6f;
        private const float FaceFrame = 0.05f;
        /// <summary>캡슐 꼭대기와 얼굴 사이, 얼굴과 라벨 사이 간격.</summary>
        private const float FaceGap = 0.08f;
        /// <summary>행동 아이콘 한 변 길이(월드 단위)와 등장/사라짐 시간.</summary>
        private const float IconSize = 0.55f;
        private const float IconPopTime = 0.12f;
        private const float IconFadeTime = 0.15f;

        private Renderer _body;
        private MaterialPropertyBlock _block;
        private TextMeshPro _label;
        private FaceBadge _face;
        private Transform _iconRoot;
        private SpriteRenderer _icon;
        private Coroutine _iconRoutine;
        /// <summary>행동 아이콘을 띄울 높이(얼굴 중심, 얼굴이 없으면 캡슐 위).</summary>
        private float _iconHeight;
        private Color _teamColor;
        private Color _actedColor;

        public Unit Unit { get; private set; }

        /// <param name="face">얼굴 스프라이트. null 이면 얼굴 없이 캡슐과 라벨만 그린다.</param>
        /// <param name="faceMask">얼굴 마스크 모양. null 이면 원.</param>
        /// <param name="frameShape">얼굴 테두리 모양. null 이면 원.</param>
        public static UnitView Create(
            Transform parent, Unit unit, Color teamColor, Color actedColor, Sprite face, Sprite faceMask = null, Sprite frameShape = null)
        {
            var root = new GameObject($"Unit {unit.Id} {unit.Team} {unit.Stats.Base.Id}");
            root.transform.SetParent(parent, false);

            var view = root.AddComponent<UnitView>();
            view.Unit = unit;
            view._teamColor = teamColor;
            view._actedColor = actedColor;
            view._block = new MaterialPropertyBlock();
            float scale = unit.IsSummon ? SummonScale : 1f;
            view._body = CreateBody(root.transform, scale);
            float labelHeight = BodyHeight * scale + FaceGap;
            view._iconHeight = labelHeight + IconSize * 0.5f;

            if (face != null)
            {
                view._face = FaceBadge.Create(root.transform, face, teamColor, FaceSize * scale, FaceFrame, faceMask, frameShape);
                view._face.transform.localPosition = Vector3.up * (labelHeight + view._face.OuterSize * 0.5f);
                view._iconHeight = labelHeight + view._face.OuterSize * 0.5f;

                // 소환물(분신)은 소환자 얼굴을 옅게 쓴다.
                if (unit.IsSummon)
                    view._face.SetImageColor(new Color(1f, 1f, 1f, 0.6f));

                labelHeight += view._face.OuterSize + FaceGap;
            }

            view._label = CreateLabel(root.transform, labelHeight);

            return view;
        }

        /// <summary>코어 상태를 그대로 반영한다. 보드에서 빠진 유닛(사망 등)은 숨긴다.</summary>
        public void Sync(BoardCoordinates coordinates)
        {
            if (!Unit.IsPlaced)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            transform.localPosition = coordinates.ToWorld(Unit.Position.Value);

            UnitTurnState turn = Unit.TurnState;
            bool acted = turn.CombatActionUsed || turn.ActionsEnded;
            SetBodyColor(acted ? _actedColor : _teamColor);
            if (_face != null)
                _face.SetDimmed(acted);

            _label.text = $"<size=70%>{Unit.Stats.Base.Name}</size>\n{Unit.Stats.CurrentHp}/{Unit.Stats.MaxHp}{FormatShield()}{FormatStatuses()}";
        }

        /// <summary>
        /// 행동 아이콘(기본 공격 등)을 얼굴 위에 duration 초 동안 띄운다. 커지며 나타나고 끝에 흐려진다.
        /// 이미 떠 있으면 처음부터 다시 보여준다.
        /// </summary>
        public void ShowActionIcon(Sprite icon, float duration)
        {
            if (icon == null || !gameObject.activeInHierarchy)
                return;

            if (_icon == null)
                CreateIcon();

            _icon.sprite = icon;
            float width = icon.bounds.size.x;
            _icon.transform.localScale = Vector3.one * (width > 0f ? IconSize / width : IconSize);

            if (_iconRoutine != null)
                StopCoroutine(_iconRoutine);
            _iconRoutine = StartCoroutine(IconRoutine(duration));
        }

        private IEnumerator IconRoutine(float duration)
        {
            _iconRoot.gameObject.SetActive(true);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float pop = Mathf.Clamp01(t / IconPopTime);
                _iconRoot.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, 1f - (1f - pop) * (1f - pop));

                float remaining = duration - t;
                _icon.color = new Color(1f, 1f, 1f, remaining < IconFadeTime ? remaining / IconFadeTime : 1f);
                yield return null;
            }

            _iconRoot.gameObject.SetActive(false);
            _iconRoutine = null;
        }

        private void CreateIcon()
        {
            var root = new GameObject("ActionIcon");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.up * _iconHeight;
            root.AddComponent<Billboard>();
            _iconRoot = root.transform;

            var image = new GameObject("Icon");
            image.transform.SetParent(_iconRoot, false);
            // 얼굴보다 카메라 쪽으로 살짝 앞에, 정렬 순서도 위로 둔다.
            image.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            _icon = image.AddComponent<SpriteRenderer>();
            _icon.sortingOrder = 10;

            root.SetActive(false);
        }

        /// <summary>보호막이 있으면 HP 옆에 " +3" 형태(하늘색)로 붙인다.</summary>
        private string FormatShield()
        {
            int shield = Unit.Stats.Shield;
            return shield > 0 ? $" <color=#7FD4FF>+{shield}</color>" : string.Empty;
        }

        /// <summary>걸린 상태를 "ROOT 1 · BURN 2"(남은 횟수) 형태의 작은 줄로 만든다. 영구 상태는 이름만. 없으면 빈 문자열.</summary>
        private string FormatStatuses()
        {
            if (Unit.Statuses.Count == 0)
                return string.Empty;

            var parts = new string[Unit.Statuses.Count];
            for (int i = 0; i < parts.Length; i++)
            {
                StatusEffect status = Unit.Statuses[i];
                parts[i] = status.Definition.IsPermanent
                    ? status.Definition.Id
                    : $"{status.Definition.Id} {status.Remaining}";
            }

            return $"\n<size=60%><color=#FFD24D>{string.Join(" · ", parts)}</color></size>";
        }

        private void SetBodyColor(Color color)
        {
            _body.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _body.SetPropertyBlock(_block);
        }

        private static Renderer CreateBody(Transform parent, float scale)
        {
            // 기본 캡슐은 높이 2, 지름 1.
            GameObject body = RuntimePrimitive.Create(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = Vector3.up * (BodyHeight * 0.5f * scale);
            body.transform.localScale = new Vector3(BodyWidth, BodyHeight * 0.5f, BodyWidth) * scale;

            return body.GetComponent<Renderer>();
        }

        private static TextMeshPro CreateLabel(Transform parent, float height)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            go.AddComponent<Billboard>();

            var label = go.AddComponent<TextMeshPro>();
            KoreanFont.Apply(label);
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.enableWordWrapping = false;
            label.color = Color.white;
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(2.5f, 1.5f);

            return label;
        }
    }
}
