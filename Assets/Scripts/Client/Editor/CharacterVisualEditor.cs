using MiniChess.Client.Visuals;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// CharacterVisual 인스펙터. 일러스트 위에 얼굴 영역(노란 사각형)을 그리고, 잘라 낸 결과를 함께 보여준다.
    ///   일러스트를 클릭/드래그 → 얼굴 중심 이동, 일러스트 위에서 마우스 휠 → 얼굴 크기 조절.
    /// </summary>
    [CustomEditor(typeof(CharacterVisual))]
    public class CharacterVisualEditor : UnityEditor.Editor
    {
        private const float MaxPreviewWidth = 320f;
        private const float FacePreviewSize = 128f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var visual = (CharacterVisual)target;
            Sprite illustration = visual.Illustration;
            if (illustration == null)
            {
                EditorGUILayout.HelpBox("일러스트를 지정하면 얼굴 영역을 편집할 수 있습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("얼굴 영역", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("일러스트를 클릭/드래그해 얼굴 중심을 옮기고, 마우스 휠로 크기를 조절합니다.", MessageType.None);

            Texture2D texture = illustration.texture;
            Rect spriteRect = illustration.rect;

            float width = Mathf.Min(MaxPreviewWidth, EditorGUIUtility.currentViewWidth - 40f);
            float height = width * spriteRect.height / spriteRect.width;
            Rect area = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

            GUI.DrawTextureWithTexCoords(area, texture, ToUv(spriteRect, texture));

            Rect face = visual.GetFaceRect();
            Rect faceGui = new Rect(
                area.x + (face.x - spriteRect.x) / spriteRect.width * area.width,
                area.y + (1f - (face.yMax - spriteRect.y) / spriteRect.height) * area.height,
                face.width / spriteRect.width * area.width,
                face.height / spriteRect.height * area.height);
            Handles.DrawSolidRectangleWithOutline(faceGui, new Color(1f, 1f, 0f, 0.08f), Color.yellow);

            HandleInput(area);

            EditorGUILayout.Space();
            Rect facePreview = GUILayoutUtility.GetRect(FacePreviewSize, FacePreviewSize, GUILayout.ExpandWidth(false));
            GUI.DrawTextureWithTexCoords(facePreview, texture, ToUv(face, texture));
        }

        private void HandleInput(Rect area)
        {
            Event e = Event.current;
            if (!area.Contains(e.mousePosition))
                return;

            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                serializedObject.Update();
                serializedObject.FindProperty("faceCenter").vector2Value = new Vector2(
                    (e.mousePosition.x - area.x) / area.width,
                    1f - (e.mousePosition.y - area.y) / area.height);
                serializedObject.ApplyModifiedProperties();
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                serializedObject.Update();
                SerializedProperty size = serializedObject.FindProperty("faceSize");
                size.floatValue = Mathf.Clamp(size.floatValue - e.delta.y * 0.01f, 0.05f, 1f);
                serializedObject.ApplyModifiedProperties();
                e.Use();
                Repaint();
            }
        }

        private static Rect ToUv(Rect pixels, Texture texture)
        {
            return new Rect(pixels.x / texture.width, pixels.y / texture.height, pixels.width / texture.width, pixels.height / texture.height);
        }
    }
}
