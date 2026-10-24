using MiniChess.Client.Visuals.Effects;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 상태(상태이상/버프) 하나의 표시 데이터: 이름, 색, 아이콘, 몸체 머티리얼(굳기/깨지기 시간), 걸릴 때/풀릴 때/반응 이펙트. 상태 Id 로 코어 상태와 연결한다.
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

        [Tooltip("이 상태에 걸린 동안 유닛 몸체 전체에 씌울 머티리얼(예: 봉인 → 마법석). 비우면 몸체를 바꾸지 않는다.")]
        [SerializeField] private Material bodyMaterial;

        [Tooltip("몸체 머티리얼이 아래에서 위로 차올라 굳는 시간(초). 0 이면 바로 바뀐다. 굳기/깨지기 연출을 지원하는 머티리얼(마법석 셰이더)만.")]
        [SerializeField, Min(0f)] private float bodyApplyTime;

        [Tooltip("상태가 풀릴 때 몸체 머티리얼이 금 가며 조각나 사라지는 시간(초). 0 이면 바로 돌아온다.")]
        [SerializeField, Min(0f)] private float bodyRemoveTime;

        [Tooltip("상태가 새로 걸릴 때 걸린 유닛 자리에서 한 번 재생할 이펙트(예: 봉인 → 마법석 문양과 돌가루). 비우면 재생하지 않는다.")]
        [SerializeField] private CastEffect applyEffect;

        [Tooltip("상태가 풀릴 때 걸린 유닛 자리에서 한 번 재생할 이펙트(예: 봉인 → 마법석 파편). 비우면 재생하지 않는다.")]
        [SerializeField] private CastEffect removeEffect;

        [Tooltip("상태가 반응할 때 재생할 이펙트(예: 호위 → 보호 대상이 맞을 피해를 호위자가 대신 받음). 상태가 걸린 유닛 → 상태를 건 유닛으로 재생한다. 비우면 재생하지 않는다.")]
        [SerializeField] private LineEffect reactionEffect;

        public string StatusId => statusId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? statusId : displayName;
        public Color Color => color;
        public Sprite Icon => icon;

        /// <summary>몸체 교체 머티리얼. 지정하지 않았으면 null.</summary>
        public Material BodyMaterial => bodyMaterial;

        /// <summary>몸체 머티리얼이 굳어 차오르는 시간(초). 0 이면 바로.</summary>
        public float BodyApplyTime => bodyApplyTime;

        /// <summary>몸체 머티리얼이 깨져 사라지는 시간(초). 0 이면 바로.</summary>
        public float BodyRemoveTime => bodyRemoveTime;

        /// <summary>상태가 새로 걸릴 때 이펙트. 지정하지 않았으면 null.</summary>
        public CastEffect ApplyEffect => applyEffect;

        /// <summary>상태가 풀릴 때 이펙트. 지정하지 않았으면 null.</summary>
        public CastEffect RemoveEffect => removeEffect;

        /// <summary>상태 반응 이펙트(걸린 유닛 → 건 유닛). 지정하지 않았으면 null.</summary>
        public LineEffect ReactionEffect => reactionEffect;
    }
}
