using System.Collections.Generic;
using MiniChess.Client.Board;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 토큰 보석(팔각 마름모) 메시를 에셋으로 저장한다(TokensView 가 Resources 에서 불러 쓴다).
    /// 모양은 시안 3면도(정면/측면/윗면) 비율: 윗면은 네 모서리를 깎은 정사각형(팔각형), 위아래로 꼭짓점이 뾰족하게 모인다.
    ///   - 폭 1(가로·세로 같음), 높이 Height. 중심 = 원점(적도 높이).
    ///   - 정면에서 큰 삼각형 면(긴 변)이 앞을 보고, 양옆에 깎인 모서리 면이 좁게 보인다. 측면도 같다.
    /// 면마다 법선을 따로 둬 각진 면이 보이게 한다.
    /// </summary>
    public static class TokenGemMeshBuilder
    {
        public const string MeshPath = "Assets/Resources/" + TokensView.GemMeshPath + ".asset";

        /// <summary>높이 / 폭(시안 정면 524 / 360).</summary>
        private const float Height = 1.46f;

        /// <summary>윗면에서 모서리를 깎는 길이 / 폭(시안 윗면 60 / 364).</summary>
        private const float Chamfer = 0.165f;

        [MenuItem("MiniChess/Build/Token Gem Mesh")]
        public static Mesh Build()
        {
            // 적도 팔각형: +X 쪽 긴 변부터 반시계(위에서 볼 때)로.
            const float half = 0.5f;
            const float inner = half - Chamfer;
            Vector3[] ring =
            {
                new Vector3(half, 0f, -inner), new Vector3(half, 0f, inner),
                new Vector3(inner, 0f, half), new Vector3(-inner, 0f, half),
                new Vector3(-half, 0f, inner), new Vector3(-half, 0f, -inner),
                new Vector3(-inner, 0f, -half), new Vector3(inner, 0f, -half),
            };
            var top = new Vector3(0f, Height * 0.5f, 0f);
            var bottom = new Vector3(0f, -Height * 0.5f, 0f);

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < ring.Length; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % ring.Length];
                // 바깥에서 보아 시계 방향(Unity 앞면).
                AddTriangle(vertices, triangles, top, b, a);
                AddTriangle(vertices, triangles, bottom, a, b);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool created = mesh == null;
            if (created)
                mesh = new Mesh();
            mesh.Clear();
            mesh.name = "SM_TokenGem";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            if (created)
                AssetDatabase.CreateAsset(mesh, MeshPath);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TokenGemMeshBuilder] {MeshPath}: 삼각형 {triangles.Count / 3}, 정점 {vertices.Count}");
            return mesh;
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            triangles.Add(vertices.Count);
            vertices.Add(a);
            triangles.Add(vertices.Count);
            vertices.Add(b);
            triangles.Add(vertices.Count);
            vertices.Add(c);
        }
    }
}
