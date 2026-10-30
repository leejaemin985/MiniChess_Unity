using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// PropImporter 로 정리한 매달린 천(깃발/현수막) 소품을 BannerCloth 셰이더로 흔들리게 바꾼다.
    ///   - 메시: FBX 의 회전/스케일(AI 생성 FBX 의 -90도, 100배)을 정점에 반영해 Meshes/SM_{이름}_Cloth.asset 로 저장한다.
    ///     원점 = 천 윗변 중앙(매다는 곳), 천은 -Y 로 늘어지고 앞뒤가 ±Z(셰이더가 원점에서 아래로 내려간 만큼 흔든다).
    ///   - 프리팹: P_{이름} 을 루트(= 매다는 곳) 아래 Cloth 자식 하나로 다시 만든다(움직이므로 Static/라이트맵 제외).
    ///   - 머티리얼: M_{이름} 의 셰이더를 BannerCloth 로 바꾸고 천 길이(_Length)를 넣는다. 조정한 흔들림 값은 그대로 둔다.
    /// </summary>
    public static class BannerClothBuilder
    {
        private const string Root = "Assets/Art/Environment";

        [MenuItem("MiniChess/Environment/Build Banner Cloth From Prop Prefab")]
        public static void BuildSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!path.StartsWith(PropImporter.PrefabFolder) || !name.StartsWith("P_"))
            {
                Debug.LogWarning("[BannerClothBuilder] Project 창에서 PropImporter 로 만든 P_ 프리팹을 선택하세요");
                return;
            }

            Build(name.Substring(2));
        }

        public static GameObject Build(string propName)
        {
            string prefabPath = $"{PropImporter.PrefabFolder}/P_{propName}.prefab";
            string meshPath = $"{Root}/Meshes/SM_{propName}_Cloth.asset";
            string materialPath = $"{Root}/Materials/M_{propName}.mat";

            Mesh cloth = BakeMesh(prefabPath, meshPath, out float length);
            if (cloth == null)
                return null;

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material.shader.name != "MiniChess/BannerCloth")
                material.shader = Shader.Find("MiniChess/BannerCloth"); // 텍스처 이름이 같아 그대로 이어진다
            material.SetFloat("_Length", length);
            EditorUtility.SetDirty(material);

            var root = new GameObject($"P_{propName}");
            var child = new GameObject("Cloth");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = cloth;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BannerClothBuilder] {propName}: {meshPath}(삼각형 {cloth.triangles.Length / 3}, 길이 {length:F2}), {prefabPath}");
            return prefab;
        }

        /// <summary>프리팹 안 FBX 메시를 루트 기준으로 옮기고, 윗변 중앙이 원점이 되게 해 에셋으로 저장한다.</summary>
        private static Mesh BakeMesh(string prefabPath, string meshPath, out float length)
        {
            length = 0;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var filter = root.GetComponentInChildren<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    Debug.LogError($"[BannerClothBuilder] {prefabPath} 에 메시가 없음");
                    return null;
                }

                // 가져온 FBX 메시는 읽기 끔 상태라 사본을 만들어 읽는다
                Mesh source = Object.Instantiate(filter.sharedMesh);
                Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = source.vertices;
                Vector3[] normals = source.normals;
                Vector4[] tangents = source.tangents;

                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = toRoot.MultiplyPoint3x4(vertices[i]);

                var bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 v in vertices)
                    bounds.Encapsulate(v);
                var anchor = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                length = bounds.size.y;

                Matrix4x4 normalMatrix = toRoot.inverse.transpose;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] -= anchor;
                    normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                    if (tangents.Length > 0)
                    {
                        Vector3 tangent = toRoot.MultiplyVector(tangents[i]).normalized;
                        tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                    }
                }

                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                bool created = mesh == null;
                if (created)
                    mesh = new Mesh();
                mesh.Clear();
                mesh.name = System.IO.Path.GetFileNameWithoutExtension(meshPath);
                mesh.vertices = vertices;
                mesh.normals = normals;
                if (tangents.Length > 0)
                    mesh.tangents = tangents;
                mesh.uv = source.uv;
                mesh.triangles = source.triangles;
                mesh.RecalculateBounds();
                // 셰이더가 앞뒤/좌우로 흔들므로 그만큼 경계를 늘려 화면 밖으로 잘리지 않게
                Bounds meshBounds = mesh.bounds;
                meshBounds.Expand(new Vector3(length * 0.3f, 0, length * 0.6f));
                mesh.bounds = meshBounds;
                Object.DestroyImmediate(source);

                if (created)
                    AssetDatabase.CreateAsset(mesh, meshPath);
                EditorUtility.SetDirty(mesh);
                return mesh;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
