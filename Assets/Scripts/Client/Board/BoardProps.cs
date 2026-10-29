using MiniChess.Client.Common;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 밖 석조물(아치, 석상 등)을 BoardArt 의 자리 목록대로 외곽 벽 바깥 땅에 세운다.
    /// 자리는 보드 외곽 직사각형 기준(모서리/변 + 칸 단위 거리)이라 맵 크기가 달라도 따라간다. 클릭 대상이 아니다.
    /// 프리팹을 그대로 만들 뿐이라 메시/머티리얼은 에셋을 공유한다(지울 때 오브젝트만 지운다).
    /// </summary>
    internal static class BoardProps
    {
        public static GameObject Build(Transform parent, BoardCoordinates coordinates, BoardArt art)
        {
            BoardArt.PropPlacement[] placements = art != null ? art.Props : null;
            if (placements == null || placements.Length == 0)
                return null;

            float cell = coordinates.CellSize;
            Vector3 min = coordinates.Center - new Vector3(coordinates.Size.x, 0f, coordinates.Size.y) * 0.5f;
            var root = new GameObject("Props");
            root.transform.SetParent(parent, false);

            foreach (BoardArt.PropPlacement placement in placements)
            {
                if (placement.prefab == null)
                    continue;

                var position = new Vector3(
                    min.x + placement.anchor.x * coordinates.Size.x + placement.offset.x * cell,
                    BoardBackdrop.GroundHeight,
                    min.z + placement.anchor.y * coordinates.Size.y + placement.offset.y * cell);

                GameObject prop = Object.Instantiate(placement.prefab, root.transform);
                prop.transform.localPosition = position;
                prop.transform.localRotation = Quaternion.Euler(0f, placement.yaw, 0f);
                prop.transform.localScale = Vector3.one * (placement.scale > 0f ? placement.scale : 1f) * cell;
            }

            return root;
        }
    }
}
