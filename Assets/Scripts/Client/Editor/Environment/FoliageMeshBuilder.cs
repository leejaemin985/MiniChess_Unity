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

        public const int VineVariantCount = 3;
        public static string HangingVinePathOf(int variant) => $"{StoneSlabMeshBuilder.Folder}/SM_VineHang_{variant}.asset";
        public static string ClimbingVinePathOf(int variant) => $"{StoneSlabMeshBuilder.Folder}/SM_VineClimb_{variant}.asset";

        // 시안의 풀: 어두운 올리브 뿌리 → 노란 기가 도는 연두 끝
        private static readonly Color RootColor = new Color(0.07f, 0.12f, 0.04f);
        private static readonly Color TipColor = new Color(0.30f, 0.42f, 0.12f);
        private static readonly Color TipColorDry = new Color(0.44f, 0.44f, 0.17f);

        // 넝쿨: 갈색 기 도는 줄기, 짙은 초록 → 밝은 연두 잎(일부는 마른 잎)
        private static readonly Color StemColor = new Color(0.10f, 0.11f, 0.05f);
        private static readonly Color LeafDark = new Color(0.07f, 0.14f, 0.05f);
        private static readonly Color LeafLight = new Color(0.24f, 0.36f, 0.10f);
        private static readonly Color LeafDry = new Color(0.38f, 0.36f, 0.14f);

        /// <summary>늘어지는 넝쿨의 최대 길이, 타고 오르는 담쟁이의 최대 높이(칸 크기 1 기준). BoardDecorator 가 벽 높이에 맞춰 줄일 때 쓴다.</summary>
        public const float HangingVineLength = 0.62f;
        public const float ClimbingVineHeight = 0.42f;

        [MenuItem("MiniChess/Environment/Build Foliage And Rubble Meshes")]
        public static void BuildAll()
        {
            EnvironmentAssetUtil.EnsureFolder(StoneSlabMeshBuilder.Folder);
            for (int variant = 0; variant < GrassVariantCount; variant++)
                StoneSlabMeshBuilder.Save(Grass(new System.Random(400 + variant), variant), GrassPathOf(variant));
            for (int variant = 0; variant < RubbleVariantCount; variant++)
                StoneSlabMeshBuilder.Save(Rubble(new System.Random(500 + variant), variant), RubblePathOf(variant));
            for (int variant = 0; variant < VineVariantCount; variant++)
            {
                StoneSlabMeshBuilder.Save(HangingVine(new System.Random(600 + variant), variant), HangingVinePathOf(variant));
                StoneSlabMeshBuilder.Save(ClimbingVine(new System.Random(700 + variant), variant), ClimbingVinePathOf(variant));
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FoliageMeshBuilder] {StoneSlabMeshBuilder.Folder} 에 풀 {GrassVariantCount}종, 돌조각 {RubbleVariantCount}종, 넝쿨 {VineVariantCount * 2}종을 만들었습니다");
        }

        /// <summary>
        /// 늘어지는 넝쿨: 벽 꼭대기 모서리(원점)에서 벽면을 따라 아래로 처지는 줄기 몇 가닥과 잎.
        /// 벽면은 z = 0 평면, 벽 바깥은 -z, 벽 윗면은 +z 쪽. 줄기 머리는 윗면으로 조금 넘어가 걸쳐 있다.
        /// 정점 A(바람) = 아래 끝일수록 크게.
        /// </summary>
        private static Mesh HangingVine(System.Random random, int variant)
        {
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            var vine = new VineMesh();
            // 모바일: 잎을 크게 하고 수는 줄여(넝쿨 하나 약 150~250 삼각형) 여러 개를 붙여도 가볍게
            int strands = 4 + variant;
            float width = 0.34f + variant * 0.05f;
            const int steps = 5;

            for (int s = 0; s < strands; s++)
            {
                float x0 = Range(-width * 0.5f, width * 0.5f);
                float length = HangingVineLength * Range(0.45f, 1f);
                float phase = Range(0f, 6.3f), wave = Range(1.5f, 3f), drift = Range(-0.05f, 0.05f);

                // 윗면에 걸친 머리 → 모서리 → 아래로 처지는 줄기
                var path = new List<Vector3> { new Vector3(x0 + Range(-0.02f, 0.02f), 0.008f, Range(0.03f, 0.07f)) };
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    path.Add(new Vector3(x0 + Mathf.Sin(t * wave * Mathf.PI + phase) * 0.015f + drift * t, -length * t, -0.008f - t * 0.01f));
                }

                vine.AddStem(path, 0.006f, i => i == 0 ? 0f : (i - 1) / (float)steps * 0.7f);

                // 윗면에 누운 잎 몇 장(모서리를 넘어오는 덤불)
                for (int k = 0; k < 2; k++)
                {
                    var center = new Vector3(path[0].x + Range(-0.04f, 0.04f), 0.01f, path[0].z * Range(0.2f, 1f));
                    float angle = Range(0f, Mathf.PI * 2f);
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vine.AddLeaf(center, direction, Vector3.up, Range(0.05f, 0.065f), LeafColor(random), 0f);
                }

                // 처지는 줄기를 따라 잎(대체로 아래를 향함, 끝으로 갈수록 작게)
                for (int k = 1; k < path.Count; k++)
                {
                    float t = (k - 1) / (float)steps;
                    int count = k < path.Count - 1 ? 2 : 1;
                    for (int c = 0; c < count; c++)
                    {
                        Vector3 center = path[k] + new Vector3(Range(-0.03f, 0.03f), Range(-0.03f, 0.03f), 0f);
                        float angle = (-90f + Range(-70f, 70f)) * Mathf.Deg2Rad;
                        var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                        var normal = new Vector3(Range(-0.25f, 0.25f), 0.3f, -1f).normalized;
                        vine.AddLeaf(center, direction, normal, Range(0.05f, 0.07f) * (1f - 0.3f * t), LeafColor(random), t * 0.7f);
                    }
                }
            }

            return vine.ToMesh();
        }

        /// <summary>
        /// 타고 오르는 담쟁이: 벽 밑동(원점)에서 벽면을 덮으며 올라가는 줄기와 촘촘한 잎. 아래가 빽빽하고 위로 갈수록 성기다.
        /// 벽면은 z = 0 평면, 벽 바깥은 -z. 바닥에도 잎 몇 장이 퍼져 있다. 벽에 붙어 있어 바람에는 조금만 흔들린다.
        /// </summary>
        private static Mesh ClimbingVine(System.Random random, int variant)
        {
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            var vine = new VineMesh();
            int stems = 2 + variant;
            float width = 0.28f + variant * 0.07f;
            float height = ClimbingVineHeight * (0.55f + variant * 0.2f);
            const int steps = 5;

            for (int s = 0; s < stems; s++)
            {
                float x0 = Range(-width * 0.4f, width * 0.4f);
                float top = height * Range(0.6f, 1f);
                float phase = Range(0f, 6.3f), lean = Range(-0.08f, 0.08f);
                var path = new List<Vector3>();
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    path.Add(new Vector3(x0 + Mathf.Sin(t * 5f + phase) * 0.03f + lean * t, top * t, -0.006f));
                }

                vine.AddStem(path, 0.005f, i => i / (float)steps * 0.15f);

                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    int count = t < 0.5f ? 3 : 2;
                    for (int c = 0; c < count; c++)
                    {
                        Vector3 center = path[k] + new Vector3(Range(-0.06f, 0.06f) * (1f - 0.3f * t), Range(-0.03f, 0.03f), -Range(0.004f, 0.014f));
                        center.y = Mathf.Max(center.y, 0.01f);
                        float angle = Range(30f, 150f) * Mathf.Deg2Rad;
                        var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                        var normal = new Vector3(Range(-0.3f, 0.3f), 0.2f, -1f).normalized;
                        vine.AddLeaf(center, direction, normal, Range(0.045f, 0.065f), LeafColor(random), t * 0.15f);
                    }
                }
            }

            // 밑동에서 바닥으로 퍼진 잎
            for (int k = 0; k < 3 + variant; k++)
            {
                var center = new Vector3(Range(-width * 0.5f, width * 0.5f), 0.008f, -Range(0.01f, 0.06f));
                float angle = Range(0f, Mathf.PI * 2f);
                vine.AddLeaf(center, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)), Vector3.up, Range(0.045f, 0.06f), LeafColor(random), 0f);
            }

            return vine.ToMesh();
        }

        private static Color LeafColor(System.Random random)
        {
            Color color = Color.Lerp(LeafDark, LeafLight, (float)random.NextDouble());
            if (random.NextDouble() < 0.2)
                color = Color.Lerp(color, LeafDry, 0.6f);
            return color;
        }

        /// <summary>넝쿨 메시 조립: 줄기(가는 띠)와 잎(마름모 두 삼각형, 가장자리가 바깥으로 살짝 말림). 정점 색 RGB = 색, A = 바람.</summary>
        private sealed class VineMesh
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<int> _triangles = new List<int>();

            /// <summary>점들을 잇는 가는 띠(벽면을 향해 납작). wind(i) = 점 i 의 바람 세기.</summary>
            public void AddStem(List<Vector3> path, float width, System.Func<int, float> wind)
            {
                int start = _vertices.Count;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 forward = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 side = Vector3.Cross(forward, Vector3.forward).normalized * (width * 0.5f);
                    if (side.sqrMagnitude < 1e-8f)
                        side = Vector3.right * (width * 0.5f);
                    Color color = StemColor;
                    color.a = wind(i);
                    _vertices.Add(path[i] - side);
                    _vertices.Add(path[i] + side);
                    _normals.Add(new Vector3(0f, 0.3f, -1f).normalized);
                    _normals.Add(new Vector3(0f, 0.3f, -1f).normalized);
                    _colors.Add(color);
                    _colors.Add(color);
                }

                for (int i = 0; i < path.Count - 1; i++)
                {
                    int a = start + i * 2;
                    _triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }

            /// <summary>잎 하나: center 에 붙어 direction 쪽으로 size 만큼 뻗는다. normal = 잎이 향한 쪽.</summary>
            public void AddLeaf(Vector3 center, Vector3 direction, Vector3 normal, float size, Color color, float wind)
            {
                Vector3 side = Vector3.Cross(normal, direction).normalized * (size * 0.32f);
                Vector3 curl = normal * (size * 0.15f);
                int start = _vertices.Count;
                _vertices.Add(center);
                _vertices.Add(center + direction * (size * 0.45f) + side + curl);
                _vertices.Add(center + direction * size + curl * 0.5f);
                _vertices.Add(center + direction * (size * 0.45f) - side + curl);

                Color baseColor = color * 0.75f;
                baseColor.a = wind;
                color.a = wind;
                _colors.Add(baseColor);
                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);
                for (int i = 0; i < 4; i++)
                    _normals.Add(normal);

                _triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh();
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
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
