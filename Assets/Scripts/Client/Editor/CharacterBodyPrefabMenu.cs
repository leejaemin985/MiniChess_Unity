using System.Text.RegularExpressions;
using MiniChess.Client.Visuals;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 캐릭터 모델(Art/Characters/{이름}/Model/{이름}.fbx)마다 보드 위 몸체 프리팹(Prefabs/P_{이름}Body)을 만들고 CharacterVisual 의 bodyPrefab 에 넣는다.
    ///   - 루트 = 발밑 중앙(회전/크기 없음), 모델은 자식(FBX 프리팹 인스턴스)으로 두고 경계 상자 바닥 중앙이 루트에 오게 옮긴다.
    ///   - 앞 방향은 +z(Meshy 내보내기 기준). 팀별로 상대 진영을 보게 돌리는 것은 UnitView 가 한다.
    /// 폴더 이름(PascalCase)을 코어 캐릭터 Id(UPPER_SNAKE, 예: ChainGuard → CHAIN_GUARD)로 바꿔 CharacterVisual 을 찾는다.
    /// 모델을 바꾸거나(리깅된 모델 등) 캐릭터가 늘면 다시 실행한다. 프리팹은 덮어쓴다.
    /// </summary>
    public static class CharacterBodyPrefabMenu
    {
        private const string CharacterFolder = "Assets/Art/Characters";
        private const string VisualFolder = "Assets/Art/CharacterVisuals";

        [MenuItem("MiniChess/Build Character Body Prefabs")]
        public static void Build()
        {
            int built = 0, linked = 0;
            foreach (string folder in AssetDatabase.GetSubFolders(CharacterFolder))
            {
                string name = folder.Substring(folder.LastIndexOf('/') + 1);
                string modelPath = $"{folder}/Model/{name}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null)
                    continue;

                GameObject prefab = BuildPrefab(folder, name, model);
                built++;

                CharacterVisual visual = FindVisual(ToCharacterId(name));
                if (visual == null)
                {
                    Debug.LogWarning($"[CharacterBodyPrefabMenu] {name}: CharacterVisual({ToCharacterId(name)}) 이 없어 연결하지 못함");
                    continue;
                }

                var visualObject = new SerializedObject(visual);
                visualObject.FindProperty("bodyPrefab").objectReferenceValue = prefab;
                visualObject.ApplyModifiedPropertiesWithoutUndo();
                linked++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterBodyPrefabMenu] 몸체 프리팹 {built}개, CharacterVisual 연결 {linked}개");
        }

        private static GameObject BuildPrefab(string folder, string name, GameObject model)
        {
            string prefabFolder = $"{folder}/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
                AssetDatabase.CreateFolder(folder, "Prefabs");

            var root = new GameObject($"P_{name}Body");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                    bounds.Encapsulate(renderer.bounds);
                instance.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{prefabFolder}/P_{name}Body.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static CharacterVisual FindVisual(string characterId)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterVisual", new[] { VisualFolder }))
            {
                var visual = AssetDatabase.LoadAssetAtPath<CharacterVisual>(AssetDatabase.GUIDToAssetPath(guid));
                if (visual != null && visual.CharacterId == characterId)
                    return visual;
            }

            return null;
        }

        /// <summary>"ChainGuard" → "CHAIN_GUARD"</summary>
        private static string ToCharacterId(string folderName)
        {
            return Regex.Replace(folderName, "(?<=[a-z0-9])([A-Z])", "_$1").ToUpperInvariant();
        }
    }
}
