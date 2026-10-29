using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 외부에서 가져온 모델(Meshy 등 FBX + 텍스처)을 맵 소품으로 정리한다. 어디에 넣어 두었든 Environment 폴더 구조로 옮긴다:
    ///   Meshes/SM_{이름}.fbx, Textures/T_{이름}_{Albedo|Normal|Roughness}.png, Materials/M_{이름}.mat, Prefabs/P_{이름}.prefab.
    ///   (원본 "{파일}.png", "{파일}_normal.png", "{파일}_roughness.png" 를 찾아 옮긴다. 옮긴 뒤 빈 폴더는 지운다.)
    ///   - 텍스처: 용도에 맞게 설정(1024, 압축). 메탈릭 텍스처("{파일}_metallic.png")는 쓰지 않으므로 지운다(돌/잎은 금속이 아님).
    ///   - 모델: FBX 가 만든 머티리얼은 쓰지 않는다. 회전/스케일(AI 생성 FBX 의 -90도, 100배)은 FBX 자식에 남기고,
    ///     프리팹 루트는 회전/스케일 없이 바닥 중앙이 원점이 되게 한다.
    ///   - 머티리얼: PropSurface 셰이더(맵 색감으로 보정). 이미 있으면 텍스처만 다시 연결하고 조정한 값은 그대로 둔다.
    /// </summary>
    public static class PropImporter
    {
        private const string Root = "Assets/Art/Environment";
        private const string MeshFolder = Root + "/Meshes";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialFolder = Root + "/Materials";
        public const string PrefabFolder = Root + "/Prefabs";
        private const int TextureSize = 1024;

        [MenuItem("MiniChess/Environment/Prepare Prop From Model")]
        public static void PrepareSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[PropImporter] Project 창에서 FBX 모델을 선택하세요");
                return;
            }

            Prepare(path, NameOf(path));
        }

        [MenuItem("MiniChess/Environment/Prepare Prop From Model", true)]
        private static bool CanPrepareSelected()
        {
            return AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>"Meshy_AI_Ivy_Crowned_Ruin_Arches_20261029_texture" → "IvyCrownedRuinArches"</summary>
        private static string NameOf(string modelPath)
        {
            string name = Path.GetFileNameWithoutExtension(modelPath);
            name = Regex.Replace(name, "^Meshy_AI_", "");
            name = Regex.Replace(name, "_\\d{8}.*$", "");
            return name.Replace("_", "");
        }

        public static GameObject Prepare(string modelPath, string propName)
        {
            foreach (string folder in new[] { MeshFolder, TextureFolder, MaterialFolder, PrefabFolder })
                EnvironmentAssetUtil.EnsureFolder(folder);

            // 원본 파일들을 폴더 구조로 옮긴다(GUID 유지라 이미 만든 머티리얼/프리팹 연결은 그대로)
            string sourceFolder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            string sourceName = Path.GetFileNameWithoutExtension(modelPath);
            modelPath = MoveTo(modelPath, $"{MeshFolder}/SM_{propName}.fbx");
            string albedoPath = MoveTo($"{sourceFolder}/{sourceName}.png", $"{TextureFolder}/T_{propName}_Albedo.png");
            string normalPath = MoveTo($"{sourceFolder}/{sourceName}_normal.png", $"{TextureFolder}/T_{propName}_Normal.png");
            string roughnessPath = MoveTo($"{sourceFolder}/{sourceName}_roughness.png", $"{TextureFolder}/T_{propName}_Roughness.png");
            AssetDatabase.DeleteAsset($"{sourceFolder}/{sourceName}_metallic.png"); // 돌/잎은 금속이 아니라 쓰지 않는다
            DeleteEmptyFolders(sourceFolder);

            Texture2D albedo = ConfigureTexture(albedoPath, TextureImporterType.Default, srgb: true);
            Texture2D normal = ConfigureTexture(normalPath, TextureImporterType.NormalMap, srgb: false);
            Texture2D roughness = ConfigureTexture(roughnessPath, TextureImporterType.Default, srgb: false);

            var model = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (model.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                model.materialImportMode = ModelImporterMaterialImportMode.None;
                model.SaveAndReimport();
            }

            Material material = CreateMaterial(propName, albedo, normal, roughness);
            GameObject prefab = CreatePrefab(modelPath, propName, material);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PropImporter] {propName}: {modelPath}, {MaterialFolder}/M_{propName}.mat, {PrefabFolder}/P_{propName}.prefab");
            return prefab;
        }

        /// <summary>파일이 있으면 to 로 옮기고 새 경로를, 없으면 to(이미 옮겨져 있을 수 있음)를 돌려준다.</summary>
        private static string MoveTo(string from, string to)
        {
            if (from == to || AssetImporter.GetAtPath(from) == null)
                return to;

            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"[PropImporter] {from} → {to} 이동 실패: {error}");
            return to;
        }

        /// <summary>옮기고 남은 빈 폴더를 아래에서부터 지운다(Environment 폴더 밖의 원본 폴더만).</summary>
        private static void DeleteEmptyFolders(string folder)
        {
            while (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/") && !Root.StartsWith(folder) && AssetDatabase.IsValidFolder(folder))
            {
                // .meta 말고 다른 파일/폴더가 남아 있으면 멈춘다
                foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
                {
                    if (!entry.EndsWith(".meta"))
                        return;
                }

                AssetDatabase.DeleteAsset(folder);
                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }
        }

        private static Texture2D ConfigureTexture(string path, TextureImporterType type, bool srgb)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return null;

            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = TextureSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateMaterial(string propName, Texture2D albedo, Texture2D normal, Texture2D roughness)
        {
            string path = $"{MaterialFolder}/M_{propName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("MiniChess/PropSurface"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_NormalMap", normal);
            material.SetTexture("_RoughnessMap", roughness);
            material.SetTexture("_MossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(StoneTextureBaker.MaskPath));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>바닥 중앙이 원점인 루트 아래에 FBX 를 자식으로 둔 프리팹(FBX 를 다시 가져오면 따라 바뀐다).</summary>
        private static GameObject CreatePrefab(string modelPath, string propName, Material material)
        {
            var root = new GameObject($"P_{propName}");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            model.transform.SetParent(root.transform, false);

            Bounds bounds = default;
            bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;

                if (first)
                    bounds = renderer.bounds;
                else
                    bounds.Encapsulate(renderer.bounds);
                first = false;
            }

            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            string path = $"{PrefabFolder}/P_{propName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
