using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 캐릭터 모델과 무기(Assets/Art/Characters/{이름}/Model/*.fbx, Weapon/*.fbx)를 임포트할 때 툰 외곽선용 평균 법선을 UV7 에 구워 넣는다(CharacterToon 셰이더의 Outline 패스가 쓴다).
    ///   - 같은 위치의 정점들(UV/법선 경계에서 갈라진 정점)의 법선을 평균해, 외곽선을 부풀려도 경계에서 선이 끊기지 않게 한다.
    ///   - 탄젠트 공간(정점의 탄젠트, 바이탄젠트, 법선 기준)으로 저장해 스킨 애니메이션으로 법선/탄젠트가 돌면 같이 돈다.
    ///     기존 법선/탄젠트는 건드리지 않으므로 노멀맵을 쓰는 다른 머티리얼(URP Lit)에는 영향이 없다.
    /// </summary>
    public class CharacterOutlineNormals : AssetPostprocessor
    {
        public const int UvChannel = 7;
        private const string CharacterFolder = "Assets/Art/Characters/";

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(CharacterFolder) || !(assetPath.Contains("/Model/") || assetPath.Contains("/Weapon/")))
                return;

            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Bake(skinned.sharedMesh);
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                Bake(filter.sharedMesh);
        }

        /// <summary>mesh 의 UV7 에 같은 위치 정점들의 평균 법선을 탄젠트 공간으로 넣는다.</summary>
        public static void Bake(Mesh mesh)
        {
            if (mesh == null)
                return;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals.Length != vertices.Length)
                return;

            Vector4[] tangents = mesh.tangents;
            if (tangents.Length != vertices.Length)
            {
                mesh.RecalculateTangents();
                tangents = mesh.tangents;
            }

            // 위치를 메시 크기 대비 1/100000 단위로 묶는다(부동소수 오차로 같은 점이 갈라지지 않게).
            float quantize = 100000f / Mathf.Max(mesh.bounds.size.magnitude, 0.0001f);
            var sums = new Dictionary<Vector3Int, Vector3>();
            var keys = new Vector3Int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i] * quantize;
                keys[i] = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                sums.TryGetValue(keys[i], out Vector3 sum);
                sums[keys[i]] = sum + normals[i];
            }

            var smoothTangentSpace = new List<Vector3>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 smooth = sums[keys[i]].normalized;
                Vector3 normal = normals[i];
                Vector3 tangent = tangents[i];
                Vector3 bitangent = Vector3.Cross(normal, tangent) * tangents[i].w;
                smoothTangentSpace.Add(new Vector3(Vector3.Dot(smooth, tangent), Vector3.Dot(smooth, bitangent), Vector3.Dot(smooth, normal)));
            }

            mesh.SetUVs(UvChannel, smoothTangentSpace);
        }
    }
}
