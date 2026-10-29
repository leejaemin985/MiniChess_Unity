using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 보드 장식 메시를 만들어 에셋으로 저장한다. 모두 원점(뿌리/바닥)이 y = 0 이고 크기는 칸 크기 1 기준이다.
    ///   - 풀 덤불(Foliage 셰이더): 가늘고 끝이 뾰족한 잎 여러 장. 정점 색 RGB = 잎 색(뿌리 어둡게 → 끝 밝게), A = 뿌리 0 → 끝 1(바람).
    ///   - 돌조각 무더기(StoneSurface 셰이더): 불규칙한 작은 돌 몇 개(StoneMeshKit). 일부는 비스듬히 박혀 있다.
    /// BoardDecorator 가 줄눈/벽 둘레에 흩뿌린 뒤 한 메시로 합쳐 그린다.
    /// </summary>
    public static class FoliageMeshBuilder
    {
        public const int GrassVariantCount = 4;
        public const int RubbleVariantCount = 4;

        public static string GrassPathOf(int variant) => $"{StoneSlabMeshBuilder.Folder}/SM_Grass_{variant}.asset";
        public static string RubblePathOf(int variant) => $"{StoneSlabMeshBuilder.Folder}/SM_Rubble_{variant}.asset";

        // 시안의 풀: 어두운 올리브 뿌리 → 노란 기가 도는 연두 끝
        private static readonly Color RootColor = new Color(0.07f, 0.12f, 0.04f);
        private static readonly Color TipColor = new Color(0.30f, 0.42f, 0.12f);
        private static readonly Color TipColorDry = new Color(0.44f, 0.44f, 0.17f);

        [MenuItem("MiniChess/Environment/Build Foliage And Rubble Meshes")]
        public static void BuildAll()
        {
            EnvironmentAssetUtil.EnsureFolder(StoneSlabMeshBuilder.Folder);
            for (int variant = 0; variant < GrassVariantCount; variant++)
                StoneSlabMeshBuilder.Save(Grass(new System.Random(400 + variant), variant), GrassPathOf(variant));
            for (int variant = 0; variant < RubbleVariantCount; variant++)
                StoneSlabMeshBuilder.Save(Rubble(new System.Random(500 + variant), variant), RubblePathOf(variant));

            AssetDatabase.SaveAssets();
            Debug.Log($"[FoliageMeshBuilder] {StoneSlabMeshBuilder.Folder} 에 풀 {GrassVariantCount}종, 돌조각 {RubbleVariantCount}종을 만들었습니다");
        }

        /// <summary>풀 덤불: 변형마다 잎 수/높이가 다르다(0 = 짧고 촘촘, 3 = 길고 성김).</summary>
        private static Mesh Grass(System.Random random, int variant)
        {
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            int blades = 16 + variant * 3;
            float maxHeight = 0.07f + variant * 0.02f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (int b = 0; b < blades; b++)
            {
                float angle = Range(0f, Mathf.PI * 2f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-outward.z, 0f, outward.x);
                Vector3 root = outward * Range(0f, 0.04f);
                float height = maxHeight * Range(0.5f, 1f);
                float width = Range(0.008f, 0.016f);
                float lean = Range(0.15f, 0.6f);                        // 바깥으로 눕는 정도
                Color tip = Color.Lerp(TipColor, TipColorDry, Range(0f, 0.6f)) * Range(0.85f, 1.1f);
                var faceNormal = Vector3.Cross(side, Vector3.up + outward * lean).normalized;
                Vector3 normal = (Vector3.up * 0.6f + faceNormal * 0.4f).normalized;

                // 잎 하나: 뿌리 2점 → 가운데 2점 → 끝 1점(바깥으로 휘어지며 가늘어짐)
                int start = vertices.Count;
                for (int row = 0; row < 3; row++)
                {
                    float t = row / 2f;
                    Vector3 center = root + Vector3.up * (height * t) + outward * (height * lean * t * t);
                    float half = width * (1f - t) * 0.5f;
                    if (row < 2)
                    {
                        vertices.Add(center - side * half);
                        vertices.Add(center + side * half);
                        normals.Add(normal);
                        normals.Add(normal);
                        Color c = Color.Lerp(RootColor, tip, t);
                        c.a = t;
                        colors.Add(c);
                        colors.Add(c);
                    }
                    else
                    {
                        vertices.Add(center);
                        normals.Add(normal);
                        Color c = tip;
                        c.a = 1f;
                        colors.Add(c);
                    }
                }

                triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3, start + 2, start + 4, start + 3 });
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>돌조각 무더기: 작은 불규칙 돌 3~6개(변형 3 은 큰 벽돌 조각 하나 + 부스러기).</summary>
        private static Mesh Rubble(System.Random random, int variant)
        {
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            var kit = new StoneMeshKit();
            int count = 3 + variant;

            for (int i = 0; i < count; i++)
            {
                bool big = variant == 3 && i == 0;
                float radius = big ? Range(0.05f, 0.07f) : Range(0.015f, 0.04f);
                float height = big ? Range(0.04f, 0.06f) : radius * Range(0.6f, 1.1f);
                Vector2 center = i == 0 ? Vector2.zero : new Vector2(Range(-0.07f, 0.07f), Range(-0.07f, 0.07f));

                kit.Add(new StoneMeshKit.Piece
                {
                    Outline = Pebble(random, center, radius, big ? 4 : 5 + random.Next(3)),
                    Top = height,
                    Bottom = -0.02f,
                    Bevel = Mathf.Min(0.012f, height * 0.3f),
                    Rotation = Quaternion.Euler(Range(-25f, 25f), Range(0f, 360f), Range(-25f, 25f)),
                    UvOffset = new Vector2(Range(0f, 5f), Range(0f, 5f)),
                    Brightness = Range(0.6f, 0.95f),
                    SideDirt = 0.5f,
                });
            }

            return kit.ToMesh($"SM_Rubble_{variant}");
        }

        /// <summary>둥글게 둘러선 점들(반시계)로 만든 불규칙 다각형. 반지름을 점마다 흔든다.</summary>
        private static List<Vector2> Pebble(System.Random random, Vector2 center, float radius, int sides)
        {
            var points = new List<Vector2>();
            float offset = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < sides; i++)
            {
                float angle = offset + Mathf.PI * 2f * (i + ((float)random.NextDouble() - 0.5f) * 0.4f) / sides;
                float r = radius * Mathf.Lerp(0.7f, 1.15f, (float)random.NextDouble());
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }

            return points;
        }
    }
}
