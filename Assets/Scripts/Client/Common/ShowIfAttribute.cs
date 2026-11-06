using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>
    /// 같은 객체의 다른 필드(열거형/정수/bool)가 지정한 값일 때만 인스펙터에 이 필드를 보인다(에디터 전용 표시, 값은 숨겨도 유지된다).
    /// 한 필드에는 속성 그리기가 하나만 쓰이므로 [Range] 와 함께 쓰지 말고, 정수 슬라이더가 필요하면 min/max 를 준다.
    /// </summary>
    public class ShowIfAttribute : PropertyAttribute
    {
        /// <summary>조건 필드 이름(같은 객체 안).</summary>
        public string Field { get; }

        /// <summary>보일 때의 조건 필드 값(열거형은 그 값, bool 은 true/false).</summary>
        public object Value { get; }

        public bool HasSlider { get; }
        public int Min { get; }
        public int Max { get; }

        public ShowIfAttribute(string field, object value)
        {
            Field = field;
            Value = value;
        }

        /// <summary>정수 필드를 min~max 슬라이더로 그린다.</summary>
        public ShowIfAttribute(string field, object value, int min, int max)
            : this(field, value)
        {
            HasSlider = true;
            Min = min;
            Max = max;
        }
    }
}
