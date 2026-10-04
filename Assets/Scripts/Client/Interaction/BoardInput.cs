using System;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniChess.Client.Interaction
{
    /// <summary>마우스 클릭을 보드 칸 좌표로 바꿔 알린다. 게임 규칙은 모른다.</summary>
    public class BoardInput : MonoBehaviour
    {
        [SerializeField] private float maxRayDistance = 200f;

        private Camera _camera;
        private BoardCoordinates _coordinates;

        /// <summary>좌클릭한 칸. 보드 밖 클릭은 null.</summary>
        public event Action<Position?> CellClicked;

        /// <summary>우클릭(선택 취소용).</summary>
        public event Action CancelClicked;

        /// <summary>마우스가 가리키는 칸이 바뀌었다. 보드 밖이나 UI 위면 null.</summary>
        public event Action<Position?> HoverChanged;

        public bool InputEnabled { get; set; } = true;

        public Position? HoveredCell { get; private set; }

        public void Initialize(Camera targetCamera, BoardCoordinates coordinates)
        {
            _camera = targetCamera;
            _coordinates = coordinates;
            HoveredCell = null;
        }

        private void Update()
        {
            if (!InputEnabled || _camera == null || _coordinates == null)
                return;

            if (IsPointerOverUi())
            {
                SetHovered(null);
                return;
            }

            Position? cell = RaycastCell();
            SetHovered(cell);

            if (Input.GetMouseButtonDown(0))
                CellClicked?.Invoke(cell);
            else if (Input.GetMouseButtonDown(1))
                CancelClicked?.Invoke();
        }

        private void SetHovered(Position? cell)
        {
            if (HoveredCell == cell)
                return;

            HoveredCell = cell;
            HoverChanged?.Invoke(cell);
        }

        private Position? RaycastCell()
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
                return null;

            return _coordinates.TryGetPosition(hit.point, out Position position) ? position : (Position?)null;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
