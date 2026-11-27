using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 유닛 몸체가 재생하는 동작. 이름이 공용 Animator 컨트롤러(AC_Unit)의 트리거/상태 이름과 같다.
    /// 캐릭터별 클립은 오버라이드 컨트롤러에서 슬롯 클립(Slot_{이름})을 바꿔 끼운다.
    /// </summary>
    public enum UnitAction
    {
        /// <summary>기본 공격(유닛/장애물).</summary>
        Attack,

        /// <summary>스킬 1번 칸.</summary>
        Skill1,

        /// <summary>스킬 2번 칸(그 밖의 추가 스킬은 Skill1).</summary>
        Skill2,

        /// <summary>막기 반응(호위로 피해를 대신 받을 때 등). 캐릭터가 슬롯 클립을 바꿔 끼웠을 때만 재생한다.</summary>
        Block,
    }

    public static class UnitActions
    {
        /// <summary>클립 애니메이션 이벤트 이름: 타격 시점(여러 개면 다단 히트). 코드는 이벤트 호출을 기다리지 않고 클립에서 시간을 미리 읽는다.</summary>
        public const string HitEvent = "Hit";

        /// <summary>클립 애니메이션 이벤트 이름: 투사체를 놓는 시점(원거리).</summary>
        public const string ReleaseEvent = "Release";

        /// <summary>클립 애니메이션 이벤트 이름: 스킬 이동(연속 베기 등)을 시작하는 시점.</summary>
        public const string MoveStartEvent = "MoveStart";

        /// <summary>클립 애니메이션 이벤트 이름: 스킬 이동이 착지 칸에 도착하는 시점.</summary>
        public const string LandEvent = "Land";

        private static readonly int[] TriggerIds =
        {
            Animator.StringToHash(nameof(UnitAction.Attack)),
            Animator.StringToHash(nameof(UnitAction.Skill1)),
            Animator.StringToHash(nameof(UnitAction.Skill2)),
            Animator.StringToHash(nameof(UnitAction.Block)),
        };

        private static readonly string[] SlotClipNames =
        {
            "Slot_" + nameof(UnitAction.Attack),
            "Slot_" + nameof(UnitAction.Skill1),
            "Slot_" + nameof(UnitAction.Skill2),
            "Slot_" + nameof(UnitAction.Block),
        };

        /// <summary>동작의 Animator 트리거 해시.</summary>
        public static int TriggerId(UnitAction action) => TriggerIds[(int)action];

        /// <summary>공용 컨트롤러에서 동작 상태가 쓰는 슬롯 클립 이름(오버라이드는 이 클립을 바꿔 끼운다).</summary>
        public static string SlotClipName(UnitAction action) => SlotClipNames[(int)action];
    }
}
