using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 돌을 쌓은 벽 메시를 만들어 에셋으로 저장한다(StoneSurface 셰이더용). 벽돌 하나하나가 StoneMeshKit 의 돌 조각이다.
    ///   - 벽 칸(칸 크기 1, 바닥 윗면 y = 0 위에 선다): 위가 무너진 벽돌 벽 / 기둥 / 반쯤 무너진 낮은 벽.
    /// 벽돌은 아래에 놓인 것 위에 얹히므로(높이 지도), 빠진 벽돌 위의 벽돌은 내려앉아 무너진 모양이 된다.
    /// </summary>
    public static class StoneWallMeshBuilder
    {
        public const int WallVariantCount = 3;

        public static string WallPathOf(int variant) => $"{StoneSlabMeshBuilder.Folder}/SM_StoneWall_{variant}.asset";

        [MenuItem("MiniChess/Environment/Build Stone Wall Meshes")]
        public static void BuildAll()
        {
            EnvironmentAssetUtil.EnsureFolder(StoneSlabMeshBuilder.Folder);
            StoneSlabMeshBuilder.Save(BrokenWall(new System.Random(301)), WallPathOf(0));
            StoneSlabMeshBuilder.Save(Pillar(new System.Random(302)), WallPathOf(1));
            StoneSlabMeshBuilder.Save(LowRuin(new System.Random(303)), WallPathOf(2));

            AssetDatabase.SaveAssets();
            Debug.Log($"[StoneWallMeshBuilder] {StoneSlabMeshBuilder.Folder} 에 벽 {WallVariantCount}종을 만들었습니다");
        }

        /// <summary>위쪽 두 단이 한쪽으로 무너져 내린 큰 돌 벽(칸 대부분을 채운다, 높이 약 0.8~1.0).</summary>
        private static Mesh BrokenWall(System.Random random)
        {
            var wall = new Mason(random, new Rect(-0.43f, -0.43f, 0.86f, 0.86f), 0f);
            float fallSide = random.Next(2) == 0 ? -1f : 1f;
            for (int course = 0; course < 5; course++)
            {
                float missing = course < 2 ? 0f : 0.1f + (course - 2) * 0.22f;
                bool upper = course >= 2;
                wall.Course(course, 0.2f, 0.3f, 0.46f, (center, along) => missing + (upper ? 0.45f * Mathf.Clamp01(fallSide * along) : 0f));
            }

            return wall.ToMesh("SM_StoneWall_0");
        }

        /// <summary>받침돌 위에 선 네모 기둥(높이 약 0.95). 꼭대기 돌은 반쯤 깨져 있다.</summary>
        private static Mesh Pillar(System.Random random)
        {
            var pillar = new Mason(random, new Rect(-0.34f, -0.34f, 0.68f, 0.68f), 0f);
            pillar.Block(Vector2.zero, 0.68f, 0.68f, 0.12f, yawJitter: 0.5f);
            const float size = 0.52f;
            for (int course = 0; course < 4; course++)
            {
                if (course % 2 == 0)
                {
                    pillar.Block(Vector2.zero, size, size, 0.2f, yawJitter: 1.5f);
                }
                else
                {
                    pillar.Block(new Vector2(0f, -size * 0.25f), size, size * 0.5f - 0.006f, 0.2f, yawJitter: 1f);
                    pillar.Block(new Vector2(0f, size * 0.25f), size, size * 0.5f - 0.006f, 0.2f, yawJitter: 1f);
                }
            }

            // 꼭대기: 반쪽만 남은 돌
            pillar.Block(new Vector2(-0.12f, 0.02f), size * 0.5f, size * 0.9f, 0.14f, yawJitter: 4f);
            return pillar.ToMesh("SM_StoneWall_1");
        }

        /// <summary>두세 단만 남은 낮은 벽 + 앞에 떨어진 돌 몇 개(높이 약 0.5~0.6).</summary>
        private static Mesh LowRuin(System.Random random)
        {
            // 벽은 칸 뒤쪽 2/3 에만 쌓고, 앞쪽 띠에 떨어진 돌을 둔다.
            var ruin = new Mason(random, new Rect(-0.44f, -0.44f, 0.88f, 0.88f), 0f) { CourseArea = new Rect(-0.44f, -0.16f, 0.88f, 0.6f) };
            for (int course = 0; course < 3; course++)
            {
                float missing = course < 2 ? 0f : 0.45f;
                ruin.Course(course, 0.2f, 0.3f, 0.44f, (center, along) => missing);
            }

            for (int i = 0; i < 3; i++)
                ruin.Rubble();

            return ruin.ToMesh("SM_StoneWall_2");
        }

        /// <summary>벽돌 쌓기. 바닥 영역을 작은 격자로 나눠 칸마다 지금 높이를 기억하고, 새 벽돌은 그 아래 가장 높은 곳에 얹는다.</summary>
        private sealed class Mason
        {
            private const int Grid = 24;

            private readonly System.Random _random;
            private readonly Rect _area;
            private readonly float[,] _height = new float[Grid, Grid];
            private readonly StoneMeshKit _kit = new StoneMeshKit();
            private Rect? _courseArea;

            /// <summary>Course 로 쌓는 영역(기본: 전체 영역). 나머지는 떨어진 벽돌(Rubble) 자리로 쓸 수 있다.</summary>
            public Rect CourseArea
            {
                get => _courseArea ?? _area;
                set => _courseArea = value;
            }

            public Mason(System.Random random, Rect area, float baseHeight)
            {
                _random = random;
                _area = area;
                for (int x = 0; x < Grid; x++)
                    for (int z = 0; z < Grid; z++)
                        _height[x, z] = baseHeight;
            }

            private float Range(float a, float b) => Mathf.Lerp(a, b, (float)_random.NextDouble());

            /// <summary>
            /// 한 단: 짝수 단은 x 방향 벽돌 두 줄, 홀수 단은 z 방향 벽돌 두 줄(엇갈려 쌓기). 벽돌 길이는 minLength~maxLength.
            /// missing(벽돌 중심, 단 방향 위치 -1~1) 확률로 그 벽돌을 빼 무너진 모양을 만든다.
            /// </summary>
            public void Course(int course, float height, float minLength, float maxLength, System.Func<Vector2, float, float> missing)
            {
                Rect area = CourseArea;
                bool alongX = course % 2 == 0;
                float length = alongX ? area.width : area.height;
                float across = alongX ? area.height : area.width;
                float start = alongX ? area.xMin : area.yMin;
                float acrossStart = alongX ? area.yMin : area.xMin;
                const float gap = 0.008f;

                for (int row = 0; row < 2; row++)
                {
                    float depth = across * 0.5f - gap;
                    float rowCenter = acrossStart + across * (0.25f + 0.5f * row);
                    float position = start;
                    // 줄마다 첫 벽돌 길이를 달리해 이음매가 엇갈리게 한다.
                    float first = row == 0 ? Range(minLength * 0.5f, maxLength * 0.6f) : Range(minLength, maxLength);
                    bool isFirst = true;
                    while (position < start + length - 0.05f)
                    {
                        float brick = Mathf.Min(isFirst ? first : Range(minLength, maxLength), start + length - position);
                        isFirst = false;
                        float center = position + brick * 0.5f;
                        Vector2 xz = alongX ? new Vector2(center, rowCenter) : new Vector2(rowCenter, center);
                        float along = (center - (start + length * 0.5f)) / (length * 0.5f);
                        if (_random.NextDouble() >= missing(xz, alongX ? along : (rowCenter - area.center.x) / (area.width * 0.5f)))
                        {
                            float w = alongX ? brick - gap : depth;
                            float d = alongX ? depth : brick - gap;
                            Block(xz, w, d, height * Range(0.94f, 1.06f), yawJitter: 1f);
                        }

                        position += brick;
                    }
                }
            }

            /// <summary>벽돌 하나를 xz 중심에 얹는다(아래 가장 높은 곳 위).</summary>
            public void Block(Vector2 center, float width, float depth, float height, float yawJitter)
            {
                var rect = new Rect(center.x - width * 0.5f, center.y - depth * 0.5f, width, depth);
                float bottom = Sample(rect);
                float top = bottom + height;
                _kit.Add(new StoneMeshKit.Piece
                {
                    Outline = StoneMeshKit.Rectangle(center, width, depth),
                    Top = top,
                    Bottom = bottom - 0.01f,
                    Bevel = Mathf.Min(0.022f, height * 0.18f),
                    Rotation = Quaternion.Euler(Range(-1f, 1f), Range(-yawJitter, yawJitter), Range(-1f, 1f)),
                    UvOffset = new Vector2(Range(0f, 5f), Range(0f, 5f)),
                    Brightness = Range(0.7f, 1f),
                    SideDirt = 0.35f,
                });
                Raise(rect, top);
            }

            /// <summary>바닥에 떨어져 비스듬히 놓인 벽돌.</summary>
            public void Rubble()
            {
                var center = new Vector2(Range(_area.xMin + 0.1f, _area.xMax - 0.1f), Range(_area.yMin + 0.08f, _area.yMin + 0.2f));
                float width = Range(0.14f, 0.24f), depth = Range(0.1f, 0.16f), height = Range(0.08f, 0.12f);
                var rect = new Rect(center.x - width * 0.5f, center.y - depth * 0.5f, width, depth);
                float bottom = Sample(rect);
                _kit.Add(new StoneMeshKit.Piece
                {
                    Outline = StoneMeshKit.Rectangle(center, width, depth),
                    Top = bottom + height,
                    Bottom = bottom - 0.02f,
                    Bevel = 0.02f,
                    Rotation = Quaternion.Euler(Range(-18f, 18f), Range(0f, 360f), Range(-18f, 18f)),
                    Offset = new Vector3(0f, -0.02f, 0f),
                    UvOffset = new Vector2(Range(0f, 5f), Range(0f, 5f)),
                    Brightness = Range(0.6f, 0.95f),
                });
                Raise(rect, bottom + height * 0.7f);
            }

            public Mesh ToMesh(string name) => _kit.ToMesh(name);

            private float Sample(Rect rect)
            {
                float max = float.MinValue;
                ForCells(rect, (x, z) => max = Mathf.Max(max, _height[x, z]));
                return max == float.MinValue ? 0f : max;
            }

            private void Raise(Rect rect, float top)
            {
                ForCells(rect, (x, z) => _height[x, z] = Mathf.Max(_height[x, z], top));
            }

            /// <summary>rect 가 덮는 격자 칸마다. 이웃 벽돌과 경계 칸을 나눠 쓰지 않도록 rect 를 조금 줄여서 본다.</summary>
            private void ForCells(Rect rect, System.Action<int, int> action)
            {
                const float inset = 0.02f;
                rect = new Rect(rect.xMin + inset, rect.yMin + inset, Mathf.Max(0f, rect.width - inset * 2f), Mathf.Max(0f, rect.height - inset * 2f));
                int x0 = Cell(rect.xMin, _area.xMin, _area.width), x1 = Cell(rect.xMax, _area.xMin, _area.width);
                int z0 = Cell(rect.yMin, _area.yMin, _area.height), z1 = Cell(rect.yMax, _area.yMin, _area.height);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                        action(x, z);
            }

            private static int Cell(float value, float min, float size)
            {
                return Mathf.Clamp(Mathf.FloorToInt((value - min) / size * Grid), 0, Grid - 1);
            }
        }
    }
}
