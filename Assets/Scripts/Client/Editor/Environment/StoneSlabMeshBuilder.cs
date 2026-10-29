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

        /// <summary>깨진 판석 변형 수: 가로/세로로 두 조각 2종, 모서리가 떨어진 것 1종, 세 조각 1종.</summary>
        public const int BrokenVariantCount = 4;

        public static string PathOf(int variant) => $"{Folder}/SM_StoneSlab_{variant}.asset";
        public static string BrokenPathOf(int variant) => $"{Folder}/SM_StoneSlabBroken_{variant}.asset";

        [MenuItem("MiniChess/Environment/Build Stone Slab Meshes")]
        public static void BuildAll()
        {
            EnvironmentAssetUtil.EnsureFolder(Folder);
            for (int variant = 0; variant < VariantCount; variant++)
                Save(Build(variant), PathOf(variant));
            for (int variant = 0; variant < BrokenVariantCount; variant++)
                Save(BuildBroken(variant), BrokenPathOf(variant));

            AssetDatabase.SaveAssets();
            Debug.Log($"[StoneSlabMeshBuilder] {Folder} 에 판석 메시 {VariantCount}종, 깨진 판석 {BrokenVariantCount}종을 만들었습니다");
        }

        /// <summary>메시를 에셋으로 저장한다. 이미 있으면 내용만 바꿔 참조(GUID)를 유지한다.</summary>
        internal static void Save(Mesh mesh, string path)
        {
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>
        /// 깨진 판석: 판석을 살짝 꺾인 금 선으로 잘라, 조각마다 조금씩 다른 높이/기울기로 내려앉힌다.
        /// 조각 사이에는 좁은 틈이 생기고, 갈라진 면은 새로 드러난 밝은 돌로 칠한다. 어느 조각도 칸 윗면(0)보다 높지 않다.
        /// </summary>
        private static Mesh BuildBroken(int variant)
        {
            var random = new System.Random(200 + variant);
            float half = Footprint * 0.5f;
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            var pieces = new List<(List<Vector2> Outline, List<bool> Fresh)>();

            switch (variant)
            {
                case 0:
                case 1:
                {
                    // 마주 보는 두 변을 잇는 금 하나 → 두 조각
                    float from = variant == 0 ? Range(0.3f, 0.7f) : 3f + Range(0.25f, 0.5f);
                    float to = variant == 0 ? 2f + Range(0.3f, 0.7f) : 1f + Range(0.5f, 0.75f);
                    List<Vector2> crack = Crack(PointAt(from, half), PointAt(to, half), 2, 0.05f, random);
                    pieces.Add(Compose((Boundary(from, to, half), true), (Reversed(crack), false)));
                    pieces.Add(Compose((Boundary(to, from, half), true), (crack, false)));
                    break;
                }
                case 2:
                {
                    // 모서리 하나가 떨어져 나감
                    int corner = random.Next(4);
                    float from = corner - Range(0.22f, 0.4f);
                    float to = corner + Range(0.22f, 0.4f);
                    List<Vector2> crack = Crack(PointAt(from, half), PointAt(to, half), 1, 0.03f, random);
                    pieces.Add(Compose((Boundary(from, to, half), true), (Reversed(crack), false)));
                    pieces.Add(Compose((Boundary(to, from, half), true), (crack, false)));
                    break;
                }
                default:
                {
                    // 나란한 금 두 개 → 세 조각(아래 변에서 위 변으로. 위 변은 오른쪽 → 왼쪽으로 매개변수가 커진다)
                    float a1 = Range(0.22f, 0.38f), a2 = Range(0.62f, 0.78f);
                    float b2 = 2f + Range(0.22f, 0.38f), b1 = 2f + Range(0.62f, 0.78f);
                    List<Vector2> crack1 = Crack(PointAt(a1, half), PointAt(b1, half), 2, 0.04f, random);
                    List<Vector2> crack2 = Crack(PointAt(a2, half), PointAt(b2, half), 2, 0.04f, random);
                    pieces.Add(Compose((Boundary(b1, a1, half), true), (crack1, false)));
                    pieces.Add(Compose((Boundary(a1, a2, half), true), (crack2, false), (Boundary(b2, b1, half), true), (Reversed(crack1), false)));
                    pieces.Add(Compose((Boundary(a2, b2, half), true), (Reversed(crack2), false)));
                    break;
                }
            }

            var kit = new StoneMeshKit();
            foreach ((List<Vector2> outline, List<bool> fresh) in pieces)
            {
                OpenGap(outline, fresh, 0.008f);

                // 기울기만큼 올라가는 높이를 빼서 어느 점도 0 을 넘지 않게 한다.
                float tilt = Range(1f, 3f);
                float axis = Range(0f, 360f);
                Quaternion rotation = Quaternion.AngleAxis(tilt, Quaternion.Euler(0f, axis, 0f) * Vector3.right);
                float radius = 0f;
                Vector2 centroid = Centroid(outline);
                foreach (Vector2 p in outline)
                    radius = Mathf.Max(radius, Vector2.Distance(p, centroid));
                float drop = radius * Mathf.Sin(tilt * Mathf.Deg2Rad) + Range(0f, 0.015f);

                kit.Add(new StoneMeshKit.Piece
                {
                    Outline = outline,
                    Fresh = fresh,
                    Top = 0f,
                    Bottom = -Thickness,
                    Bevel = Bevel,
                    Rotation = rotation,
                    Offset = new Vector3(0f, -drop, 0f),
                    Brightness = Range(0.85f, 1f),
                });
            }

            return kit.ToMesh($"SM_StoneSlabBroken_{variant}");
        }

        /// <summary>판석 둘레 위의 점. s 는 0~4: 0 = 왼쪽 아래 꼭짓점에서 반시계로 한 변에 1씩(아래 → 오른쪽 → 위 → 왼쪽 변).</summary>
        private static Vector2 PointAt(float s, float half)
        {
            s = ((s % 4f) + 4f) % 4f;
            int side = Mathf.Min(3, Mathf.FloorToInt(s));
            float t = s - side;
            switch (side)
            {
                case 0: return new Vector2(-half + 2f * half * t, -half);
                case 1: return new Vector2(half, -half + 2f * half * t);
                case 2: return new Vector2(half - 2f * half * t, half);
                default: return new Vector2(-half, half - 2f * half * t);
            }
        }

        /// <summary>둘레를 from 에서 to 까지 반시계로 따라간 점들(사이의 꼭짓점 포함, 양 끝 포함).</summary>
        private static List<Vector2> Boundary(float from, float to, float half)
        {
            if (to <= from)
                to += 4f;
            var points = new List<Vector2> { PointAt(from, half) };
            for (int corner = Mathf.FloorToInt(from) + 1; corner < to; corner++)
                points.Add(PointAt(corner, half));
            points.Add(PointAt(to, half));
            return points;
        }

        /// <summary>a → b 금 선의 안쪽 점들(양 끝 제외). 선을 따라 나눈 점을 옆으로 조금씩 흔들어 꺾이게 한다.</summary>
        private static List<Vector2> Crack(Vector2 a, Vector2 b, int kinks, float jitter, System.Random random)
        {
            var points = new List<Vector2>();
            Vector2 perpendicular = new Vector2(-(b - a).y, (b - a).x).normalized;
            for (int i = 1; i <= kinks; i++)
            {
                float t = (float)i / (kinks + 1) + ((float)random.NextDouble() - 0.5f) * 0.1f;
                points.Add(Vector2.Lerp(a, b, t) + perpendicular * (((float)random.NextDouble() * 2f - 1f) * jitter));
            }

            return points;
        }

        private static List<Vector2> Reversed(List<Vector2> points)
        {
            var copy = new List<Vector2>(points);
            copy.Reverse();
            return copy;
        }

        /// <summary>
        /// 둘레 구간(boundary = true)과 금 선(false)을 번갈아 이어 조각 다각형을 만든다. 마지막 금 선 끝은 첫 점으로 이어진다.
        /// Fresh[i] = 점 i → i+1 변이 금(갈라진 면)인지: 둘레 구간의 마지막 점부터 다음 둘레 구간 직전까지.
        /// </summary>
        private static (List<Vector2>, List<bool>) Compose(params (List<Vector2> Points, bool Boundary)[] segments)
        {
            var outline = new List<Vector2>();
            var fresh = new List<bool>();
            foreach ((List<Vector2> points, bool boundary) in segments)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    outline.Add(points[i]);
                    fresh.Add(!boundary || i == points.Count - 1);
                }
            }

            return (outline, fresh);
        }

        /// <summary>금에 닿은 점을 조각 중심 쪽으로 조금 당겨 조각 사이에 틈을 만든다.</summary>
        private static void OpenGap(List<Vector2> outline, List<bool> fresh, float amount)
        {
            Vector2 centroid = Centroid(outline);
            int n = outline.Count;
            var moved = new List<Vector2>(outline);
            for (int i = 0; i < n; i++)
            {
                if (fresh[i] || fresh[(i - 1 + n) % n])
                    moved[i] = outline[i] + (centroid - outline[i]).normalized * amount;
            }

            outline.Clear();
            outline.AddRange(moved);
        }

        private static Vector2 Centroid(List<Vector2> points)
        {
            Vector2 sum = Vector2.zero;
            foreach (Vector2 p in points)
                sum += p;
            return sum / points.Count;
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
