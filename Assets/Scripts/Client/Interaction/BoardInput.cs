using System;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniChess.Client.Interaction
{
    /// <summary>
    /// 탭/클릭을 보드 칸 좌표로 바꿔 알린다. 게임 규칙은 모른다.
    /// 누른 뒤 거의 움직이지 않고 뗐을 때만 탭으로 본다(DragThreshold 이상 끌면 카메라 조작이라 BoardCameraInput 이 처리).
    /// 모바일은 첫 손가락 하나만 탭이 되고, 두 손가락이 닿은 터치는 탭이 아니다.
    /// </summary>
    public class BoardInput : MonoBehaviour
    {
        [SerializeField] private float maxRayDistance = 200f;

        [Tooltip("판 바깥 빈 곳을 두 번 탭한 것으로 보는 최대 간격(초).")]
        [SerializeField] private float doubleTapTime = 0.3f;

        private Camera _camera;
        private BoardCoordinates _coordinates;
        private Press _left;
        private Press _right;
        private Press _touch;
        private float _lastEmptyTapTime = float.NegativeInfinity;
        private Vector2 _lastEmptyTapPosition;

        private struct Press
        {
            public bool Active;
            public bool Moved;
            public Vector2 Start;
        }

        /// <summary>누른 뒤 이만큼(픽셀) 움직이면 탭이 아니라 드래그(카메라 조작)로 본다. 화면 밀도에 맞춰 약 2mm.</summary>
        public static float DragThreshold => Mathf.Max(10f, Screen.dpi > 0f ? Screen.dpi * 0.08f : 10f);

        /// <summary>탭/좌클릭한 칸. 보드 밖 탭은 null.</summary>
        public event Action<Position?> CellClicked;

        /// <summary>우클릭(선택 취소용).</summary>
        public event Action CancelClicked;

        /// <summary>판 바깥 빈 곳을 두 번 탭했다(카메라를 기본 시점으로 돌릴 때 쓴다).</summary>
        public event Action EmptyDoubleTapped;

        /// <summary>마우스(또는 마지막으로 누른 손가락)가 가리키는 칸이 바뀌었다. 보드 밖이나 UI 위면 null.</summary>
        public event Action<Position?> HoverChanged;

        public bool InputEnabled { get; set; } = true;

        public Position? HoveredCell { get; private set; }

        public void Initialize(Camera targetCamera, BoardCoordinates coordinates)
        {
            _camera = targetCamera;
            _coordinates = coordinates;
            HoveredCell = null;
        }

        private void Awake()
        {
            // 터치는 터치로만 처리한다(마우스 흉내로 같은 탭이 두 번 들어오지 않게)
            Input.simulateMouseWithTouches = false;
        }

        private void Update()
        {
            if (!InputEnabled || _camera == null || _coordinates == null)
            {
                _left = _right = _touch = default;
                return;
            }

            if (Input.touchCount > 0 || _touch.Active)
                UpdateTouch();
            else
                UpdateMouse();
        }

        private void UpdateMouse()
        {
            Vector2 position = Input.mousePosition;
            bool overUi = IsPointerOverUi(-1);
            SetHovered(overUi ? null : RaycastCell(position));

            if (TrackMouse(ref _left, 0, overUi))
                Tap(position);
            if (TrackMouse(ref _right, 1, overUi))
                CancelClicked?.Invoke();
        }

        /// <summary>버튼을 UI 밖에서 눌렀다가 거의 움직이지 않고 떼면 true.</summary>
        private static bool TrackMouse(ref Press press, int button, bool overUi)
        {
            Vector2 position = Input.mousePosition;
            if (Input.GetMouseButtonDown(button))
                press = new Press { Active = !overUi, Start = position };

            if (!press.Active)
                return false;

            if ((position - press.Start).sqrMagnitude > DragThreshold * DragThreshold)
                press.Moved = true;

            if (!Input.GetMouseButtonUp(button))
                return false;

            bool tap = !press.Moved;
            press = default;
            return tap;
        }

        private void UpdateTouch()
        {
            if (Input.touchCount == 0)
            {
                _touch = default;
                return;
            }

            Touch finger = Input.GetTouch(0);
            if (finger.phase == TouchPhase.Began)
                _touch = new Press { Active = !IsPointerOverUi(finger.fingerId), Start = finger.position };

            if (!_touch.Active)
                return;

            // 두 손가락(확대/이동)이 닿았거나 끌었으면 탭이 아니다
            if (Input.touchCount > 1 || (finger.position - _touch.Start).sqrMagnitude > DragThreshold * DragThreshold)
                _touch.Moved = true;

            SetHovered(RaycastCell(finger.position));

            if (finger.phase == TouchPhase.Canceled)
            {
                _touch = default;
            }
            else if (finger.phase == TouchPhase.Ended)
            {
                bool tap = !_touch.Moved;
                _touch = default;
                if (tap)
                    Tap(finger.position);
            }
        }

        private void Tap(Vector2 screenPosition)
        {
            Position? cell = RaycastCell(screenPosition);
            CellClicked?.Invoke(cell);
            if (cell.HasValue)
                return;

            // 판 바깥 빈 곳 두 번 탭
            float now = Time.unscaledTime;
            bool near = (screenPosition - _lastEmptyTapPosition).sqrMagnitude < DragThreshold * DragThreshold * 9f;
            if (now - _lastEmptyTapTime <= doubleTapTime && near)
            {
                _lastEmptyTapTime = float.NegativeInfinity;
                EmptyDoubleTapped?.Invoke();
                return;
            }

            _lastEmptyTapTime = now;
            _lastEmptyTapPosition = screenPosition;
        }

        private void SetHovered(Position? cell)
        {
            if (HoveredCell == cell)
                return;

            HoveredCell = cell;
            HoverChanged?.Invoke(cell);
        }

        private Position? RaycastCell(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
                return null;

            return _coordinates.TryGetPosition(hit.point, out Position position) ? position : (Position?)null;
        }

        /// <summary>pointerId 가 UI 위인지. 마우스는 -1, 터치는 손가락 번호.</summary>
        private static bool IsPointerOverUi(int pointerId)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
        }
    }
}
