using System;
using MiniChess.Client.Common;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>[ShowIf] 필드를 조건 필드 값에 따라 그리거나 숨긴다. 조건 필드를 바꾸면 바로 다시 그려진다(컴파일 불필요).</summary>
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 숨길 때는 줄 간격까지 없애 빈칸이 남지 않게 한다.
            return IsVisible(property) ? EditorGUI.GetPropertyHeight(property, label, true) : -EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!IsVisible(property))
                return;

            var showIf = (ShowIfAttribute)attribute;
            if (showIf.HasSlider && property.propertyType == SerializedPropertyType.Integer)
            {
                EditorGUI.BeginProperty(position, label, property);
                property.intValue = EditorGUI.IntSlider(position, label, property.intValue, showIf.Min, showIf.Max);
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.PropertyField(position, property, label, true);
        }

        private bool IsVisible(SerializedProperty property)
        {
            var showIf = (ShowIfAttribute)attribute;
            SerializedProperty condition = FindSibling(property, showIf.Field);
            if (condition == null)
                return true; // 조건 필드를 못 찾으면 숨기지 않는다(설정 실수로 값이 사라져 보이지 않게).

            switch (condition.propertyType)
            {
                case SerializedPropertyType.Enum:
                    return condition.intValue == Convert.ToInt32(showIf.Value);
                case SerializedPropertyType.Integer:
                    return condition.intValue == Convert.ToInt32(showIf.Value);
                case SerializedPropertyType.Boolean:
                    return condition.boolValue == Convert.ToBoolean(showIf.Value);
                default:
                    return true;
            }
        }

        private static SerializedProperty FindSibling(SerializedProperty property, string name)
        {
            string path = property.propertyPath;
            int dot = path.LastIndexOf('.');
            string siblingPath = dot < 0 ? name : path.Substring(0, dot + 1) + name;
            return property.serializedObject.FindProperty(siblingPath);
        }
    }
}
