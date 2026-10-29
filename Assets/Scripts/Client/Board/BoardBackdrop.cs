using System.Collections.Generic;
using MiniChess.Client.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 바깥 배경: 보드가 올라앉은 고지대(외곽 벽 바깥 땅 + 아래로 떨어지는 절벽), 절벽 아래 숲, 멀리 둘러싼 산.
    /// 카메라 각도가 자유롭게 바뀌어도 어느 쪽에서든 보이도록 보드 둘레 전체를 만든다(하늘은 BoardArt 의 스카이박스).
    /// 모두 Backdrop 셰이더로 그리고, 멀거나 낮을수록 안개 색(하늘 지평선 색)에 묻혀 멀게 보인다.
    /// 보드 크기에 맞춰 실행 중에 메시를 만든다(절벽, 숲, 산 = 그리기 호출 3번). 같은 크기의 보드는 항상 같은 모습이다.
    /// 정점 색: RGB = 색, A = 돌 텍스처를 얹는 정도(가까운 절벽만 1).
    /// </summary>
    internal static class BoardBackdrop
    {
        private static readonly Color GrassColor = new Color(0.22f, 0.26f, 0.11f);
        private static readonly Color DirtColor = new Color(0.30f, 0.26f, 0.22f);
        private static readonly Color StoneColor = new Color(0.47f, 0.44f, 0.47f);
        private static readonly Color MossColor = new Color(0.20f, 0.25f, 0.11f);
        private static readonly Color ForestFloorColor = new Color(0.12f, 0.16f, 0.08f);
        private static readonly Color TreeDark = new Color(0.06f, 0.11f, 0.06f);
        private static readonly Color TreeLight = new Color(0.15f, 0.22f, 0.09f);
        private static readonly Color MountainLow = new Color(0.16f, 0.20f, 0.12f);
        private static readonly Color MountainRock = new Color(0.38f, 0.36f, 0.42f);

        /// <summary>먼 산 겹(보드 중심에서의 거리, 숲 바닥에서 최고 높이). 멀수록 높고 안개에 더 묻힌다.</summary>
        private static readonly Vector2[] MountainLayers = { new Vector2(95f, 22f), new Vector2(140f, 36f), new Vector2(200f, 55f) };

        /// <summary>절벽 지층 높이, 층마다 바깥으로 나오는 턱 폭, 아래로 갈수록 바깥으로 기우는 비율(월드 단위).</summary>
        private const float Strata = 1.6f;
        private const float Shelf = 0.35f;
        private const float Slope = 0.12f;

        /// <summary>보드 가장자리에서 절벽 끝(땅이 꺾이는 곳)까지의 거리.</summary>
        private static float LipOffset(BoardArt art, float cell) => 0.5f * cell + art.PlateauWidth * cell;

        public static GameObject Build(Transform parent, BoardCoordinates coordinates, BoardArt art)
        {
            if (art == null || art.BackdropMaterial == null)
                return null;

            var outline = new Outline(coordinates, step: 0.25f * coordinates.CellSize);
            var root = new GameObject("Backdrop");
            root.transform.SetParent(parent, false);
            CreatePart(root.transform, "Cliff", Cliff(outline, art, coordinates.CellSize), art.BackdropMaterial);
            CreatePart(root.transform, "Forest", Forest(outline, art), art.BackdropMaterial);
            CreatePart(root.transform, "Mountains", Mountains(outline, art), art.BackdropMaterial);
            return root;
        }

        private static void CreatePart(Transform parent, string name, Mesh mesh, Material material)
        {
            mesh.name = name;

            // 정점 색은 셰이더에 그대로 들어가므로(리니어 색 공간), 위의 색 상수(sRGB 기준)를 리니어로 바꿔 넣는다.
            Color[] colors = mesh.colors;
            for (int i = 0; i < colors.Length; i++)
                colors[i] = colors[i].linear;
            mesh.colors = colors;
            mesh.UploadMeshData(markNoLongerReadable: true); // 다시 고치지 않으므로 CPU 쪽 사본은 버린다

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// 고지대와 절벽: 보드 외곽선을 바깥으로 넓힌 고리들을 위에서 아래로 이어 붙인 한 장의 면.
        /// 위쪽 고리는 외곽 벽 밑에서 벽 바깥 땅을 지나 가장자리에서 꺾이고, 아래로 갈수록 바깥으로 비탈지며
        /// 지층마다 턱이 져(위를 향한 턱에는 이끼) 숲 바닥 아래까지 내려간다.
        /// </summary>
        private static Mesh Cliff(Outline outline, BoardArt art, float cell)
        {
            float depth = art.CliffDepth;
            float lip = LipOffset(art, cell);

            // (바깥 거리, 높이, 흔들림 폭). 앞 셋은 땅(외곽 벽 밑 → 벽 바깥 → 가장자리 앞), 나머지는 절벽.
            var rings = new List<Vector3>
            {
                new Vector3(0.02f * cell, -0.16f, 0f),
                new Vector3(0.5f * cell, -0.12f, 0f),
                new Vector3(lip - 0.3f * cell, -0.13f, 0.1f),
                new Vector3(lip - 0.08f * cell, -0.17f, 0.25f),
                new Vector3(lip + 0.05f * cell, -0.45f, 0.35f),
            };
            const int plateauRings = 4;
            const int cliffRings = 34;
            for (int k = 1; k <= cliffRings; k++)
            {
                float t = Mathf.Pow(k / (float)cliffRings, 1.35f);
                float y = Mathf.Lerp(-0.45f, -depth - 1.5f, t);
                rings.Add(new Vector3(lip + 0.1f + Slope * -y, y, 0.8f + t * 2f));
            }

            int n = outline.Count;
            var vertices = new Vector3[rings.Count * n];
            for (int j = 0; j < rings.Count; j++)
            {
                Vector3 ring = rings[j];
                for (int i = 0; i < n; i++)
                {
                    Vector3 basePoint = outline.Points[i];
                    Vector3 normal = outline.Normals[i];
                    Vector3 probe = basePoint + normal * 3f;
                    float offset = ring.x;
                    float y = ring.y;

                    if (j >= plateauRings)
                    {
                        // 지층: 아래 층일수록 조금씩 바깥으로 나와 턱이 진다(층 높이도 둘레를 따라 흔들림)
                        float layer = (-y + Noise(probe.x * 0.15f, 7.3f, probe.z * 0.15f) * Strata) / Strata;
                        offset += (Mathf.Floor(layer) + Smooth(0.75f, 1f, layer - Mathf.Floor(layer))) * Shelf;
                        offset += (Fbm(probe.x * 0.35f, y * 0.25f, probe.z * 0.35f) - 0.5f) * 2f * ring.z;
                    }
                    else if (ring.z > 0f)
                    {
                        offset += (Fbm(probe.x * 0.5f, 1.7f, probe.z * 0.5f) - 0.5f) * 2f * ring.z * cell;
                        y += (Noise(probe.x * 1.3f, 4.1f, probe.z * 1.3f) - 0.5f) * 0.04f;
                    }

                    vertices[j * n + i] = basePoint + normal * offset + Vector3.up * y;
                }
            }

            var triangles = new List<int>();
            for (int j = 0; j < rings.Count - 1; j++)
                AddStrip(triangles, j * n, (j + 1) * n, n, loop: true);

            var mesh = new Mesh { indexFormat = IndexFormatFor(vertices.Length) };
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            // 색: 땅(풀/흙 섞임) → 절벽 돌(지층마다 밝기 다름, 아래로 갈수록 어둡게), 위를 향한 턱에 이끼
            Vector3[] normals = mesh.normals;
            var colors = new Color[vertices.Length];
            for (int j = 0; j < rings.Count; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    int index = j * n + i;
                    Vector3 p = vertices[index];
                    if (j < plateauRings)
                    {
                        Color ground = Color.Lerp(GrassColor, DirtColor, Smooth(0.35f, 0.65f, Fbm(p.x * 0.8f, 2.2f, p.z * 0.8f)));
                        ground.a = 0.35f;
                        colors[index] = ground;
                        continue;
                    }

                    float layer = Mathf.Floor(-p.y / Strata);
                    float brightness = Mathf.Lerp(0.82f, 1.08f, Hash(Mathf.RoundToInt(layer), 17, 3)) * Mathf.Lerp(0.95f, 1.05f, Noise(p.x * 0.6f, p.y * 0.6f, p.z * 0.6f));
                    brightness *= Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(-p.y / depth));
                    Color stone = StoneColor * brightness;
                    float moss = Smooth(0.35f, 0.75f, normals[index].y) * Smooth(0.3f, 0.6f, Fbm(p.x * 0.4f, p.y * 0.4f + 9f, p.z * 0.4f));
                    moss = Mathf.Max(moss, j == plateauRings ? 0.6f : 0f);
                    Color color = Color.Lerp(stone, MossColor, moss);
                    color.a = 1f;
                    colors[index] = color;
                }
            }

            mesh.colors = colors;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>절벽 아래 숲: 넓은 숲 바닥과 원뿔을 겹친 침엽수(덤불처럼 몰려 있고 빈터도 있다).</summary>
        private static Mesh Forest(Outline outline, BoardArt art)
        {
            float floor = -art.CliffDepth;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // 숲 바닥: 보드 중심을 둘러싼 원판(가운데는 절벽 속에 묻힘)
            float[] radii = { 0f, 20f, 40f, 70f, 110f, 170f, 260f, 400f };
            const int segments = 48;
            Vector3 center = outline.Center;
            for (int r = 0; r < radii.Length; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float angle = s * Mathf.PI * 2f / segments;
                    var p = new Vector3(center.x + Mathf.Cos(angle) * radii[r], 0f, center.z + Mathf.Sin(angle) * radii[r]);
                    p.y = floor - 0.5f + (Noise(p.x * 0.05f, 0.5f, p.z * 0.05f) - 0.5f) * 2f;
                    vertices.Add(p);
                    normals.Add(Vector3.up);
                    Color c = ForestFloorColor * Mathf.Lerp(0.85f, 1.15f, Noise(p.x * 0.08f, 3f, p.z * 0.08f));
                    c.a = 0f;
                    colors.Add(c);
                }
            }

            for (int r = 0; r < radii.Length - 1; r++)
                AddStrip(triangles, r * segments, (r + 1) * segments, segments, loop: true);

            // 나무: 절벽 발치 바깥, 덤불 노이즈가 높은 곳에 몰린다
            var random = new System.Random(outline.Seed);
            // 절벽 발치(지층 턱 + 비탈 + 흔들림)보다 조금 더 바깥부터
            float footClearance = LipOffset(art, 1f) + Slope * art.CliffDepth + art.CliffDepth / Strata * Shelf + 4f;
            int placed = 0;
            for (int attempt = 0; attempt < art.TreeCount * 4 && placed < art.TreeCount; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(0f, art.ForestRadius, Mathf.Sqrt((float)random.NextDouble()));
                var p = new Vector3(center.x + Mathf.Cos(angle) * radius, 0f, center.z + Mathf.Sin(angle) * radius);
                if (outline.Distance(p) < footClearance)
                    continue;
                if (Fbm(p.x * 0.06f, 5f, p.z * 0.06f) < 0.35f + (float)random.NextDouble() * 0.25f)
                    continue;

                p.y = floor - 0.5f + (Noise(p.x * 0.05f, 0.5f, p.z * 0.05f) - 0.5f) * 2f;
                AddTree(vertices, normals, colors, triangles, p, random);
                placed++;
            }

            var mesh = new Mesh { indexFormat = IndexFormatFor(vertices.Count) };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>원뿔 셋을 겹친 침엽수 하나(밑면 없이 옆면만, 6각).</summary>
        private static void AddTree(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles, Vector3 root, System.Random random)
        {
            const int sides = 6;
            float height = Mathf.Lerp(3f, 6.5f, (float)random.NextDouble());
            float radius = height * Mathf.Lerp(0.26f, 0.34f, (float)random.NextDouble());
            float shade = Mathf.Lerp(0.8f, 1.15f, (float)random.NextDouble());
            float spin = (float)random.NextDouble() * Mathf.PI * 2f;

            for (int tier = 0; tier < 3; tier++)
            {
                float bottom = root.y + height * (0.12f + 0.24f * tier);
                float top = bottom + height * (0.5f - 0.07f * tier);
                float r = radius * (1f - 0.28f * tier);
                int start = vertices.Count;
                for (int s = 0; s < sides; s++)
                {
                    float angle = spin + tier * 0.5f + s * Mathf.PI * 2f / sides;
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices.Add(new Vector3(root.x, bottom, root.z) + radial * r);
                    normals.Add((radial + Vector3.up * 0.6f).normalized);
                    Color c = TreeDark * shade;
                    c.a = 0f;
                    colors.Add(c);
                }

                vertices.Add(new Vector3(root.x, top, root.z));
                normals.Add(Vector3.up);
                Color tip = TreeLight * shade;
                tip.a = 0f;
                colors.Add(tip);

                int apex = start + sides;
                for (int s = 0; s < sides; s++)
                    triangles.AddRange(new[] { start + s, apex, start + (s + 1) % sides });
            }
        }

        /// <summary>멀리 보드를 빙 둘러싼 산 능선 몇 겹(안쪽을 향한 비탈만). 먼 겹일수록 높고 안개에 묻힌다.</summary>
        private static Mesh Mountains(Outline outline, BoardArt art)
        {
            float floor = -art.CliffDepth - 1f;
            const int segments = 120;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            Vector3 center = outline.Center;

            for (int layer = 0; layer < MountainLayers.Length; layer++)
            {
                float distance = MountainLayers[layer].x;
                float maxHeight = MountainLayers[layer].y;
                int start = vertices.Count;

                // 줄: 앞 발치 → 중턱 → 능선
                for (int row = 0; row < 3; row++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        float angle = s * Mathf.PI * 2f / segments;
                        var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        Vector3 probe = direction * 4f + new Vector3(0f, layer * 13.7f, 0f);
                        float ridge = 1f - Mathf.Abs(Fbm(probe.x, probe.y, probe.z) * 2f - 1f);
                        float peak = maxHeight * (0.3f + 0.7f * ridge * ridge);
                        float jitter = (Noise(probe.x * 3f, probe.y + row, probe.z * 3f) - 0.5f) * 8f;

                        float radius;
                        float height;
                        switch (row)
                        {
                            case 0: radius = distance - 25f; height = 0f; break;
                            case 1: radius = distance - 9f + jitter; height = peak * 0.45f; break;
                            default: radius = distance + jitter; height = peak; break;
                        }

                        vertices.Add(center + direction * radius + Vector3.up * (floor + height));
                        Color c = Color.Lerp(MountainLow, MountainRock, row * 0.5f) * Mathf.Lerp(0.85f, 1.1f, ridge);
                        c.a = 0f;
                        colors.Add(c);
                    }
                }

                for (int row = 0; row < 2; row++)
                    AddStrip(triangles, start + row * segments, start + (row + 1) * segments, segments, loop: true);
            }

            var mesh = new Mesh { indexFormat = IndexFormatFor(vertices.Count) };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 같은 수의 점으로 된 두 줄(inner, outer)을 사각형 띠로 잇는다. 두 줄이 위에서 볼 때 반시계로 돌면
        /// 면은 위쪽을 향한다(바깥으로 내려가면 위-바깥, 바깥으로 올라가면 위-안쪽 = 보드 쪽).
        /// </summary>
        private static void AddStrip(List<int> triangles, int inner, int outer, int count, bool loop)
        {
            int end = loop ? count : count - 1;
            for (int i = 0; i < end; i++)
            {
                int next = (i + 1) % count;
                int a = inner + i, b = inner + next, c = outer + i, d = outer + next;
                triangles.AddRange(new[] { a, b, d, a, d, c });
            }
        }

        /// <summary>정점 수가 16비트 인덱스 범위 안이면 UInt16(메모리 절반, 구형 GPU 호환), 넘으면 UInt32.</summary>
        internal static IndexFormat IndexFormatFor(int vertexCount) => vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

        private static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        /// <summary>3D 값 노이즈(0~1).</summary>
        private static float Noise(float x, float y, float z)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fy = y - iy, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            float Corner(int dx, int dy, int dz) => Hash(ix + dx, iy + dy, iz + dz);
            float x00 = Mathf.Lerp(Corner(0, 0, 0), Corner(1, 0, 0), fx);
            float x10 = Mathf.Lerp(Corner(0, 1, 0), Corner(1, 1, 0), fx);
            float x01 = Mathf.Lerp(Corner(0, 0, 1), Corner(1, 0, 1), fx);
            float x11 = Mathf.Lerp(Corner(0, 1, 1), Corner(1, 1, 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        private static float Fbm(float x, float y, float z)
        {
            return Noise(x, y, z) * 0.55f + Noise(x * 2.1f + 3.7f, y * 2.1f, z * 2.1f) * 0.3f + Noise(x * 4.3f, y * 4.3f + 7.1f, z * 4.3f) * 0.15f;
        }

        /// <summary>
        /// 보드 외곽선(판석 바깥 경계 직사각형)을 따라 일정 간격으로 찍은 점과 바깥 방향.
        /// 위에서 볼 때 반시계로 돈다. 모서리에서는 한 점에 방향만 90도 돌려 여러 번 찍어서,
        /// 점 + 방향 × 거리로 넓히면 모서리가 그 거리를 반지름으로 둥글어진다.
        /// </summary>
        private sealed class Outline
        {
            public readonly List<Vector3> Points = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly Vector3 Center;
            public readonly int Seed;
            private readonly float _halfX;
            private readonly float _halfZ;

            public int Count => Points.Count;

            public Outline(BoardCoordinates coordinates, float step)
            {
                Center = coordinates.Center;
                _halfX = coordinates.Size.x * 0.5f;
                _halfZ = coordinates.Size.y * 0.5f;
                Seed = coordinates.Width * 7919 + coordinates.Height * 104729;

                const int cornerSteps = 6;
                var corners = new[]
                {
                    new Vector3(-_halfX, 0f, -_halfZ), new Vector3(_halfX, 0f, -_halfZ),
                    new Vector3(_halfX, 0f, _halfZ), new Vector3(-_halfX, 0f, _halfZ),
                };

                for (int side = 0; side < 4; side++)
                {
                    Vector3 from = corners[side];
                    Vector3 to = corners[(side + 1) % 4];
                    Vector3 along = (to - from).normalized;
                    var outward = new Vector3(along.z, 0f, -along.x);
                    int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / step));
                    for (int k = 0; k < count; k++)
                        Add(from + (to - from) * (k / (float)count), outward);

                    // 다음 변으로 넘어가는 모서리(to): 바깥 방향을 돌려 가며 찍는다
                    var nextOutward = new Vector3(-outward.z, 0f, outward.x);
                    for (int k = 0; k < cornerSteps; k++)
                    {
                        float t = k / (float)cornerSteps;
                        Add(to, Vector3.Slerp(outward, nextOutward, t).normalized);
                    }
                }
            }

            private void Add(Vector3 point, Vector3 normal)
            {
                Points.Add(Center + point);
                Normals.Add(normal);
            }

            /// <summary>보드 외곽 직사각형까지의 수평 거리(안쪽이면 0).</summary>
            public float Distance(Vector3 p)
            {
                float dx = Mathf.Max(Mathf.Abs(p.x - Center.x) - _halfX, 0f);
                float dz = Mathf.Max(Mathf.Abs(p.z - Center.z) - _halfZ, 0f);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
        }
    }
}
