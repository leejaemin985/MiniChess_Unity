using System.Linq;
using MiniChess.Client.Visuals;
using MiniChess.Core.Characters;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 코어 캐릭터마다 CharacterVisual 에셋을 만들고 카탈로그(Resources/CharacterVisualCatalog)에 등록한다.
    /// 이미 있는 에셋/등록은 건드리지 않으므로, 캐릭터가 추가될 때마다 다시 실행하면 빠진 것만 채운다.
    /// 일러스트와 얼굴 영역은 만들어진 에셋에서 직접 지정한다.
    /// </summary>
    public static class CharacterVisualMenu
    {
        private const string VisualFolder = "Assets/Art/CharacterVisuals";
        private const string CatalogPath = "Assets/Resources/" + CharacterVisualCatalog.ResourcePath + ".asset";

        [MenuItem("MiniChess/Create Missing Character Visuals")]
        public static void CreateMissing()
        {
            EnsureFolder(VisualFolder);
            EnsureFolder("Assets/Resources");

            CharacterVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var catalogObject = new SerializedObject(catalog);
            SerializedProperty list = catalogObject.FindProperty("visuals");
            int created = 0;

            foreach (PieceModule piece in PieceModules.All)
            {
                if (catalog.Find(piece.Id) != null)
                    continue;

                string path = $"{VisualFolder}/{piece.Id}.asset";
                var visual = AssetDatabase.LoadAssetAtPath<CharacterVisual>(path);
                if (visual == null)
                {
                    visual = ScriptableObject.CreateInstance<CharacterVisual>();
                    AssetDatabase.CreateAsset(visual, path);

                    var visualObject = new SerializedObject(visual);
                    visualObject.FindProperty("characterId").stringValue = piece.Id;
                    visualObject.ApplyModifiedPropertiesWithoutUndo();
                    created++;
                }

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = visual;
            }

            catalogObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Selection.activeObject = catalog;
            Debug.Log($"[CharacterVisualMenu] 새 에셋 {created}개, 카탈로그 {catalog.Visuals.Count(v => v != null)}개 등록 ({CatalogPath})");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
