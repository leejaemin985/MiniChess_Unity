using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MiniChess.Client.Visuals;
using MiniChess.Core.Statuses.Library;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 상태마다 StatusVisual 에셋을 만들고 카탈로그(Resources/StatusVisualCatalog)에 등록한다.
    /// 이미 있는 에셋/등록은 건드리지 않으므로, 상태가 추가될 때마다 다시 실행하면 빠진 것만 채운다.
    /// 새로 만들 때 아래 기본 이름/색을 넣고, 아이콘은 만들어진 에셋에서 직접 지정한다.
    /// </summary>
    public static class StatusVisualMenu
    {
        private const string VisualFolder = "Assets/Art/StatusVisuals";
        private const string CatalogPath = "Assets/Resources/" + StatusVisualCatalog.ResourcePath + ".asset";

        /// <summary>새 에셋의 기본 이름과 색. 목록에 없는 상태는 Id 와 흰색으로 만든다.</summary>
        private static readonly Dictionary<string, (string Name, Color Color)> Defaults = new Dictionary<string, (string, Color)>
        {
            { StatusLibrary.RootId, ("속박", new Color(0.55f, 0.8f, 0.35f)) },
            { StatusLibrary.BurnId, ("화상", new Color(1f, 0.5f, 0.2f)) },
            { StatusLibrary.PoisonId, ("중독", new Color(0.7f, 0.4f, 0.95f)) },
            { StatusLibrary.DistanceLimitId, ("이동 제한", new Color(0.6f, 0.7f, 0.85f)) },
            { StatusLibrary.GuardId, ("호위", new Color(0.4f, 0.65f, 1f)) },
            { StatusLibrary.InstalledId, ("설치", new Color(0.85f, 0.75f, 0.45f)) },
            { StatusLibrary.CurseMarkId, ("저주 표식", new Color(0.85f, 0.3f, 0.5f)) },
            { StatusLibrary.ChargedId, ("차징", new Color(1f, 0.85f, 0.25f)) },
            { StatusLibrary.SealedId, ("봉인", new Color(0.55f, 0.9f, 0.95f)) },
            { StatusLibrary.StunnedId, ("기절", new Color(1f, 0.95f, 0.4f)) },
        };

        /// <summary>표시할 수 있는 상태 Id: 코어 StatusLibrary 의 "...Id" 문자열 상수.</summary>
        public static List<string> GetStatusIds()
        {
            return typeof(StatusLibrary)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("Id"))
                .Select(field => (string)field.GetRawConstantValue())
                .ToList();
        }

        [MenuItem("MiniChess/Create Missing Status Visuals")]
        public static void CreateMissing()
        {
            EnsureFolder(VisualFolder);
            EnsureFolder("Assets/Resources");

            StatusVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<StatusVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StatusVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var catalogObject = new SerializedObject(catalog);
            SerializedProperty list = catalogObject.FindProperty("visuals");
            int created = 0;

            foreach (string id in GetStatusIds())
            {
                if (catalog.Find(id) != null)
                    continue;

                string path = $"{VisualFolder}/{id}.asset";
                var visual = AssetDatabase.LoadAssetAtPath<StatusVisual>(path);
                if (visual == null)
                {
                    visual = ScriptableObject.CreateInstance<StatusVisual>();
                    AssetDatabase.CreateAsset(visual, path);

                    (string name, Color color) = Defaults.TryGetValue(id, out var preset) ? preset : (id, Color.white);
                    var visualObject = new SerializedObject(visual);
                    visualObject.FindProperty("statusId").stringValue = id;
                    visualObject.FindProperty("displayName").stringValue = name;
                    visualObject.FindProperty("color").colorValue = color;
                    visualObject.ApplyModifiedPropertiesWithoutUndo();
                    created++;
                }

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = visual;
            }

            catalogObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Selection.activeObject = catalog;
            Debug.Log($"[StatusVisualMenu] 새 에셋 {created}개, 카탈로그 {catalog.Visuals.Count(v => v != null)}개 등록 ({CatalogPath})");
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
