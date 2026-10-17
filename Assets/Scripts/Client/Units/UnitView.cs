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

        private Renderer _body;
        private MaterialPropertyBlock _block;
        private TextMeshPro _label;
        private FaceBadge _face;
        private Color _teamColor;
        private Color _actedColor;

        public Unit Unit { get; private set; }

        /// <param name="face">얼굴 스프라이트. null 이면 얼굴 없이 캡슐과 라벨만 그린다.</param>
        public static UnitView Create(Transform parent, Unit unit, Color teamColor, Color actedColor, Sprite face)
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

            if (face != null)
            {
                view._face = FaceBadge.Create(root.transform, face, teamColor, FaceSize * scale, FaceFrame);
                view._face.transform.localPosition = Vector3.up * (labelHeight + view._face.OuterSize * 0.5f);

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
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
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
