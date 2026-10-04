using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>
    /// 카메라를 Player1 쪽(-z)에서 보드를 비스듬히 내려다보도록 배치하고, 보드 전체가 화면에 들어오게 거리를 맞춘다.
    /// </summary>
    public class BoardCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField, Range(20f, 90f)] private float pitch = 60f;
        [SerializeField] private float padding = 1.1f;

        public Camera Camera => targetCamera;

        public void Frame(BoardCoordinates coordinates)
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null)
            {
                Debug.LogError("[BoardCameraRig] 카메라가 없음");
                return;
            }

            Vector2 size = coordinates.Size;
            float radius = Mathf.Sqrt(size.x * size.x + size.y * size.y) * 0.5f * padding;

            float verticalHalfFov = targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * targetCamera.aspect);
            float distance = radius / Mathf.Sin(Mathf.Min(verticalHalfFov, horizontalHalfFov));

            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            Transform cameraTransform = targetCamera.transform;
            cameraTransform.rotation = rotation;
            cameraTransform.position = coordinates.Center - rotation * Vector3.forward * distance;
        }
    }
}
