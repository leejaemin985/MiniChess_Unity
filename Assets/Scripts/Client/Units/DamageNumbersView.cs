using System.Collections.Generic;
using System.Globalization;
using MiniChess.Client.Common;
using MiniChess.Client.UI;
using MiniChess.Core.Combat;
using MiniChess.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 확정된 피해 이벤트의 HP 피해/보호막 흡수량만 표시한다. HP 차이로 피해를 추측하지 않는다.
    /// 숫자는 전용 Screen Space Overlay 캔버스에 그려 3D 유닛/이펙트/월드 UI 에 가려지지 않고, 포스트 프로세싱(톤매핑)도 받지 않는다.
    /// 위치만 유닛 머리 위 월드 좌표를 화면 좌표로 바꿔 따라간다. 거리/높이 값은 캔버스 기준 해상도(UiFactory.ReferenceResolution) 단위다.
    /// </summary>
    public class DamageNumbersView : MonoBehaviour
    {
        // Code-owned colors: Unity must not restore old instance values after script reload.
        private static readonly Color damageColor = new Color32(0xFF, 0x50, 0x50, 0xFF);
        private static readonly Color fatigueColor = new Color32(0x9A, 0x32, 0xE6, 0xFF);
        [SerializeField, Min(0.1f)] private float duration = 1.5f;
        [Tooltip("떠오르는 높이(캔버스 단위).")]
        [SerializeField, Min(0f)] private float riseHeight = 70f;
        [Tooltip("같은 유닛에 겹친 숫자 사이 간격(캔버스 단위).")]
        [SerializeField, Min(0f)] private float stackHeight = 44f;
        [Tooltip("글자 크기(캔버스 단위).")]
        [SerializeField, Min(1f)] private float screenFontSize = 40f;
        [Tooltip("숫자 외곽선 색.")]
        [SerializeField] private Color outlineColor = Color.black;
        [Tooltip("숫자 외곽선 두께(TMP SDF 단위, 0 이면 없음).")]
        [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.27f;
        [Tooltip("HUD 캔버스보다 위에 그려 다른 UI 에도 묻히지 않게 한다.")]
        [SerializeField] private int canvasSortingOrder = 100;

        private sealed class Popup
        {
            /// <summary>겹침(위로 쌓기)을 세는 대상 키. 유닛은 "u{Id}", 장애물은 "o{칸}".</summary>
            public string Key;

            /// <summary>따라갈 유닛. 장애물 숫자는 null(Anchor 고정).</summary>
            public UnitView Target;
            public Vector3 Anchor;
            public float StackOffset;
            public float Age;
            public TextMeshProUGUI Text;
        }

        private readonly List<Popup> _popups = new List<Popup>();
        private readonly Stack<TextMeshProUGUI> _pool = new Stack<TextMeshProUGUI>();
        private UnitsView _units;
        private Camera _camera;
        private Canvas _canvas;
        private Material _outlineMaterial;

        public void Initialize(UnitsView units)
        {
            _units = units;
        }

        /// <summary>확정 피해 이벤트가 들어올 때만 호출한다. 실제 HP 피해/보호막 흡수가 0 이면 표시하지 않는다.</summary>
        public void Play(UnitDamagedEvent damaged)
        {
            if (damaged == null || _units == null || !_units.TryGetView(damaged.TargetId, out UnitView view))
                return;

            // 보호막 흡수와 HP 피해를 나누지 않고 받은 피해 전체를 숫자 하나로 보인다.
            int total = damaged.AppliedAmount + damaged.ShieldAbsorbed;
            if (total > 0)
                Spawn($"u{view.Unit.Id}", view, view.DamageNumberPosition, total, ColorOf(damaged.Type));
        }

        /// <summary>장애물 피해 숫자. anchor 는 장애물 체력 바 위(BoardView.GetObstacleDamageAnchor). 장애물이 부서져도 끝까지 남는다.</summary>
        public void Play(ObstacleDamagedEvent damaged, Vector3 anchor)
        {
            if (damaged != null && damaged.AppliedAmount > 0)
                Spawn($"o{damaged.Position}", null, anchor, damaged.AppliedAmount, ColorOf(damaged.Type));
        }

        private static Color ColorOf(DamageType type) => type == DamageType.Fatigue ? fatigueColor : damageColor;

        private void Spawn(string key, UnitView target, Vector3 anchor, int amount, Color color)
        {
            int stack = 0;
            foreach (Popup popup in _popups)
            {
                if (popup.Key == key)
                    stack++;
            }

            // 유닛 뷰가 아닌 전용 캔버스 아래에 두어 사망/스냅샷 Rebind 후에도 숫자는 끝까지 남는다.
            TextMeshProUGUI text = Rent();
            text.gameObject.name = $"Damage {key} {amount}";
            text.text = amount.ToString(CultureInfo.InvariantCulture);
            text.color = color;

            var entry = new Popup
            {
                Key = key,
                Target = target,
                Anchor = anchor,
                StackOffset = stack * stackHeight,
                Text = text,
            };
            _popups.Add(entry);
            Place(entry, 0f);
        }

        private void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;

            float lifetime = Mathf.Max(0.1f, duration);
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];
                popup.Age += Time.unscaledDeltaTime;
                if (popup.Text == null || popup.Age >= lifetime)
                {
                    Return(popup.Text);
                    _popups.RemoveAt(i);
                    continue;
                }

                if (popup.Target != null)
                    popup.Anchor = popup.Target.DamageNumberPosition;
                Place(popup, popup.Age / lifetime);
            }
        }

        private void Place(Popup popup, float progress)
        {
            TextMeshProUGUI text = popup.Text;
            Vector3 screen = _camera != null ? _camera.WorldToScreenPoint(popup.Anchor) : popup.Anchor;
            if (_camera != null && screen.z <= 0f)
            {
                // 카메라 뒤: 화면에 뒤집혀 나타나지 않게 숨긴다.
                text.alpha = 0f;
                return;
            }

            // Overlay 캔버스의 월드 좌표는 화면 픽셀이므로, 캔버스 단위 오프셋은 scaleFactor 를 곱해 픽셀로 바꾼다.
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            float offset = (popup.StackOffset + riseHeight * progress) * scale;
            text.rectTransform.position = new Vector3(screen.x, screen.y + offset, 0f);
            text.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(progress * 8f));
            text.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, progress));
        }

        public void Clear()
        {
            foreach (Popup popup in _popups)
                Return(popup.Text);
            _popups.Clear();
        }

        private TextMeshProUGUI Rent()
        {
            while (_pool.Count > 0)
            {
                TextMeshProUGUI pooled = _pool.Pop();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            EnsureCanvas();
            TextMeshProUGUI text = UiFactory.CreateText(_canvas.transform, "Damage", screenFontSize, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.rectTransform.sizeDelta = new Vector2(240f, 96f);
            // 외곽선은 공유 머티리얼 하나에 넣는다(텍스트마다 outlineWidth 를 주면 머티리얼이 복제되어 캔버스 배칭이 깨진다).
            if (_outlineMaterial == null)
            {
                _outlineMaterial = new Material(text.fontSharedMaterial) { name = "DamageNumber Outline" };
                ApplyOutline();
            }
            text.fontSharedMaterial = _outlineMaterial;
            return text;
        }

        /// <summary>외곽선 색/두께를 공유 머티리얼에 넣는다. 모든 숫자가 이 머티리얼을 쓰므로 한 번에 바뀐다.</summary>
        private void ApplyOutline()
        {
            if (_outlineMaterial == null)
                return;

            if (outlineWidth > 0f)
                _outlineMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            else
                _outlineMaterial.DisableKeyword(ShaderUtilities.Keyword_Outline);
            _outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            _outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        }

        // 플레이 중 인스펙터에서 바꾸면 떠 있는 숫자에도 바로 반영한다.
        private void OnValidate()
        {
            ApplyOutline();
        }

        private void Return(TextMeshProUGUI text)
        {
            if (text == null)
                return;
            text.gameObject.SetActive(false);
            _pool.Push(text);
        }

        private void EnsureCanvas()
        {
            if (_canvas != null)
                return;

            // 클릭을 받지 않으므로 GraphicRaycaster 없이 만든다.
            var go = new GameObject("DamageNumbersCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = canvasSortingOrder;
            UiFactory.ConfigureScaler(go.AddComponent<CanvasScaler>());
        }

        private void OnDestroy()
        {
            if (_outlineMaterial != null)
                Destroy(_outlineMaterial);
        }
    }
}
