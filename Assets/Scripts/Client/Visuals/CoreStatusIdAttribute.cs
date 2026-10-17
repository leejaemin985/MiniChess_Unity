using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 문자열 필드를 상태 Id 드롭다운으로 보이게 한다(에디터 전용 표시, 오타 방지).
    /// 목록은 코어 StatusLibrary 의 Id 상수이며, 상태가 추가되면 자동으로 늘어난다.
    /// </summary>
    public class CoreStatusIdAttribute : PropertyAttribute
    {
    }
}
