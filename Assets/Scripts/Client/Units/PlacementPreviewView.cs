using System;
using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Client.Interaction;
using MiniChess.Core.Common;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 확정 전 배치(PlacementController.Pending)를 옅은 팀 색 캡슐 + 이름으로 보여준다.
    /// 확정되면 코어 유닛이 생겨 UnitsView 가 그리므로 여기서는 지운다.
    /// </summary>
    public class PlacementPreviewView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Range(0f, 1f)] private float ghostLighten = 0.5f;

        private readonly List<GameObject> _ghosts = new List<GameObject>();
        private PlacementController _placement;
        private Func<BoardCoordinates> _coordinates;
        private Func<Team, Color> _teamColor;
        private Func<string, string> _characterName;

        public void Initialize(
            PlacementController placement, Func<BoardCoordinates> coordinates, Func<Team, Color> teamColor, Func<string, string> characterName)
        {
            if (_placement != null)
                _placement.Changed -= Refresh;

            _placement = placement;
            _coordinates = coordinates;
            _teamColor = teamColor;
            _characterName = characterName;
            _placement.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (_placement != null)
                _placement.Changed -= Refresh;
        }

        private void Refresh()
        {
            Clear();

            BoardCoordinates coordinates = _coordinates();
            if (!_placement.PlacingTeam.HasValue || coordinates == null)
                return;

            Color color = Color.Lerp(_teamColor(_placement.PlacingTeam.Value), Color.white, ghostLighten);
            foreach (KeyValuePair<Position, string> pair in _placement.Pending)
                _ghosts.Add(CreateGhost(coordinates.ToWorld(pair.Key), color, _characterName(pair.Value)));
        }

        private void Clear()
        {
            foreach (GameObject ghost in _ghosts)
            {
                if (ghost != null)
                    Destroy(ghost);
            }

            _ghosts.Clear();
        }

        private GameObject CreateGhost(Vector3 position, Color color, string label)
        {
            var root = new GameObject($"Placement {label}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;

            // UnitView 와 같은 크기(높이 1, 폭 0.6)의 캡슐.
            GameObject body = RuntimePrimitive.Create(PrimitiveType.Capsule);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>()); // 클릭은 칸이 받는다
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 0.5f;
            body.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            var block = new MaterialPropertyBlock();
            var renderer = body.GetComponent<Renderer>();
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = Vector3.up * 1.1f;
            labelObject.AddComponent<Billboard>();

            var text = labelObject.AddComponent<TextMeshPro>();
            KoreanFont.Apply(text);
            text.text = $"<size=70%>{label}</size>";
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Bottom;
            text.enableWordWrapping = false;
            text.color = Color.white;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.rectTransform.pivot = new Vector2(0.5f, 0f);
            text.rectTransform.sizeDelta = new Vector2(2.5f, 1f);

            return root;
        }
    }
}
