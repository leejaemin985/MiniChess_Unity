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
    /// 확정 전 배치(PlacementController.Pending)를 몸체 + 발밑 옅은 팀 색 고리 + 이름으로 보여준다.
    /// 몸체는 UnitView 와 같이 캐릭터 모델 프리팹(있으면, 상대 진영 쪽을 봄)을, 없으면 옅은 팀 색 캡슐을 쓴다.
    /// 확정되면 코어 유닛이 생겨 UnitsView 가 그리므로 여기서는 지운다.
    /// </summary>
    public class PlacementPreviewView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        /// <summary>UnitView 의 발밑 팀 고리와 같은 머티리얼/지름.</summary>
        private const string TeamRingPath = "Materials/TeamRing";
        private const float TeamRingSize = 0.8f;

        [SerializeField, Range(0f, 1f)] private float ghostLighten = 0.5f;

        private readonly List<GameObject> _ghosts = new List<GameObject>();
        private PlacementController _placement;
        private Func<BoardCoordinates> _coordinates;
        private Func<Team, Color> _teamColor;
        private Func<string, string> _characterName;
        private Func<string, GameObject> _bodyPrefab;
        private Func<Team, float> _facingYaw;

        /// <param name="bodyPrefab">캐릭터 Id → 몸체 모델 프리팹(없으면 null, 캡슐). null 이면 모두 캡슐.</param>
        /// <param name="facingYaw">팀 → 몸체가 바라볼 방향(도). null 이면 +z.</param>
        public void Initialize(
            PlacementController placement, Func<BoardCoordinates> coordinates, Func<Team, Color> teamColor, Func<string, string> characterName,
            Func<string, GameObject> bodyPrefab = null, Func<Team, float> facingYaw = null)
        {
            if (_placement != null)
                _placement.Changed -= Refresh;

            _placement = placement;
            _coordinates = coordinates;
            _teamColor = teamColor;
            _characterName = characterName;
            _bodyPrefab = bodyPrefab;
            _facingYaw = facingYaw;
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

            Team team = _placement.PlacingTeam.Value;
            Color color = Color.Lerp(_teamColor(team), Color.white, ghostLighten);
            float facing = _facingYaw?.Invoke(team) ?? 0f;
            foreach (KeyValuePair<Position, string> pair in _placement.Pending)
                _ghosts.Add(CreateGhost(coordinates.ToWorld(pair.Key), color, _characterName(pair.Value), _bodyPrefab?.Invoke(pair.Value), facing));
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

        private GameObject CreateGhost(Vector3 position, Color color, string label, GameObject bodyPrefab, float facingYaw)
        {
            var root = new GameObject($"Placement {label}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;

            if (bodyPrefab != null)
            {
                // UnitView 와 같은 모델 몸체(피벗 발밑, 높이 1), 상대 진영 쪽을 본다.
                GameObject model = Instantiate(bodyPrefab, root.transform, false);
                model.name = "Body";
                model.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f) * model.transform.localRotation;
            }
            else
            {
                // UnitView 와 같은 크기(높이 1, 폭 0.6)의 캡슐.
                GameObject body = RuntimePrimitive.Create(PrimitiveType.Capsule);
                body.name = "Body";
                Destroy(body.GetComponent<Collider>()); // 클릭은 칸이 받는다
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.up * 0.5f;
                body.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
                SetColor(body.GetComponent<Renderer>(), color);
            }

            CreateRing(root.transform, color);

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

        /// <summary>발밑에 옅은 팀 색 고리(UnitView 의 팀 고리와 같은 모양).</summary>
        private static void CreateRing(Transform parent, Color color)
        {
            Material material = Resources.Load<Material>(TeamRingPath);
            if (material == null)
                return;

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Team Ring";
            Destroy(ring.GetComponent<Collider>()); // 클릭은 칸이 받는다
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = Vector3.up * 0.015f;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕힌다
            ring.transform.localScale = Vector3.one * TeamRingSize;
            var renderer = ring.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetColor(renderer, color);
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
