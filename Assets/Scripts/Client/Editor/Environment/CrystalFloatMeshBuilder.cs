using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 큰 크리스탈(P_CrystalShard) 주위를 떠다니는 작은 파편들을 메시 하나로 합쳐 에셋으로 저장한다(Crystal 셰이더의 _FLOATING 으로 움직인다).
    /// 크리스탈 메시를 작게 줄여 크리스탈 둘레/위쪽에 흩어 놓고, 조각마다 중심(UV1.xyz)과 위상(UV1.w)을 넣는다.
    /// 좌표는 프리팹 루트 기준(바닥 중앙 = 원점, 크리스탈 높이 약 1.9)이라 프리팹 루트 아래에 회전/크기 없이 붙이면 된다.
    /// </summary>
    public static class CrystalFloatMeshBuilder
    {
        public const string MeshPath = "Assets/Art/Environment/Meshes/SM_CrystalFloat.asset";
        private const string CrystalPrefabPath = "Assets/Art/Environment/Prefabs/P_CrystalShard.prefab";
        private const int PieceCount = 5;

        [MenuItem("MiniChess/Environment/Build Crystal Float Mesh")]
        public static Mesh Build()
        {
            if (!TryGetCrystalMesh(out Mesh source, out Matrix4x4 toRoot, out Bounds crystal))
                return null;

            var random = new System.Random(731);
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var pieces = new List<Vector4>();
            var triangles = new List<int>();
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            int[] sourceTriangles = source.triangles;

            float height = crystal.size.y;
            float radius = Mathf.Max(crystal.extents.x, crystal.extents.z);
            for (int i = 0; i < PieceCount; i++)
            {
                // 둘레를 고르게 돌되 조금씩 흔들고, 높이는 크리스탈 중간~꼭대기 위
                float angle = (i / (float)PieceCount + Range(-0.06f, 0.06f)) * Mathf.PI * 2f;
                float distance = radius * Range(1.5f, 2.2f);
                var pivot = new Vector3(Mathf.Cos(angle) * distance, height * Range(0.5f, 1.2f), Mathf.Sin(angle) * distance);
                float scale = Range(0.07f, 0.13f);
                Quaternion tilt = Quaternion.Euler(Range(-35f, 35f), Range(0f, 360f), Range(-35f, 35f));
                Matrix4x4 place = Matrix4x4.TRS(pivot, tilt, Vector3.one * scale) * Matrix4x4.Translate(-crystal.center) * toRoot;
                float phase = Range(0f, Mathf.PI * 2f);

                int start = vertices.Count;
                for (int v = 0; v < sourceVertices.Length; v++)
                {
                    vertices.Add(place.MultiplyPoint3x4(sourceVertices[v]));
                    normals.Add(place.MultiplyVector(sourceNormals[v]).normalized);
                    pieces.Add(new Vector4(pivot.x, pivot.y, pivot.z, phase));
                }

                foreach (int index in sourceTriangles)
                    triangles.Add(start + index);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool created = mesh == null;
            if (created)
                mesh = new Mesh();
            mesh.Clear();
            mesh.name = "SM_CrystalFloat";
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(1, pieces);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            // 셰이더가 위아래로 흔들므로 그만큼 경계를 늘려 화면 밖으로 잘리지 않게
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0.2f, 0.6f, 0.2f));
            mesh.bounds = bounds;

            if (created)
                AssetDatabase.CreateAsset(mesh, MeshPath);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CrystalFloatMeshBuilder] {MeshPath}: 파편 {PieceCount}개, 삼각형 {triangles.Count / 3}");
            return mesh;
        }

        /// <summary>크리스탈 메시(읽을 수 있는 사본)와 메시 → 프리팹 루트 변환, 루트 기준 크리스탈 경계.</summary>
        private static bool TryGetCrystalMesh(out Mesh mesh, out Matrix4x4 toRoot, out Bounds bounds)
        {
            mesh = null;
            toRoot = Matrix4x4.identity;
            bounds = default;

            GameObject root = PrefabUtility.LoadPrefabContents(CrystalPrefabPath);
            try
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    // 빛 판(Quad)/파편 자신은 건너뛰고 크리스탈 몸체만
                    if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>().sharedMaterial.shader.name != "MiniChess/Crystal" || filter.name == "Floating")
                        continue;

                    // 가져온 FBX 메시는 읽기 끔 상태라 사본을 만들어 읽는다
                    mesh = Object.Instantiate(filter.sharedMesh);
                    toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Vector3[] vertices = mesh.vertices;
                    bounds = new Bounds(toRoot.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                    foreach (Vector3 v in vertices)
                        bounds.Encapsulate(toRoot.MultiplyPoint3x4(v));
                    return true;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.LogError($"[CrystalFloatMeshBuilder] {CrystalPrefabPath} 에서 크리스탈 메시를 찾지 못함");
            return false;
        }
    }
}
