using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 바닥 판석 메시(칸 하나 크기, 윗면 y = 0)를 변형 여러 개로 만들어 에셋으로 저장한다(StoneSurface 셰이더용).
    ///   - 윗면 가장자리는 둥글게 깎이고(베벨), 변형마다 모서리 몇 군데가 깨져 있고, 윗면이 아주 살짝 울퉁불퉁하다.
    ///   - 정점 색: R = 모서리 닳음(가장자리일수록 1), G = 깨진 자리, B = 아래쪽 때(옆면 아래일수록 1).
    ///   - UV: 윗면은 위에서 내려다본 좌표(0~1), 옆면은 둘레 방향 × 높이.
    /// 밑면은 보이지 않으므로 만들지 않는다. 다시 실행하면 같은 에셋을 덮어쓴다(참조는 유지된다).
    /// </summary>
    public static class StoneSlabMeshBuilder
    {
        public const string Folder = "Assets/Art/Environment/Meshes";
        public const int VariantCount = 3;

        /// <summary>판석 한 변 길이(칸 크기 1 기준). 나머지가 판석 사이 줄눈이 된다.</summary>
        private const float Footprint = 0.94f;
        private const float Thickness = 0.2f;
        private const float Bevel = 0.045f;
        private const float Undulation = 0.006f;

        /// <summary>베벨 안쪽(평평한 부분)을 나누는 칸 수. 깨진 자리를 표현할 만큼만.</summary>
        private const int InteriorSteps = 10;

        public static string PathOf(int variant) => $"{Folder}/SM_StoneSlab_{variant}.asset";

        [MenuItem("MiniChess/Environment/Build Stone Slab Meshes")]
        public static void BuildAll()
        {
            EnvironmentAssetUtil.EnsureFolder(Folder);
            for (int variant = 0; variant < VariantCount; variant++)
            {
                Mesh mesh = Build(variant);
                mesh.name = $"SM_StoneSlab_{variant}";

                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(PathOf(variant));
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(mesh, PathOf(variant));
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    Object.DestroyImmediate(mesh);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[StoneSlabMeshBuilder] {Folder} 에 판석 메시 {VariantCount}종을 만들었습니다");
        }

        private struct Chip
        {
            public Vector2 Center;
            public float Radius;
            public float Depth;
        }

        private static Mesh Build(int variant)
        {
            var random = new System.Random(100 + variant);
            float half = Footprint * 0.5f;
            List<Chip> chips = MakeChips(random, half, 1 + variant % 3);
            var phases = new Vector3((float)random.NextDouble() * 6.3f, (float)random.NextDouble() * 6.3f, (float)random.NextDouble() * 6.3f);

            float[] coords = Coords(half);
            int n = coords.Length;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // 윗면(격자)
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = coords[i], z = coords[j];
                    float d = EdgeDistance(x, z, half);
                    float y = Profile(d);
                    y += Undulation * (Mathf.Sin(x * 7f + phases.x) + Mathf.Sin(z * 9f + phases.y) + Mathf.Sin((x + z) * 5f + phases.z)) / 3f
                        * Mathf.Clamp01(d / Bevel);

                    float chip = 0;
                    foreach (Chip c in chips)
                    {
                        float distance = Vector2.Distance(new Vector2(x, z), c.Center);
                        float amount = SmoothStep(c.Radius, c.Radius * 0.35f, distance);
                        y -= c.Depth * amount;
                        chip = Mathf.Max(chip, amount);
                    }

                    y = Mathf.Max(y, -Thickness * 0.6f);
                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(x + 0.5f, z + 0.5f));
                    colors.Add(new Color(1f - SmoothStep(0f, Bevel * 1.8f, d), Mathf.Clamp01(chip * 1.3f), 0f, 1f));
                }
            }

            for (int j = 0; j < n - 1; j++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = (j + 1) * n + i, c = (j + 1) * n + i + 1, d = j * n + i + 1;
                    triangles.AddRange(new[] { a, b, c, a, c, d });
                }
            }

            // 옆면: 밖에서 봤을 때 왼쪽 → 오른쪽 순서로 윗면 테두리 정점을 따라간다.
            var front = new List<int>();
            var right = new List<int>();
            var back = new List<int>();
            var left = new List<int>();
            for (int k = 0; k < n; k++)
            {
                front.Add(k);                       // z = -half, x 증가
                right.Add(k * n + n - 1);           // x = +half, z 증가
                back.Add((n - 1) * n + n - 1 - k);  // z = +half, x 감소
                left.Add((n - 1 - k) * n);          // x = -half, z 감소
            }

            float perimeter = 0;
            foreach (List<int> side in new[] { front, right, back, left })
                perimeter = AddSide(side, perimeter, vertices, colors, uvs, triangles);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>윗면 테두리 정점들(top)에서 바닥까지 옆면 띠를 만든다. 다음 옆면의 UV 시작 위치를 돌려준다.</summary>
        private static float AddSide(List<int> top, float uStart, List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles)
        {
            int start = vertices.Count;
            float u = uStart;
            for (int k = 0; k < top.Count; k++)
            {
                Vector3 p = vertices[top[k]];
                if (k > 0)
                    u += Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(vertices[top[k - 1]].x, 0, vertices[top[k - 1]].z));

                Color topColor = colors[top[k]];
                vertices.Add(p);
                vertices.Add(new Vector3(p.x, -Thickness, p.z));
                colors.Add(new Color(0.6f, topColor.g, 0.25f, 1f));
                colors.Add(new Color(0.2f, 0f, 1f, 1f));
                uvs.Add(new Vector2(u, p.y));
                uvs.Add(new Vector2(u, -Thickness));
            }

            for (int k = 0; k < top.Count - 1; k++)
            {
                int p0 = start + k * 2, q0 = p0 + 1, p1 = p0 + 2, q1 = p0 + 3;
                triangles.AddRange(new[] { p0, p1, q1, p0, q1, q0 });
            }

            return u;
        }

        /// <summary>깨진 자리: 테두리 위의 점을 중심으로 한 움푹한 홈. 모서리(꼭짓점) 근처에 더 자주 생긴다.</summary>
        private static List<Chip> MakeChips(System.Random random, float half, int count)
        {
            var chips = new List<Chip>();
            for (int k = 0; k < count; k++)
            {
                int side = random.Next(4);
                // 0~1 중 양 끝(꼭짓점 쪽)에 몰리게
                float t = (float)random.NextDouble();
                t = t < 0.5f ? t * t * 2f : 1f - (1f - t) * (1f - t) * 2f;
                float along = Mathf.Lerp(-half, half, t);
                Vector2 center = side switch
                {
                    0 => new Vector2(along, -half),
                    1 => new Vector2(half, along),
                    2 => new Vector2(along, half),
                    _ => new Vector2(-half, along),
                };

                chips.Add(new Chip
                {
                    Center = center,
                    Radius = Mathf.Lerp(0.06f, 0.12f, (float)random.NextDouble()),
                    Depth = Mathf.Lerp(0.025f, 0.055f, (float)random.NextDouble()),
                });
            }

            return chips;
        }

        /// <summary>한 축의 정점 좌표: 베벨 구간은 촘촘하게, 안쪽은 고르게.</summary>
        private static float[] Coords(float half)
        {
            var edge = new[] { 0f, 0.15f, 0.4f, 0.7f, 1f };
            var coords = new List<float>();
            foreach (float e in edge)
                coords.Add(-half + e * Bevel);
            for (int i = 1; i < InteriorSteps; i++)
                coords.Add(-half + Bevel + (2f * half - 2f * Bevel) * i / InteriorSteps);
            for (int k = edge.Length - 1; k >= 0; k--)
                coords.Add(half - edge[k] * Bevel);
            return coords.ToArray();
        }

        /// <summary>테두리까지의 거리. 꼭짓점 근처는 둥글게 이어서, 꼭짓점이 비스듬히 닳은 모양이 된다.</summary>
        private static float EdgeDistance(float x, float z, float half)
        {
            float dx = half - Mathf.Abs(x);
            float dz = half - Mathf.Abs(z);
            if (dx < Bevel && dz < Bevel)
                return Mathf.Max(0f, Bevel - new Vector2(Bevel - dx, Bevel - dz).magnitude);
            return Mathf.Min(dx, dz);
        }

        /// <summary>베벨 단면: 테두리(0)에서 -Bevel, 안쪽(Bevel 이상)에서 0 인 사분원.</summary>
        private static float Profile(float d)
        {
            if (d >= Bevel)
                return 0f;
            float t = Bevel - d;
            return -Bevel + Mathf.Sqrt(Mathf.Max(0f, Bevel * Bevel - t * t));
        }

        /// <summary>a → b 에서 0 → 1(a > b 면 거꾸로).</summary>
        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
