using UnityEngine;

namespace MiniChess.Client.Bootstrap
{
    /// <summary>
    /// Inspector 에서 팀 구성을 고르기 위한 목록. 이름이 코어 CharacterRoster 의 Id 와 같아야 한다.
    /// InspectorName 은 Inspector 드롭다운에 보이는 이름이다.
    /// </summary>
    public enum CharacterChoice
    {
        [InspectorName("SCYTHE - 낫 / 그림자 분신 전투원")]
        SCYTHE,         // 낫 / 그림자 분신 전투원

        [InspectorName("MORTAR - 마법공학 박격포 여학생")]
        MORTAR,         // 마법공학 박격포 여학생

        [InspectorName("GARDENER - 원예부 선배 수호자")]
        GARDENER,       // 원예부 선배 수호자

        [InspectorName("WARRIOR - 대검 전사")]
        WARRIOR,        // 대검 전사

        [InspectorName("ARCHER - 궁수 / 볼라 사냥꾼")]
        ARCHER,         // 궁수 / 볼라 사냥꾼

        [InspectorName("CHEMIST - 화학공학 덫 전문가")]
        CHEMIST,        // 화학공학 덫 전문가

        [InspectorName("FLAME - 소형 중화기 화염 딜러")]
        FLAME,          // 소형 중화기 화염 딜러

        [InspectorName("SEAMSTRESS - 공간 재봉사")]
        SEAMSTRESS,     // 공간 재봉사

        [InspectorName("CHAIN_GUARD - 사슬 수호기사")]
        CHAIN_GUARD,    // 사슬 수호기사
    }
}
