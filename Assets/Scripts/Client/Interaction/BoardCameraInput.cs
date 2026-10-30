using MiniChess.Client.Common;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniChess.Client.Interaction
{
    /// <summary>
    /// 터치/마우스 동작으로 보드 카메라(BoardCameraRig)를 돌리고, 당기고, 옮긴다. 칸 선택(탭)은 BoardInput 이 한다.
    ///   - 모바일: 한 손가락 드래그 = 판 둘레로 회전(좌우) / 기울기(위아래). 두 손가락 = 벌리기·오므리기로 확대(손가락 사이 지점을 향해) + 같이 끌어 이동.
    ///   - 에디터/PC: 오른쪽 드래그 = 회전/기울기, 휠 = 확대(마우스 위치를 향해), 가운데 드래그 = 이동.
    ///   - 판 바깥 빈 곳 두 번 탭(BoardInput.EmptyDoubleTapped) = 기본 시점.
    /// 회전은 BoardInput.DragThreshold 이상 끌어야 시작한다(탭과 구분). UI 위에서 시작한 동작은 카메라를 움직이지 않는다.
    /// 두 손가락 조작 뒤 남은 한 손가락으로는 다 뗄 때까지 회전하지 않는다(확대하다 갑자기 돌지 않게).
    /// </summary>
    public class BoardCameraInput : MonoBehaviour
    {
        private BoardCameraRig _rig;
        private CameraSettings _settings;
        private BoardInput _boardInput;

        private bool _orbitPressed;
        private bool _orbiting;
        private Vector2 _orbitStart;
        private Vector2 _orbitLast;

        private bool _panning;
        private Vector2 _panLast;

        private bool _touchBlocked;
        private bool _pinching;
        private bool _pinchedThisTouch;
        private float _pinchDistance;
        private Vector2 _pinchCenter;

        public bool InputEnabled { get; set; } = true;

        public void Initialize(BoardCameraRig rig, CameraSettings settings, BoardInput boardInput)
        {
            _rig = rig;
            _settings = settings;

            if (_boardInput != null)
                _boardInput.EmptyDoubleTapped -= OnEmptyDoubleTapped;
            _boardInput = boardInput;
            if (_boardInput != null)
                _boardInput.EmptyDoubleTapped += OnEmptyDoubleTapped;

            ResetGestures();
        }

        private void OnDestroy()
        {
            if (_boardInput != null)
                _boardInput.EmptyDoubleTapped -= OnEmptyDoubleTapped;
        }

        private void OnEmptyDoubleTapped()
        {
            if (InputEnabled && _rig != null)
                _rig.ResetView();
        }

        private void Update()
        {
            if (!InputEnabled || _rig == null || _settings == null || !_rig.IsFramed)
            {
                ResetGestures();
                return;
            }

            if (Input.touchCount > 0)
            {
                UpdateTouch();
            }
            else
            {
                if (_pinchedThisTouch || _touchBlocked || _pinching)
                    ResetGestures();
                UpdateMouse();
            }
        }

        private void UpdateMouse()
        {
            Vector2 position = Input.mousePosition;
            bool overUi = IsPointerOverUi(-1);

            // 회전/기울기: 오른쪽 드래그
            if (Input.GetMouseButtonDown(1))
                BeginOrbit(position, !overUi);
            if (_orbitPressed && Input.GetMouseButton(1))
                DragOrbit(position);
            if (Input.GetMouseButtonUp(1))
                _orbitPressed = _orbiting = false;

            // 이동: 가운데 드래그
            if (Input.GetMouseButtonDown(2))
            {
                _panning = !overUi;
                _panLast = position;
            }
            if (_panning && Input.GetMouseButton(2))
            {
                _rig.Pan(position - _panLast);
                _panLast = position;
            }
            if (Input.GetMouseButtonUp(2))
                _panning = false;

            // 확대: 휠(마우스 위치를 향해)
            float scroll = Input.mouseScrollDelta.y;
            if (!overUi && Mathf.Abs(scroll) > 0.01f)
                _rig.Zoom(Mathf.Exp(-scroll * _settings.WheelZoomStep), position);
        }

        private void UpdateTouch()
        {
            Touch first = Input.GetTouch(0);
            if (first.phase == TouchPhase.Began && Input.touchCount == 1)
            {
                _touchBlocked = IsPointerOverUi(first.fingerId);
                _pinching = _pinchedThisTouch = false;
                BeginOrbit(first.position, !_touchBlocked);
            }

            if (_touchBlocked)
            {
                EndTouchIfReleased();
                return;
            }

            if (Input.touchCount >= 2)
            {
                Touch second = Input.GetTouch(1);
                Vector2 center = (first.position + second.position) * 0.5f;
                float distance = Vector2.Distance(first.position, second.position);

                if (!_pinching)
                {
                    _pinching = _pinchedThisTouch = true;
                    _orbitPressed = _orbiting = false;
                }
                else
                {
                    if (distance > 1f && _pinchDistance > 1f)
                        _rig.Zoom(_pinchDistance / distance, center);
                    _rig.Pan(center - _pinchCenter);
                }

                _pinchDistance = distance;
                _pinchCenter = center;
                EndTouchIfReleased();
                return;
            }

            // 한 손가락: 두 손가락 조작이 있었던 터치면 다 뗄 때까지 회전하지 않는다
            _pinching = false;
            if (!_pinchedThisTouch && _orbitPressed && (first.phase == TouchPhase.Moved || first.phase == TouchPhase.Stationary))
                DragOrbit(first.position);

            EndTouchIfReleased();
        }

        /// <summary>모든 손가락이 떨어지는 프레임이면 이번 터치 동작을 끝낸다.</summary>
        private void EndTouchIfReleased()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                TouchPhase phase = Input.GetTouch(i).phase;
                if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled)
                    return;
            }

            ResetGestures();
        }

        private void BeginOrbit(Vector2 position, bool allowed)
        {
            _orbitPressed = allowed;
            _orbiting = false;
            _orbitStart = _orbitLast = position;
        }

        /// <summary>DragThreshold 이상 끌면 회전을 시작하고, 그 뒤로는 움직인 만큼 돌린다(좌우 = yaw, 아래로 끌면 더 내려다본다).</summary>
        private void DragOrbit(Vector2 position)
        {
            if (!_orbiting)
            {
                float threshold = BoardInput.DragThreshold;
                if ((position - _orbitStart).sqrMagnitude < threshold * threshold)
                    return;

                _orbiting = true;
                _orbitLast = position; // 문턱을 넘는 순간 튀지 않게 여기서부터 잰다
                return;
            }

            Vector2 delta = position - _orbitLast;
            _orbitLast = position;
            float yaw = delta.x / Mathf.Max(1, Screen.width) * _settings.OrbitSpeed;
            float pitch = -delta.y / Mathf.Max(1, Screen.height) * _settings.PitchSpeed;
            _rig.Orbit(yaw, pitch);
        }

        private void ResetGestures()
        {
            _orbitPressed = _orbiting = false;
            _panning = false;
            _touchBlocked = false;
            _pinching = _pinchedThisTouch = false;
        }

        /// <summary>pointerId 가 UI 위인지. 마우스는 -1, 터치는 손가락 번호.</summary>
        private static bool IsPointerOverUi(int pointerId)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
        }
    }
}
