using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>항상 카메라와 같은 방향을 바라보게 한다(월드 공간 라벨용).</summary>
    public class Billboard : MonoBehaviour
    {
        private Camera _camera;

        private void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;

            if (_camera != null)
                transform.rotation = _camera.transform.rotation;
        }
    }
}
