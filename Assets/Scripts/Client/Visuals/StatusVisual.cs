using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 상태(상태이상/버프) 하나의 표시 데이터: 이름, 색, 아이콘. 상태 Id 로 코어 상태와 연결한다.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Status Visual", fileName = "StatusVisual")]
    public class StatusVisual : ScriptableObject
    {
        [SerializeField, CoreStatusId] private string statusId;

        [Tooltip("화면에 보일 이름(예: 화상).")]
        [SerializeField] private string displayName;

        [Tooltip("상태를 나타내는 색. 글자/아이콘 강조 등에 쓴다.")]
        [SerializeField] private Color color = Color.white;

        [Tooltip("상태 아이콘. 비우면 이름(글자)으로 표시한다.")]
        [SerializeField] private Sprite icon;

        public string StatusId => statusId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? statusId : displayName;
        public Color Color => color;
        public Sprite Icon => icon;
    }
}
