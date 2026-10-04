using MiniChess.Client.Common;
using MiniChess.Core.State;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 유닛 하나의 표시(캡슐 + 이름/HP 라벨). 코어 Unit 을 읽기만 하며, Sync 로 상태를 그대로 반영한다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private const float BodyHeight = 1f;
        private const float BodyWidth = 0.6f;
        private const float LabelHeight = 1.45f;

        private Renderer _body;
        private MaterialPropertyBlock _block;
        private TextMeshPro _label;
        private Color _teamColor;
        private Color _actedColor;

        public Unit Unit { get; private set; }

        public static UnitView Create(Transform parent, Unit unit, Color teamColor, Color actedColor)
        {
            var root = new GameObject($"Unit {unit.Id} {unit.Team} {unit.Stats.Base.Name}");
            root.transform.SetParent(parent, false);

            var view = root.AddComponent<UnitView>();
            view.Unit = unit;
            view._teamColor = teamColor;
            view._actedColor = actedColor;
            view._block = new MaterialPropertyBlock();
            view._body = CreateBody(root.transform);
            view._label = CreateLabel(root.transform);

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

            SetBodyColor(Unit.HasActed ? _actedColor : _teamColor);
            _label.text = $"<size=70%>{Unit.Stats.Base.Name}</size>\n{Unit.Stats.CurrentHp}/{Unit.Stats.MaxHp}";
        }

        private void SetBodyColor(Color color)
        {
            _body.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _body.SetPropertyBlock(_block);
        }

        private static Renderer CreateBody(Transform parent)
        {
            // 기본 캡슐은 높이 2, 지름 1.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = Vector3.up * (BodyHeight * 0.5f);
            body.transform.localScale = new Vector3(BodyWidth, BodyHeight * 0.5f, BodyWidth);

            return body.GetComponent<Renderer>();
        }

        private static TextMeshPro CreateLabel(Transform parent)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * LabelHeight;
            go.AddComponent<Billboard>();

            var label = go.AddComponent<TextMeshPro>();
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.color = Color.white;
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;
            label.rectTransform.sizeDelta = new Vector2(2f, 1f);

            return label;
        }
    }
}
