using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>
    /// 카메라가 보드를 비스듬히 내려다본다. 판 위의 한 점(초점)을 중심으로 돌고(yaw 360도 자유, pitch 는 범위 안), 당기고(거리), 옮긴다(초점).
    ///   - Frame: 기본 시점을 정한다(보드 전체가 화면에 들어오는 거리 × HomeDistance, 내 진영 쪽 yaw). ResetView 로 언제든 돌아온다.
    ///   - Orbit/Zoom/Pan: 입력(BoardCameraInput)이 목표 시점만 바꾸고, 카메라는 LateUpdate 에서 목표를 부드럽게 따라간다.
    ///   - 확대는 가리킨 지점을 향해 당긴다(그 지점이 화면에서 그 자리에 남는다). 가장 멀리는 보드 전체가 들어오는 거리.
    ///   - 초점은 판 안에서만 움직이고, 멀어질수록 판 중심으로 모인다(가장 멀리서는 판 중심) → 판을 화면 밖으로 잃지 않는다.
    /// yaw 0 은 Player1 쪽(-z)에서 +z 를 바라보는 방향이고, 180 이면 반대편에서 본다.
    /// HUD 가 화면 위/아래를 가리면(SetScreenInsets) 그 사이 영역을 화면처럼 쓴다(투영 중심을 옮기고 그 영역에 맞춰 거리를 구한다).
    /// </summary>
    public class BoardCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        private BoardCoordinates _coordinates;
        private CameraSettings _settings;
        private View _home;
        private View _target;
        private View _current;
        private float _minDistance;
        /// <summary>가장 멀리: 보드 전체가 화면에 들어오는 거리.</summary>
        private float _maxDistance;
        /// <summary>HUD 가 가리는 화면 위/아래(화면 높이 대비 0~1). 보드는 그 사이 영역의 가운데에 맞춘다.</summary>
        private float _insetTop;
        private float _insetBottom;

        private struct View
        {
            public Vector3 Focus;
            public float Yaw;
            public float Pitch;
            public float Distance;
        }

        public Camera Camera => targetCamera;

        /// <summary>Frame 으로 기본 시점이 정해졌는지(그 전에는 조작하지 않는다).</summary>
        public bool IsFramed => _coordinates != null && targetCamera != null;

        public void Frame(BoardCoordinates coordinates, CameraSettings settings, float yaw)
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null)
            {
                Debug.LogError("[BoardCameraRig] 카메라가 없음");
                return;
            }

            _coordinates = coordinates;
            _settings = settings;
            if (settings.FieldOfView > 0f)
                targetCamera.fieldOfView = settings.FieldOfView;

            _home = new View
            {
                Focus = coordinates.Center,
                Yaw = yaw,
                Pitch = Mathf.Clamp(settings.Pitch, settings.MinPitch, settings.MaxPitch),
            };
            UpdateDistances();
            _target = _home;
            _current = _home;
            Apply(_current);
        }

        /// <summary>
        /// HUD 가 가리는 화면 위/아래 비율(화면 높이 대비 0~1)을 정한다. 보드는 그 사이 영역(보이는 영역)의 가운데에, 그 영역에 다 들어오게 맞춘다.
        /// 바뀌면 거리 범위와 기본 시점을 다시 구한다(지금 시점은 새 범위 안으로만 당긴다).
        /// </summary>
        public void SetScreenInsets(float top, float bottom)
        {
            top = Mathf.Clamp(top, 0f, 0.45f);
            bottom = Mathf.Clamp(bottom, 0f, 0.45f);
            if (Mathf.Approximately(top, _insetTop) && Mathf.Approximately(bottom, _insetBottom))
                return;

            _insetTop = top;
            _insetBottom = bottom;
            if (!IsFramed)
                return;

            UpdateDistances();
            _target.Distance = Mathf.Clamp(_target.Distance, _minDistance, _maxDistance);
            ClampFocus();
        }

        /// <summary>보이는 영역(HUD 사이)의 좁은 쪽 시야각에 맞춰 가장 멀리/가까이 거리와 기본 거리를 구한다.</summary>
        private void UpdateDistances()
        {
            Vector2 size = _coordinates.Size;
            float radius = Mathf.Sqrt(size.x * size.x + size.y * size.y) * 0.5f * _settings.Padding;

            // 세로/가로 중 좁은 쪽 시야각에 맞춘다. 세로는 HUD 가 가린 만큼 좁다.
            float verticalHalfFov = targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * targetCamera.aspect);
            float visibleHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * VisibleHeight);
            float narrowHalfFov = Mathf.Min(visibleHalfFov, horizontalHalfFov);
            float distance = radius / Mathf.Sin(narrowHalfFov);

            // 가장 가까이: 좁은 쪽에 칸 MinVisibleCells 개가 들어오는 거리
            _maxDistance = distance;
            _minDistance = Mathf.Min(distance, _settings.MinVisibleCells * _coordinates.CellSize * 0.5f / Mathf.Tan(narrowHalfFov));
            _home.Distance = Mathf.Clamp(distance * _settings.HomeDistance, _minDistance, _maxDistance);
        }

        /// <summary>HUD 사이 보이는 영역의 높이(화면 높이 대비).</summary>
        private float VisibleHeight => Mathf.Max(0.1f, 1f - _insetTop - _insetBottom);

        /// <summary>기본 시점으로 돌아간다(지금 방향에서 가까운 쪽으로 돈다).</summary>
        public void ResetView()
        {
            if (!IsFramed)
                return;

            float yaw = _target.Yaw + Mathf.DeltaAngle(_target.Yaw, _home.Yaw);
            _target = _home;
            _target.Yaw = yaw;
        }

        /// <summary>판 둘레로 돌고(yaw, 제한 없음) 기울인다(pitch, 범위 안).</summary>
        public void Orbit(float yawDelta, float pitchDelta)
        {
            if (!IsFramed)
                return;

            _target.Yaw += yawDelta;
            _target.Pitch = Mathf.Clamp(_target.Pitch + pitchDelta, _settings.MinPitch, _settings.MaxPitch);
        }

        /// <summary>factor 배 거리로 당기거나 민다(1 보다 작으면 가까이). 화면의 screenPoint 아래 판 위 점을 향해.</summary>
        public void Zoom(float factor, Vector2 screenPoint)
        {
            if (!IsFramed || factor <= 0f || _target.Distance <= 0f)
                return;

            float distance = Mathf.Clamp(_target.Distance * factor, _minDistance, _maxDistance);
            float scale = distance / _target.Distance;

            // 가리킨 점을 중심으로 카메라와 초점을 같은 비율로 당기면 그 점이 화면에서 그 자리에 남는다
            if (TryGetGroundPoint(screenPoint, out Vector3 point))
                _target.Focus = point + (_target.Focus - point) * scale;

            _target.Distance = distance;
            ClampFocus();
        }

        /// <summary>화면에서 끈 만큼(픽셀) 판이 손가락을 따라오도록 초점을 옮긴다.</summary>
        public void Pan(Vector2 screenDelta)
        {
            if (!IsFramed)
                return;

            // 초점 거리에서 화면 1픽셀이 판 위 몇 m 인지. 비스듬히 보므로 화면 세로 이동은 판 위에서 1/sin(pitch) 배 길다.
            float worldPerPixel = 2f * _target.Distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            float pitchStretch = 1f / Mathf.Max(0.3f, Mathf.Sin(_target.Pitch * Mathf.Deg2Rad));

            Quaternion yawRotation = Quaternion.Euler(0f, _target.Yaw, 0f);
            Vector3 right = yawRotation * Vector3.right;
            Vector3 forward = yawRotation * Vector3.forward;
            _target.Focus -= (right * screenDelta.x + forward * (screenDelta.y * pitchStretch)) * worldPerPixel;
            ClampFocus();
        }

        private void LateUpdate()
        {
            if (!IsFramed)
                return;

            float t = _settings.Smoothing <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / _settings.Smoothing);
            _current.Focus = Vector3.Lerp(_current.Focus, _target.Focus, t);
            _current.Yaw = Mathf.Lerp(_current.Yaw, _target.Yaw, t);
            _current.Pitch = Mathf.Lerp(_current.Pitch, _target.Pitch, t);
            _current.Distance = Mathf.Lerp(_current.Distance, _target.Distance, t);
            Apply(_current);
        }

        /// <summary>당길수록 초점을 판 가장자리까지 옮길 수 있고, 가장 멀리(보드 전체 거리)에서는 판 중심으로 모은다.</summary>
        private void ClampFocus()
        {
            float zoom = Mathf.InverseLerp(_maxDistance, _minDistance, _target.Distance);
            Vector2 half = _coordinates.Size * (0.5f * zoom);
            Vector3 center = _coordinates.Center;
            Vector3 offset = _target.Focus - center;
            _target.Focus = center + new Vector3(Mathf.Clamp(offset.x, -half.x, half.x), 0f, Mathf.Clamp(offset.z, -half.y, half.y));
        }

        private void Apply(View view)
        {
            Quaternion rotation = Quaternion.Euler(view.Pitch, view.Yaw, 0f);
            Transform cameraTransform = targetCamera.transform;
            cameraTransform.rotation = rotation;
            cameraTransform.position = view.Focus - rotation * Vector3.forward * view.Distance;

            // 화면 중심을 HUD 사이 보이는 영역의 가운데로 옮긴다(투영 중심 이동). 위/아래 가림이 같으면 그대로.
            targetCamera.ResetProjectionMatrix();
            float shift = _insetTop - _insetBottom;
            if (!Mathf.Approximately(shift, 0f))
            {
                Matrix4x4 projection = targetCamera.projectionMatrix;
                projection.m12 += shift;
                targetCamera.projectionMatrix = projection;
            }
        }

        /// <summary>화면 점 아래 판 높이(y = 0) 평면 위의 점.</summary>
        private bool TryGetGroundPoint(Vector2 screenPoint, out Vector3 point)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = default;
            return false;
        }
    }
}
