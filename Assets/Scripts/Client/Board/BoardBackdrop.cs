using System.Collections.Generic;
using MiniChess.Client.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 바깥 배경: 보드가 올라앉은 거대한 성벽(외곽 벽 바로 아래로 떨어지는 낭떠러지), 성벽 아래 숲, 멀리 둘러싼 산.
    /// 카메라 각도가 자유롭게 바뀌어도 어느 쪽에서든 보이도록 보드 둘레 전체를 만든다(하늘은 BoardArt 의 스카이박스).
    ///   - 성벽: 외곽 벽 바깥 면(보드 가장자리에서 RimOffset)에서 숲 바닥 아래까지 곧게 내려가는 석축. 몇 층마다 턱이 진다.
    ///     벽돌 무늬는 Masonry 셰이더가 월드 좌표로 그린다(벽돌을 메시로 만들지 않음).
    ///   - 모서리 단: 보드 네 모서리 바깥에 2 × 2 칸 돌바닥 단(아래로는 같은 성벽)을 두어 큰 석조물이 설 자리를 만든다.
    ///     단 위는 저폴리 판석(보드와 같은 돌 머티리얼), 바깥 두 변에는 낮은 흉벽과 모서리 기둥.
    /// 숲/산은 Backdrop 셰이더. 성벽/숲/산은 멀거나 낮을수록 안개 색(하늘 지평선 색)에 묻혀 멀게 보인다.
    /// 보드 크기에 맞춰 실행 중에 메시를 만든다(성벽, 판석, 흉벽, 숲, 산 = 그리기 호출 5번). 같은 크기의 보드는 항상 같은 모습이다.
    /// </summary>
    internal static class BoardBackdrop
    {
        private static readonly Color ForestFloorColor = new Color(0.12f, 0.16f, 0.08f);
        private static readonly Color TreeDark = new Color(0.06f, 0.11f, 0.06f);
        private static readonly Color TreeLight = new Color(0.15f, 0.22f, 0.09f);
        private static readonly Color MountainLow = new Color(0.16f, 0.20f, 0.12f);
        private static readonly Color MountainRock = new Color(0.38f, 0.36f, 0.42f);

        /// <summary>먼 산 겹(보드 중심에서의 거리, 숲 바닥에서 최고 높이). 멀수록 높고 안개에 더 묻힌다.</summary>
        private static readonly Vector2[] MountainLayers = { new Vector2(95f, 22f), new Vector2(140f, 36f), new Vector2(200f, 55f) };

        /// <summary>모서리 단 판석 윗면 높이(보드 판석 윗면 0 보다 조금 낮게). 석조물/넝쿨이 이 높이에 선다.</summary>
        internal const float GroundHeight = -0.04f;

        /// <summary>성벽 꼭대기(판석/외곽 벽 아래, 줄눈 바닥) 높이.</summary>
        private const float WallTop = -0.12f;

        /// <summary>성벽 바깥 면이 보드 가장자리에서 떨어진 거리(칸 단위). 외곽 벽(두께 약 0.46칸) 바로 바깥.</summary>
        private const float RimOffset = 0.5f;

        /// <summary>성벽 층 높이, 층마다 바깥으로 나오는 턱 폭(월드 단위).</summary>
        private const float CourseHeight = 3.2f;
        private const float Ledge = 0.18f;

        /// <summary>모서리 단의 한 변(칸 단위). 보드 모서리에서 바깥으로 이만큼 칸 격자에 맞춰 나온다.</summary>
        private const int BastionSize = 2;

        /// <param name="cliffLip">채워 줄 목록: 성벽 가장자리를 따라 일정 간격의 (가장자리 점, 바깥 방향). 넝쿨을 늘어뜨릴 자리.</param>
        public static GameObject Build(Transform parent, BoardCoordinates coordinates, BoardArt art, List<(Vector3 Point, Vector3 Normal)> cliffLip)
        {
            cliffLip?.Clear();
            if (art == null || art.BackdropMaterial == null)
                return null;

            var frame = new Frame(coordinates);
            var root = new GameObject("Backdrop");
            root.transform.SetParent(parent, false);

            if (art.MasonryMaterial != null)
                CreatePart(root.transform, "Fortress", Fortress(frame, art, cliffLip), art.MasonryMaterial, ShadowCastingMode.Off, linearColors: true);
            if (art.HasPavingMeshes && art.FloorMaterial != null)
                CreatePart(root.transform, "Paving", Paving(frame, coordinates, art), art.FloorMaterial, ShadowCastingMode.Off, linearColors: false);
            if (art.HasBorder)
                CreatePart(root.transform, "Parapet", Parapet(frame, art), art.WallMaterial, ShadowCastingMode.On, linearColors: false);
            CreatePart(root.transform, "Forest", Forest(frame, art), art.BackdropMaterial, ShadowCastingMode.Off, linearColors: true);
            CreatePart(root.transform, "Mountains", Mountains(frame, art), art.BackdropMaterial, ShadowCastingMode.Off, linearColors: true);
            return root;
        }

        /// <param name="linearColors">정점 색이 색(sRGB 기준 상수)이면 리니어로 바꿔 넣는다. 돌 메시처럼 정점 색이 데이터(닳음/때)면 그대로.</param>
        private static void CreatePart(Transform parent, string name, Mesh mesh, Material material, ShadowCastingMode shadows, bool linearColors)
        {
            mesh.name = name;
            if (linearColors)
            {
                Color[] colors = mesh.colors;
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = colors[i].linear;
                mesh.colors = colors;
            }

            mesh.UploadMeshData(markNoLongerReadable: true); // 다시 고치지 않으므로 CPU 쪽 사본은 버린다

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// 성벽: 외곽 벽 바로 바깥(직사각형)에서 숲 바닥 아래까지 곧게 내려가는 벽. 층(CourseHeight)마다 바깥으로 턱(Ledge)이 진다.
        /// 네 모서리 단도 같은 방식으로 내려간다. 꼭대기는 판석/외곽 벽 아래의 줄눈 바닥(무늬 없는 어두운 면).
        /// 정점 색: RGB = 색조(아래로 갈수록 어둡게), A = 벽돌 무늬(1) / 줄눈 바닥(0).
        /// </summary>
        private static Mesh Fortress(Frame frame, BoardArt art, List<(Vector3 Point, Vector3 Normal)> cliffLip)
        {
            var builder = new BoxBuilder(art.CliffDepth);
            float bottom = -art.CliffDepth - 1.5f;
            float cell = frame.Cell;

            builder.Top(frame.RimMin, frame.RimMax, WallTop, pattern: 0f);
            builder.Tower(frame.RimMin, frame.RimMax, WallTop, bottom, CourseHeight, Ledge);

            foreach ((Vector2 min, Vector2 max) in frame.Bastions())
            {
                builder.Top(min, max, WallTop, pattern: 0f);
                builder.Tower(min, max, WallTop, bottom, CourseHeight, Ledge);
            }

            // 넝쿨 자리: 성벽 가장자리를 따라 0.75칸마다(모서리 단 구간 제외)
            if (cliffLip != null)
            {
                float skip = (BastionSize - RimOffset + 0.4f) * cell;
                foreach ((Vector3 from, Vector3 to, Vector3 normal) in frame.RimEdges())
                {
                    float length = Vector3.Distance(from, to);
                    for (float d = skip; d < length - skip; d += 0.75f * cell)
                        cliffLip.Add((Vector3.Lerp(from, to, d / length) + Vector3.up * WallTop, normal));
                }
            }

            return builder.ToMesh();
        }

        /// <summary>
        /// 모서리 단 판석: 보드 칸 격자를 이어 모서리 단 칸마다 저폴리 판석 한 장(칸 좌표로 변형/회전/흔들림을 정함).
        /// 약 7% 는 빠져서 아래 줄눈 바닥이 보이고, 약 10% 는 살짝 내려앉는다. 보드와 같은 돌 머티리얼이라 색/질감이 이어진다.
        /// </summary>
        private static Mesh Paving(Frame frame, BoardCoordinates coordinates, BoardArt art)
        {
            var parts = new List<CombineInstance>();
            float cell = frame.Cell;
            for (int x = -BastionSize; x < coordinates.Width + BastionSize; x++)
            {
                for (int y = -BastionSize; y < coordinates.Height + BastionSize; y++)
                {
                    if (!frame.InBastion(new Vector2(x * cell, y * cell)))
                        continue;

                    var random = new System.Random(unchecked(x * 73856093 ^ y * 19349663 ^ 0x5f3759df));
                    float Jitter(float amount) => ((float)random.NextDouble() * 2f - 1f) * amount;
                    if (random.NextDouble() < 0.07)
                        continue;

                    bool sunk = random.NextDouble() < 0.1;
                    float tilt = sunk ? 2f : 0.7f;
                    var position = new Vector3(x * cell, GroundHeight + Jitter(0.01f) - (sunk ? 0.03f : 0f), y * cell);
                    Quaternion rotation = Quaternion.Euler(Jitter(tilt), random.Next(4) * 90f, Jitter(tilt));
                    parts.Add(new CombineInstance
                    {
                        mesh = art.GetPavingMesh(random.Next(1024)),
                        transform = Matrix4x4.TRS(position, rotation, Vector3.one * cell),
                    });
                }
            }

            return Combine(parts);
        }

        /// <summary>모서리 단 흉벽: 바깥 두 변을 따라 외곽 벽 조각(약 35% 는 무너져 비어 있음)과 바깥 모서리 기둥.</summary>
        private static Mesh Parapet(Frame frame, BoardArt art)
        {
            var parts = new List<CombineInstance>();
            float cell = frame.Cell;
            float inset = art.BorderOffset * cell;
            var random = new System.Random(frame.Seed + 17);

            void Add(Mesh mesh, Vector2 point, float yaw)
            {
                if (mesh != null)
                    parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(new Vector3(point.x, GroundHeight, point.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one * cell) });
            }

            foreach ((Vector2 min, Vector2 max) in frame.Bastions())
            {
                // 보드에서 먼 쪽 두 변
                float sx = Mathf.Sign((min.x + max.x) * 0.5f - frame.Center.x);
                float sz = Mathf.Sign((min.y + max.y) * 0.5f - frame.Center.z);
                float outerX = sx > 0f ? max.x - inset : min.x + inset;
                float outerZ = sz > 0f ? max.y - inset : min.y + inset;
                Add(art.BorderCornerMesh, new Vector2(outerX, outerZ), 0f);

                // 조각 메시는 x 방향 길이 1, 바깥 면이 -z(BoardView.CreateBorder 와 같은 규칙: 아래 변 0도, 오른쪽 변 -90도)
                float yawZ = sz > 0f ? 180f : 0f;
                float yawX = sx > 0f ? -90f : 90f;
                for (int k = 0; k < BastionSize - 1; k++)
                {
                    float t = (k + 0.5f) * cell;
                    if (random.NextDouble() >= 0.35)
                        Add(art.GetBorderMesh(random.Next(1024)), new Vector2(outerX - sx * (t + 0.5f * cell), outerZ), yawZ);
                    if (random.NextDouble() >= 0.35)
                        Add(art.GetBorderMesh(random.Next(1024)), new Vector2(outerX, outerZ - sz * (t + 0.5f * cell)), yawX);
                }
            }

            return Combine(parts);
        }

        private static Mesh Combine(List<CombineInstance> parts)
        {
            int vertexCount = 0;
            foreach (CombineInstance part in parts)
                vertexCount += part.mesh.vertexCount;

            var mesh = new Mesh { indexFormat = IndexFormatFor(vertexCount) };
            mesh.CombineMeshes(parts.ToArray(), mergeSubMeshes: true, useMatrices: true);
            return mesh;
        }

        /// <summary>성벽 아래 숲: 넓은 숲 바닥과 원뿔을 겹친 침엽수(덤불처럼 몰려 있고 빈터도 있다).</summary>
        private static Mesh Forest(Frame frame, BoardArt art)
        {
            float floor = -art.CliffDepth;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // 숲 바닥: 보드 중심을 둘러싼 원판(가운데는 성벽 속에 묻힘)
            float[] radii = { 0f, 20f, 40f, 70f, 110f, 170f, 260f, 400f };
            const int segments = 48;
            Vector3 center = frame.Center;
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

            // 나무: 성벽(망루, 층마다 턱 포함) 발치보다 조금 더 바깥부터, 덤불 노이즈가 높은 곳에 몰린다
            var random = new System.Random(frame.Seed);
            float footClearance = BastionSize * frame.Cell + art.CliffDepth / CourseHeight * Ledge + 4f;
            int placed = 0;
            for (int attempt = 0; attempt < art.TreeCount * 4 && placed < art.TreeCount; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(0f, art.ForestRadius, Mathf.Sqrt((float)random.NextDouble()));
                var p = new Vector3(center.x + Mathf.Cos(angle) * radius, 0f, center.z + Mathf.Sin(angle) * radius);
                if (frame.RimDistance(p) < footClearance)
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
        private static Mesh Mountains(Frame frame, BoardArt art)
        {
            float floor = -art.CliffDepth - 1f;
            const int segments = 120;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            Vector3 center = frame.Center;

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

        /// <summary>성벽의 틀: 보드 중심, 성벽 가장자리(보드 가장자리에서 RimOffset 칸 바깥 직사각형), 네 모서리 단.</summary>
        private sealed class Frame
        {
            public readonly Vector3 Center;
            public readonly float Cell;
            public readonly int Seed;
            public readonly Vector2 BoardMin;
            public readonly Vector2 BoardMax;
            public readonly Vector2 RimMin;
            public readonly Vector2 RimMax;

            public Frame(BoardCoordinates coordinates)
            {
                Center = coordinates.Center;
                Cell = coordinates.CellSize;
                Seed = coordinates.Width * 7919 + coordinates.Height * 104729;
                var half = new Vector2(coordinates.Size.x * 0.5f, coordinates.Size.y * 0.5f);
                var center = new Vector2(Center.x, Center.z);
                BoardMin = center - half;
                BoardMax = center + half;
                RimMin = BoardMin - Vector2.one * (RimOffset * Cell);
                RimMax = BoardMax + Vector2.one * (RimOffset * Cell);
            }

            /// <summary>테두리 네 변: (시작, 끝, 바깥 방향). 위에서 볼 때 반시계.</summary>
            public IEnumerable<(Vector3 From, Vector3 To, Vector3 Normal)> RimEdges()
            {
                var c = new[] { new Vector2(RimMin.x, RimMin.y), new Vector2(RimMax.x, RimMin.y), new Vector2(RimMax.x, RimMax.y), new Vector2(RimMin.x, RimMax.y) };
                for (int i = 0; i < 4; i++)
                {
                    var from = new Vector3(c[i].x, 0f, c[i].y);
                    var to = new Vector3(c[(i + 1) % 4].x, 0f, c[(i + 1) % 4].y);
                    Vector3 along = (to - from).normalized;
                    yield return (from, to, new Vector3(along.z, 0f, -along.x));
                }
            }

            /// <summary>네 모서리 단(min, max): 보드 모서리에서 바깥으로 BastionSize 칸 정사각형(칸 격자에 맞음).</summary>
            public IEnumerable<(Vector2 Min, Vector2 Max)> Bastions()
            {
                float size = BastionSize * Cell;
                foreach (Vector2 corner in new[] { BoardMin, new Vector2(BoardMax.x, BoardMin.y), BoardMax, new Vector2(BoardMin.x, BoardMax.y) })
                {
                    var outward = new Vector2(corner.x > Center.x ? 1f : -1f, corner.y > Center.z ? 1f : -1f);
                    Vector2 far = corner + outward * size;
                    yield return (Vector2.Min(corner, far), Vector2.Max(corner, far));
                }
            }

            public bool InBastion(Vector2 point)
            {
                foreach ((Vector2 min, Vector2 max) in Bastions())
                {
                    if (point.x > min.x && point.x < max.x && point.y > min.y && point.y < max.y)
                        return true;
                }

                return false;
            }

            /// <summary>성벽 가장자리 직사각형까지의 수평 거리(안쪽이면 0).</summary>
            public float RimDistance(Vector3 p)
            {
                float dx = Mathf.Max(RimMin.x - p.x, p.x - RimMax.x, 0f);
                float dz = Mathf.Max(RimMin.y - p.z, p.z - RimMax.y, 0f);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
        }

        /// <summary>
        /// 축에 나란한 상자 모양(성벽/망루)을 면마다 따로 만든다(모서리가 날카롭게, 면마다 노멀 하나).
        /// 정점 색: 아래로 갈수록 어둡게, A = 벽돌 무늬 세기.
        /// </summary>
        private sealed class BoxBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<int> _triangles = new List<int>();
            private readonly float _depth;

            public BoxBuilder(float depth)
            {
                _depth = depth;
            }

            private Color Shade(float y, float pattern)
            {
                float t = Mathf.Clamp01(-y / _depth);
                Color c = Color.white * Mathf.Lerp(1f, 0.6f, t);
                c.a = pattern;
                return c;
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float pattern)
            {
                // a-b-c-d: 바깥(노멀 쪽)에서 볼 때 시계 방향
                int start = _vertices.Count;
                foreach (Vector3 v in new[] { a, b, c, d })
                {
                    _vertices.Add(v);
                    _normals.Add(normal);
                    _colors.Add(Shade(v.y, pattern));
                }

                _triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            /// <summary>높이 y 의 평평한 윗면(min~max 직사각형).</summary>
            public void Top(Vector2 min, Vector2 max, float y, float pattern)
            {
                Quad(new Vector3(min.x, y, min.y), new Vector3(min.x, y, max.y), new Vector3(max.x, y, max.y), new Vector3(max.x, y, min.y), Vector3.up, pattern);
            }

            /// <summary>
            /// top 에서 bottom 까지 내려가는 네 옆면. course 높이마다 ledge 만큼 바깥으로 넓어지고, 그 턱의 윗면도 만든다.
            /// </summary>
            public void Tower(Vector2 min, Vector2 max, float top, float bottom, float course, float ledge)
            {
                float y = top;
                float grow = 0f;
                while (y > bottom)
                {
                    float next = Mathf.Max(bottom, y - course);
                    Vector2 lo = min - Vector2.one * grow;
                    Vector2 hi = max + Vector2.one * grow;
                    Sides(lo, hi, y, next);

                    if (next > bottom)
                    {
                        // 턱: 이번 층 바닥에서 바깥으로 ledge 만큼 나온 윗면(네 변 띠)
                        Vector2 lo2 = lo - Vector2.one * ledge;
                        Vector2 hi2 = hi + Vector2.one * ledge;
                        Ring(lo, hi, lo2, hi2, next);
                        grow += ledge;
                    }

                    y = next;
                }
            }

            private void Sides(Vector2 lo, Vector2 hi, float y0, float y1)
            {
                // -z 면, +x 면, +z 면, -x 면(각 면을 바깥에서 볼 때 왼쪽 위 → 오른쪽 위 → 오른쪽 아래 → 왼쪽 아래)
                Quad(new Vector3(lo.x, y0, lo.y), new Vector3(hi.x, y0, lo.y), new Vector3(hi.x, y1, lo.y), new Vector3(lo.x, y1, lo.y), Vector3.back, 1f);
                Quad(new Vector3(hi.x, y0, lo.y), new Vector3(hi.x, y0, hi.y), new Vector3(hi.x, y1, hi.y), new Vector3(hi.x, y1, lo.y), Vector3.right, 1f);
                Quad(new Vector3(hi.x, y0, hi.y), new Vector3(lo.x, y0, hi.y), new Vector3(lo.x, y1, hi.y), new Vector3(hi.x, y1, hi.y), Vector3.forward, 1f);
                Quad(new Vector3(lo.x, y0, hi.y), new Vector3(lo.x, y0, lo.y), new Vector3(lo.x, y1, lo.y), new Vector3(lo.x, y1, hi.y), Vector3.left, 1f);
            }

            /// <summary>안쪽 직사각형(lo~hi)과 바깥 직사각형(lo2~hi2) 사이의 윗면 띠.</summary>
            private void Ring(Vector2 lo, Vector2 hi, Vector2 lo2, Vector2 hi2, float y)
            {
                Quad(new Vector3(lo2.x, y, lo2.y), new Vector3(lo.x, y, lo.y), new Vector3(hi.x, y, lo.y), new Vector3(hi2.x, y, lo2.y), Vector3.up, 1f);
                Quad(new Vector3(hi2.x, y, lo2.y), new Vector3(hi.x, y, lo.y), new Vector3(hi.x, y, hi.y), new Vector3(hi2.x, y, hi2.y), Vector3.up, 1f);
                Quad(new Vector3(hi2.x, y, hi2.y), new Vector3(hi.x, y, hi.y), new Vector3(lo.x, y, hi.y), new Vector3(lo2.x, y, hi2.y), Vector3.up, 1f);
                Quad(new Vector3(lo2.x, y, hi2.y), new Vector3(lo.x, y, hi.y), new Vector3(lo.x, y, lo.y), new Vector3(lo2.x, y, lo2.y), Vector3.up, 1f);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh { indexFormat = IndexFormatFor(_vertices.Count) };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
