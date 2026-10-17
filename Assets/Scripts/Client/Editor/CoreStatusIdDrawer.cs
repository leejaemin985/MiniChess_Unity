using System.Collections.Generic;
using MiniChess.Client.Visuals;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>[CoreStatusId] 문자열 필드를 상태 Id 드롭다운으로 그린다.</summary>
    [CustomPropertyDrawer(typeof(CoreStatusIdAttribute))]
    public class CoreStatusIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var ids = new List<string> { string.Empty };
            ids.AddRange(StatusVisualMenu.GetStatusIds());

            var options = new List<GUIContent> { new GUIContent("(없음)") };
            for (int i = 1; i < ids.Count; i++)
                options.Add(new GUIContent(ids[i]));

            string current = property.stringValue ?? string.Empty;
            int index = ids.IndexOf(current);
            if (index < 0)
            {
                // 코어에서 사라진 Id 도 값은 보존해 보여준다.
                ids.Add(current);
                options.Add(new GUIContent($"{current} (코어에 없음)"));
                index = ids.Count - 1;
            }

            EditorGUI.BeginProperty(position, label, property);
            int selected = EditorGUI.Popup(position, label, index, options.ToArray());
            if (selected != index)
                property.stringValue = ids[selected];
            EditorGUI.EndProperty();
        }
    }
}
