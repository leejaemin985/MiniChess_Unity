using System.Collections.Generic;
using MiniChess.Client.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 바깥 배경: 보드 전체를 띄우는 큰 섬 하나, 아래 멀리 숲, 멀리 둘러싼 산.
    /// 카메라 각도가 자유롭게 바뀌어도 어느 쪽에서든 보이도록 보드 둘레 전체를 만든다(하늘은 BoardArt 의 스카이박스).
    ///   - 떠 있는 섬: 섬 프리팹 하나를 보드보다 조금 크게 늘려 보드를 얹는다(BuildIsland).
    ///   - 숲/산: Backdrop 셰이더. 멀거나 낮을수록 안개 색(하늘 지평선 색)에 묻혀 멀게 보인다. 보드 크기에 맞춰 실행 중에 메시를 만든다.
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

        /// <summary>숲/산(실행 중에 만든 메시, 지울 때 메시도 함께 지운다).</summary>
        public static GameObject Build(Transform parent, BoardCoordinates coordinates, BoardArt art)
        {
            if (art == null || art.BackdropMaterial == null)
                return null;

            var frame = new Frame(coordinates);
            var root = new GameObject("Backdrop");
            root.transform.SetParent(parent, false);
            CreatePart(root.transform, "Forest", Forest(frame, art), art.BackdropMaterial);
            CreatePart(root.transform, "Mountains", Mountains(frame, art), art.BackdropMaterial);
            return root;
        }

        /// <summary>
        /// 보드 전체를 띄우는 큰 섬 하나. 섬 프리팹(윗면 중앙이 원점)을 보드보다 사방 IslandMargin 칸 크게 늘려,
        /// 보드 판석이 섬 윗면에 얹히고 남는 둘레는 석조물이 설 자리가 된다. 세로는 가로/세로 배율의 평균에 IslandHeightScale 을 곱해 늘린다.
        /// 섬 윗면 바닥(테두리 안)은 보드 판석 윗면 바로 아래에 오게 맞춘다(판석이 바닥에 묻혀 보인다).
        /// 섬은 프리팹 에셋의 메시를 그대로 쓰므로 지울 때 오브젝트만 지운다(실행 중에 만든 숲/산 메시와 따로 둔다).
        /// 그림자는 드리우지 않는다(보드 아래라 티가 나지 않고, 그림자 패스 비용만 든다).
        /// </summary>
        public static GameObject BuildIsland(Transform parent, BoardCoordinates coordinates, BoardArt art)
        {
            if (art == null || art.IslandPrefab == null)
                return null;

            GameObject island = Object.Instantiate(art.IslandPrefab, parent);
            island.name = "Island";
            island.transform.localRotation = Quaternion.identity;
            island.transform.localScale = Vector3.one;

            // 프리팹 크기(윗면 한 변)를 재서 보드 + 둘레에 맞춘다
            Bounds bounds = default;
            bool first = true;
            foreach (Renderer renderer in island.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (first)
                    bounds = renderer.bounds;
                else
                    bounds.Encapsulate(renderer.bounds);
                first = false;
            }

            float margin = art.IslandMargin * coordinates.CellSize * 2f;
            float scaleX = (coordinates.Size.x + margin) / Mathf.Max(bounds.size.x, 0.01f);
            float scaleZ = (coordinates.Size.y + margin) / Mathf.Max(bounds.size.z, 0.01f);
            float scaleY = (scaleX + scaleZ) * 0.5f * art.IslandHeightScale;
            island.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            // 프리팹 윗면 바닥은 원점(테두리 꼭대기)보다 IslandFloorDepth 아래 → 늘린 뒤 그 바닥이 판석 윗면(0) 바로 아래(-0.01)에 오게
            island.transform.localPosition = new Vector3(coordinates.Center.x, art.IslandFloorDepth * scaleY - 0.01f, coordinates.Center.z);
            return island;
        }

        /// <summary>실행 중에 만든 숲/산 메시. 정점 색 상수(sRGB 기준)를 리니어로 바꿔 넣는다.</summary>
        private static void CreatePart(Transform parent, string name, Mesh mesh, Material material)
        {
            mesh.name = name;
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

        /// <summary>섬 아래 멀리 숲: 넓은 숲 바닥과 원뿔을 겹친 침엽수(덤불처럼 몰려 있고 빈터도 있다).</summary>
        private static Mesh Forest(Frame frame, BoardArt art)
        {
            float floor = -art.CliffDepth;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // 숲 바닥: 보드 중심을 둘러싼 원판
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

            // 나무: 보드 바로 아래는 비우고(위에서 내려다볼 때 섬 사이로 겹쳐 보이지 않게), 덤불 노이즈가 높은 곳에 몰린다
            var random = new System.Random(frame.Seed);
            float footClearance = 4f * frame.Cell;
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


        /// <summary>배경의 틀: 보드 중심과 보드 외곽 직사각형(숲 나무를 보드 아래에서 비켜 두는 데 쓴다).</summary>
        private sealed class Frame
        {
            public readonly Vector3 Center;
            public readonly float Cell;
            public readonly int Seed;
            private readonly Vector2 _min;
            private readonly Vector2 _max;

            public Frame(BoardCoordinates coordinates)
            {
                Center = coordinates.Center;
                Cell = coordinates.CellSize;
                Seed = coordinates.Width * 7919 + coordinates.Height * 104729;
                var half = new Vector2(coordinates.Size.x * 0.5f, coordinates.Size.y * 0.5f);
                var center = new Vector2(Center.x, Center.z);
                _min = center - half;
                _max = center + half;
            }

            /// <summary>보드 외곽 직사각형까지의 수평 거리(안쪽이면 0).</summary>
            public float RimDistance(Vector3 p)
            {
                float dx = Mathf.Max(_min.x - p.x, p.x - _max.x, 0f);
                float dz = Mathf.Max(_min.y - p.z, p.z - _max.y, 0f);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
        }
    }
}
