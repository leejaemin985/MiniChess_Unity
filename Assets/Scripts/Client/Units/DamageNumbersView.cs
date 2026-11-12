using System.Collections.Generic;
using System.Globalization;
using MiniChess.Client.Common;
using MiniChess.Core.Combat;
using MiniChess.Core.Events;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>확정된 피해 이벤트의 HP 피해/보호막 흡수량만 표시한다. HP 차이로 피해를 추측하지 않는다.</summary>
    public class DamageNumbersView : MonoBehaviour
    {
        [SerializeField] private Color damageColor = new Color(1f, 0.32f, 0.25f);
        [SerializeField] private Color fatigueColor = new Color32(0xB9, 0x9C, 0xFF, 0xFF);
        [SerializeField, Min(0.1f)] private float duration = 0.9f;
        [SerializeField, Min(0f)] private float riseDistance = 0.65f;
        [SerializeField, Min(0f)] private float stackSpacing = 0.22f;
        [SerializeField, Min(0.1f)] private float fontSize = 5.5f;

        private sealed class Popup
        {
            public int UnitId;
            public UnitView Target;
            public Vector3 Anchor;
            public float StackOffset;
            public float Age;
            public TextMeshPro Text;
        }

        private readonly List<Popup> _popups = new List<Popup>();
        private UnitsView _units;
        private Camera _camera;

        public void Initialize(UnitsView units)
        {
            _units = units;
        }

        /// <summary>확정 피해 이벤트가 들어올 때만 호출한다. 실제 HP 피해/보호막 흡수가 0 이면 표시하지 않는다.</summary>
        public void Play(UnitDamagedEvent damaged)
        {
            if (damaged == null || _units == null || !_units.TryGetView(damaged.TargetId, out UnitView view))
                return;

            Color color = damaged.Type == DamageType.Fatigue ? fatigueColor : damageColor;
            if (damaged.AppliedAmount > 0)
                Spawn(view, damaged.AppliedAmount, color);
            if (damaged.ShieldAbsorbed > 0)
                Spawn(view, damaged.ShieldAbsorbed, color);
        }

        private void Spawn(UnitView view, int amount, Color color)
        {
            int stack = 0;
            foreach (Popup popup in _popups)
            {
                if (popup.UnitId == view.Unit.Id)
                    stack++;
            }

            // 유닛 뷰의 자식으로 만들지 않아 사망/스냅샷 Rebind 후에도 숫자는 끝까지 남는다.
            var go = new GameObject($"Damage {view.Unit.Id} -{amount}");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            KoreanFont.Apply(text);
            text.text = "-" + amount.ToString(CultureInfo.InvariantCulture);
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.color = color;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;
            text.rectTransform.sizeDelta = new Vector2(2f, 0.8f);
            text.renderer.sortingOrder = 50;

            var entry = new Popup
            {
                UnitId = view.Unit.Id,
                Target = view,
                Anchor = view.DamageNumberPosition,
                StackOffset = stack * stackSpacing,
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
                    if (popup.Text != null)
                        Destroy(popup.Text.gameObject);
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
            Vector3 up = _camera != null ? _camera.transform.up : Vector3.up;
            popup.Text.transform.position = popup.Anchor + up * (popup.StackOffset + riseDistance * progress);
            if (_camera != null)
                popup.Text.transform.rotation = _camera.transform.rotation;
            popup.Text.transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(progress * 8f));
            popup.Text.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, progress));
        }

        public void Clear()
        {
            foreach (Popup popup in _popups)
            {
                if (popup.Text != null)
                    Destroy(popup.Text.gameObject);
            }
            _popups.Clear();
        }
    }
}
