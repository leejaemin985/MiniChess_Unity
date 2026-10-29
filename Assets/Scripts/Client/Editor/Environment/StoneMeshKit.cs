using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 돌 조각(바닥 다각형을 세운 돌, 윗면 가장자리는 둥글게 깎임)을 한 메시에 차곡차곡 넣는 도구.
    /// 깨진 판석의 조각과 벽돌을 만드는 데 쓴다. 정점 색 규칙은 StoneSurface 셰이더를 따른다:
    ///   R = 모서리 닳음, G = 새로 드러난 돌(갈라진 면), B = 아래쪽 때, A = 조각 밝기(1 = 그대로, 낮을수록 어둡게).
    /// 다각형은 위에서 내려다볼 때 반시계 방향(x 오른쪽, z 위쪽)으로 준다.
    /// </summary>
    internal sealed class StoneMeshKit
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>베벨 단면을 나누는 비율(0 = 테두리, 1 = 베벨 안쪽 끝). 화면에서 작은 조각이라 4단이면 둥글게 보인다.</summary>
        private static readonly float[] BevelSteps = { 0f, 0.25f, 0.6f, 1f };

        /// <summary>돌 조각 하나의 모양.</summary>
        public struct Piece
        {
            /// <summary>바닥 다각형(반시계). Fresh[i] 가 참이면 점 i → i+1 변이 갈라진 면이다(밝은 새 돌, 때 적음).</summary>
            public List<Vector2> Outline;
            public List<bool> Fresh;
            public float Top;
            public float Bottom;
            public float Bevel;

            /// <summary>조각 전체를 이 회전/이동으로 옮긴다(회전 중심 = 다각형 중심, 윗면 높이).</summary>
            public Quaternion Rotation;
            public Vector3 Offset;

            /// <summary>윗면 UV 를 옮기는 양(조각마다 다른 무늬). UV 는 위에서 본 좌표 + 0.5 + UvOffset.</summary>
            public Vector2 UvOffset;
            public float Brightness;

            /// <summary>옆면 아래쪽 때의 세기(정점 색 B, 0 이면 1). 쌓은 벽돌은 낮춰 단마다 어두운 띠가 생기지 않게 한다.</summary>
            public float SideDirt;
        }

        public int VertexCount => _vertices.Count;

        public void Add(Piece piece)
        {
            List<Vector2> outline = piece.Outline;
            int n = outline.Count;
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in outline)
                centroid += p;
            centroid /= n;
            var pivot = new Vector3(centroid.x, piece.Top, centroid.y);
            Vector3 Place(Vector3 local) => piece.Rotation * (local - pivot) + pivot + piece.Offset;

            float brightness = piece.Brightness <= 0f ? 1f : piece.Brightness;
            bool FreshVertex(int i) => piece.Fresh != null && (piece.Fresh[i] || piece.Fresh[(i - 1 + n) % n]);

            // 윗면: 바깥 테두리에서 베벨 안쪽까지 고리 여러 개 + 가운데 뚜껑
            int ringStart = _vertices.Count;
            foreach (float step in BevelSteps)
            {
                float inset = step * piece.Bevel;
                List<Vector2> ring = Inset(outline, inset);
                float y = piece.Top + Profile(inset, piece.Bevel);
                float edge = 1f - SmoothStep(0f, 1.8f, step);
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = ring[i];
                    _vertices.Add(Place(new Vector3(p.x, y, p.y)));
                    _uvs.Add(new Vector2(p.x + 0.5f, p.y + 0.5f) + piece.UvOffset);
                    bool fresh = FreshVertex(i);
                    _colors.Add(new Color(fresh ? edge * 0.4f : edge, fresh ? 0.75f * (1f - step) : 0f, 0f, brightness));
                }
            }

            for (int r = 0; r < BevelSteps.Length - 1; r++)
            {
                int outer = ringStart + r * n, inner = outer + n;
                for (int i = 0; i < n; i++)
                {
                    int a = outer + i, b = outer + (i + 1) % n, c = inner + (i + 1) % n, d = inner + i;
                    _triangles.AddRange(new[] { a, d, c, a, c, b });
                }
            }

            int capStart = ringStart + (BevelSteps.Length - 1) * n;
            List<Vector2> cap = Inset(outline, piece.Bevel);
            foreach ((int a, int b, int c) in Triangulate(cap))
                _triangles.AddRange(new[] { capStart + a, capStart + c, capStart + b });

            // 옆면: 변마다 따로(각진 면)
            float u = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = outline[i], p1 = outline[(i + 1) % n];
                float top0 = piece.Top - piece.Bevel, top1 = top0;
                bool fresh = piece.Fresh != null && piece.Fresh[i];
                int s = _vertices.Count;
                _vertices.Add(Place(new Vector3(p0.x, top0, p0.y)));
                _vertices.Add(Place(new Vector3(p1.x, top1, p1.y)));
                _vertices.Add(Place(new Vector3(p0.x, piece.Bottom, p0.y)));
                _vertices.Add(Place(new Vector3(p1.x, piece.Bottom, p1.y)));
                float length = Vector2.Distance(p0, p1);
                _uvs.Add(new Vector2(u, top0) + piece.UvOffset);
                _uvs.Add(new Vector2(u + length, top1) + piece.UvOffset);
                _uvs.Add(new Vector2(u, piece.Bottom) + piece.UvOffset);
                _uvs.Add(new Vector2(u + length, piece.Bottom) + piece.UvOffset);
                u += length;

                float dirt = piece.SideDirt <= 0f ? 1f : piece.SideDirt;
                var topColor = new Color(0.5f, fresh ? 0.65f : 0f, (fresh ? 0.1f : 0.25f) * dirt, brightness);
                var bottomColor = new Color(0.15f, fresh ? 0.3f : 0f, dirt, brightness);
                _colors.Add(topColor);
                _colors.Add(topColor);
                _colors.Add(bottomColor);
                _colors.Add(bottomColor);
                _triangles.AddRange(new[] { s, s + 1, s + 3, s, s + 3, s + 2 });
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>직사각형 바닥(반시계).</summary>
        public static List<Vector2> Rectangle(Vector2 center, float width, float depth)
        {
            float x = width * 0.5f, z = depth * 0.5f;
            return new List<Vector2>
            {
                center + new Vector2(-x, -z), center + new Vector2(x, -z), center + new Vector2(x, z), center + new Vector2(-x, z),
            };
        }

        /// <summary>베벨 단면: 테두리(0)에서 -bevel, 안쪽(bevel 이상)에서 0 인 사분원.</summary>
        private static float Profile(float d, float bevel)
        {
            if (d >= bevel)
                return 0f;
            float t = bevel - d;
            return -bevel + Mathf.Sqrt(Mathf.Max(0f, bevel * bevel - t * t));
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>다각형(반시계)을 안쪽으로 distance 만큼 줄인다. 뾰족한 꼭짓점은 밀려나는 거리를 제한한다.</summary>
        private static List<Vector2> Inset(List<Vector2> polygon, float distance)
        {
            int n = polygon.Count;
            var result = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = polygon[(i - 1 + n) % n], p = polygon[i], next = polygon[(i + 1) % n];
                Vector2 n0 = InwardNormal(prev, p), n1 = InwardNormal(p, next);
                float denominator = Mathf.Max(0.3f, 1f + Vector2.Dot(n0, n1));
                result.Add(p + (n0 + n1) * (distance / denominator));
            }

            return result;
        }

        private static Vector2 InwardNormal(Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            return new Vector2(-d.y, d.x);
        }

        /// <summary>단순 다각형(반시계) 귀 자르기 삼각분할. 위에서 볼 때 반시계 순서의 삼각형을 돌려준다.</summary>
        private static List<(int, int, int)> Triangulate(List<Vector2> polygon)
        {
            var result = new List<(int, int, int)>();
            var indices = new List<int>();
            for (int i = 0; i < polygon.Count; i++)
                indices.Add(i);

            int guard = 0;
            while (indices.Count > 3 && guard++ < 1000)
            {
                bool clipped = false;
                for (int k = 0; k < indices.Count; k++)
                {
                    int ia = indices[(k - 1 + indices.Count) % indices.Count], ib = indices[k], ic = indices[(k + 1) % indices.Count];
                    Vector2 a = polygon[ia], b = polygon[ib], c = polygon[ic];
                    if (Cross(b - a, c - b) <= 1e-7f)
                        continue;

                    bool contains = false;
                    foreach (int other in indices)
                    {
                        if (other == ia || other == ib || other == ic)
                            continue;
                        if (InTriangle(polygon[other], a, b, c))
                        {
                            contains = true;
                            break;
                        }
                    }

                    if (contains)
                        continue;

                    result.Add((ia, ib, ic));
                    indices.RemoveAt(k);
                    clipped = true;
                    break;
                }

                if (!clipped)
                    break;
            }

            // 남은 것(정상이면 삼각형 하나, 비정상이면 부채꼴)으로 마무리
            for (int k = 1; k < indices.Count - 1; k++)
                result.Add((indices[0], indices[k], indices[k + 1]));
            return result;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f;
        }
    }
}
