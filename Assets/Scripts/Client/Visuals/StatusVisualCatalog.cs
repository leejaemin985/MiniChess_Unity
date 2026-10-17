using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 전체 상태 표시 데이터 목록. 상태 Id 로 찾는다. Resources/StatusVisualCatalog 에 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Status Visual Catalog", fileName = "StatusVisualCatalog")]
    public class StatusVisualCatalog : ScriptableObject
    {
        public const string ResourcePath = "StatusVisualCatalog";

        [SerializeField] private List<StatusVisual> visuals = new List<StatusVisual>();

        public IReadOnlyList<StatusVisual> Visuals => visuals;

        /// <summary>상태 Id 의 표시 데이터. 없으면 null.</summary>
        public StatusVisual Find(string statusId)
        {
            foreach (StatusVisual visual in visuals)
            {
                if (visual != null && visual.StatusId == statusId)
                    return visual;
            }

            return null;
        }
    }
}
