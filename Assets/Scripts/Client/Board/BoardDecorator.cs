using System;
using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using UnityEngine;
using UnityEngine.Rendering;
using CoreBoard = MiniChess.Core.State.Board;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 장식(풀 덤불, 돌조각)을 흩뿌려 종류마다 메시 하나로 합친다(그리기 호출 2번).
    /// 칸 안쪽에는 두지 않는다. 유닛/하이라이트를 가리지 않게 줄눈과 가장자리에만 놓는다:
    ///   - 풀: 네 칸이 만나는 줄눈 모서리, 두 칸 사이 줄눈, 벽 칸 둘레.
    ///   - 돌조각: 벽 칸 둘레(벽 칸 안쪽 가장자리).
    ///   - 넝쿨(풀 메시에 합침): 벽 칸의 드러난 면에 늘어지는 넝쿨과 타고 오르는 담쟁이.
    ///     BoardArt 에서 켜고 끌 수 있다. 넝쿨은 풀/돌조각 다음에 놓아 켜고 꺼도 풀/돌조각 자리는 그대로다.
    /// 칸 좌표로 정하므로 같은 맵은 항상 같은 모습이다. 줄눈의 풀은 판석 틈(윗면 아래)에서 자라 나온다.
    /// </summary>
    internal static class BoardDecorator
    {
        /// <summary>줄눈 풀의 뿌리 높이(판석 윗면 아래, 틈 속).</summary>
        private const float GroutDepth = -0.05f;

        /// <summary>넝쿨 메시의 최대 길이/높이(칸 크기 1 기준, FoliageMeshBuilder 와 같은 값). 벽 높이에 맞춰 줄일 때 쓴다.</summary>
        private const float VineHangLength = 0.62f;
        private const float VineClimbHeight = 0.42f;

        /// <summary>넝쿨을 붙일 표면: 벽 칸의 벽 메시.</summary>
        internal sealed class Surfaces
        {
            public Func<Position, MeshFilter> WallOf;
        }

        public static GameObject Build(Transform parent, CoreBoard board, BoardCoordinates coordinates, BoardArt art, Surfaces surfaces)
        {
            if (art == null || (!art.HasGrass && !art.HasRubble))
                return null;

            var random = new System.Random(board.Width * 7919 + board.Height * 104729);
            var grass = new List<CombineInstance>();
            var rubble = new List<CombineInstance>();
            float cell = coordinates.CellSize;
            float half = cell * 0.5f;

            void AddGrass(Vector3 position, float scale)
            {
                if (!art.HasGrass)
                    return;
                Mesh mesh = art.GetGrassMesh(random.Next(1024));
                float yaw = (float)random.NextDouble() * 360f;
                grass.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * (cell * scale)) });
            }

            void AddRubble(Vector3 position, float scale)
            {
                if (!art.HasRubble)
                    return;
                Mesh mesh = art.GetRubbleMesh(random.Next(1024));
                float yaw = (float)random.NextDouble() * 360f;
                rubble.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * (cell * scale)) });
            }

            float Jitter(float amount) => ((float)random.NextDouble() * 2f - 1f) * amount;
            float Scale() => Mathf.Lerp(1.1f, 1.8f, (float)random.NextDouble());
            float RubbleScale() => Mathf.Lerp(1.4f, 2.2f, (float)random.NextDouble());
            bool IsWall(int x, int y) => board.IsInBounds(new Position(x, y)) && board.GetCell(new Position(x, y)).IsWall;

            // 줄눈 모서리(보드 안쪽 모서리만)
            for (int x = 1; x < board.Width; x++)
            {
                for (int y = 1; y < board.Height; y++)
                {
                    if (random.NextDouble() < art.GrassCornerChance)
                        AddGrass(new Vector3((x - 0.5f) * cell + Jitter(0.02f), GroutDepth, (y - 0.5f) * cell + Jitter(0.02f)), Scale());
                }
            }

            // 두 칸 사이 줄눈: 세로선(x 사이)과 가로선(y 사이)
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (x + 1 < board.Width && random.NextDouble() < art.GrassEdgeChance)
                        AddGrass(new Vector3((x + 0.5f) * cell + Jitter(0.008f), GroutDepth, y * cell + Jitter(0.3f) * cell), Scale() * 0.85f);
                    if (y + 1 < board.Height && random.NextDouble() < art.GrassEdgeChance)
                        AddGrass(new Vector3(x * cell + Jitter(0.3f) * cell, GroutDepth, (y + 0.5f) * cell + Jitter(0.008f)), Scale() * 0.85f);
                }
            }

            // 벽 칸 둘레: 벽 밑동 안쪽 가장자리(벽 칸 바닥 판석 위)에 풀과 돌조각
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (!IsWall(x, y))
                        continue;

                    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        if (IsWall(x + dx, y + dy) || random.NextDouble() >= art.WallDecorChance)
                            continue;

                        float along = Jitter(0.35f) * cell;
                        var edge = new Vector3(x * cell + dx * (half - 0.04f * cell), 0f, y * cell + dy * (half - 0.04f * cell));
                        Vector3 offset = dx != 0 ? new Vector3(0f, 0f, along) : new Vector3(along, 0f, 0f);
                        if (random.NextDouble() < 0.6)
                            AddRubble(edge + offset, RubbleScale());
                        AddGrass(edge + offset * -0.6f + Vector3.up * 0.005f, Scale() * 1.3f);
                        if (random.NextDouble() < 0.6)
                            AddGrass(edge + offset * 0.4f + Vector3.up * 0.005f, Scale() * 1.1f);
                    }
                }
            }

            // 넝쿨(풀과 같은 머티리얼이라 풀 메시에 합친다). 메시는 벽면 z = 0, 바깥 -z 기준이라 -z 가 면의 바깥 방향을 보게 돌린다.
            void AddVine(bool hanging, Vector3 anchor, Vector3 normal, float scale)
            {
                Mesh mesh = hanging ? art.GetHangingVineMesh(random.Next(1024)) : art.GetClimbingVineMesh(random.Next(1024));
                if (mesh == null)
                    return;
                Quaternion rotation = Quaternion.LookRotation(-normal, Vector3.up);
                grass.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(anchor + normal * 0.004f, rotation, Vector3.one * scale) });
            }

            // 벽면 한 자리에 늘어지는 넝쿨/담쟁이를 붙인다. 면 위치와 꼭대기 높이는 그 자리 근처의 벽 메시 정점에서 구한다.
            void AddFaceVines(Vector3[] vertices, Vector3 point, Vector3 normal, float floor, float chance)
            {
                Vector3 along = new Vector3(-normal.z, 0f, normal.x);
                if (!SampleFace(vertices, point, normal, along, 0.12f * cell, out float face, out float top))
                    return;

                Vector3 onFace = point + normal * (face - Vector3.Dot(point, normal));
                float height = top - floor;
                // 벽 높이보다 길어지지 않게 줄인다(너무 작아지면 붙이지 않음)
                float hangScale = Mathf.Min(Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble()), height / (VineHangLength * cell));
                if (art.GetHangingVineMesh(0) != null && random.NextDouble() < chance && hangScale > 0.3f)
                    AddVine(true, new Vector3(onFace.x, top, onFace.z), normal, hangScale * cell);

                float climbScale = Mathf.Min(Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble()), height / (VineClimbHeight * cell));
                if (art.GetClimbingVineMesh(0) != null && random.NextDouble() < chance && climbScale > 0.3f)
                {
                    Vector3 shifted = onFace + along * (Jitter(0.12f) * cell);
                    AddVine(false, new Vector3(shifted.x, floor, shifted.z), normal, climbScale * cell);
                }
            }

            if (art.HasVines && surfaces != null)
            {
                // 벽 칸: 이웃이 벽이 아닌(드러난) 면마다
                if (art.VinesOnWalls && surfaces.WallOf != null)
                {
                    for (int x = 0; x < board.Width; x++)
                    {
                        for (int y = 0; y < board.Height; y++)
                        {
                            MeshFilter wall = IsWall(x, y) ? surfaces.WallOf(new Position(x, y)) : null;
                            if (wall == null || wall.sharedMesh == null)
                                continue;

                            Vector3[] vertices = BoardSpaceVertices(wall, parent);
                            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                if (IsWall(x + dx, y + dy))
                                    continue;
                                var normal = new Vector3(dx, 0f, dy);
                                var along = new Vector3(-dy, 0f, dx);
                                AddFaceVines(vertices, new Vector3(x * cell, 0f, y * cell) + along * (Jitter(0.28f) * cell), normal, 0f, art.WallVineChance);
                            }
                        }
                    }
                }
            }

            var root = new GameObject("Decoration");
            root.transform.SetParent(parent, false);
            if (grass.Count > 0)
                CreatePart(root.transform, "Grass", grass, art.GrassMaterial, ShadowCastingMode.Off);
            if (rubble.Count > 0)
                CreatePart(root.transform, "Rubble", rubble, art.WallMaterial, ShadowCastingMode.On);
            return root;
        }

        /// <summary>메시 정점을 보드 기준(parent 로컬) 좌표로.</summary>
        private static Vector3[] BoardSpaceVertices(MeshFilter filter, Transform parent)
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            Matrix4x4 toBoard = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = toBoard.MultiplyPoint3x4(vertices[i]);
            return vertices;
        }

        /// <summary>
        /// 벽면 한 자리(point 를 지나고 normal 쪽을 향한 면)의 위치와 꼭대기 높이를 그 자리 폭(halfWidth) 안의 정점으로 구한다.
        /// face = 가장 바깥 정점의 normal 방향 거리, top = 면 가까이(깊이 0.15 이내) 정점 중 가장 높은 y. 정점이 없으면 false.
        /// </summary>
        private static bool SampleFace(Vector3[] vertices, Vector3 point, Vector3 normal, Vector3 along, float halfWidth, out float face, out float top)
        {
            face = float.MinValue;
            top = float.MinValue;
            float center = Vector3.Dot(point, along);
            foreach (Vector3 v in vertices)
            {
                if (Mathf.Abs(Vector3.Dot(v, along) - center) <= halfWidth)
                    face = Mathf.Max(face, Vector3.Dot(v, normal));
            }

            if (face == float.MinValue)
                return false;

            foreach (Vector3 v in vertices)
            {
                if (Mathf.Abs(Vector3.Dot(v, along) - center) <= halfWidth && Vector3.Dot(v, normal) > face - 0.15f)
                    top = Mathf.Max(top, v.y);
            }

            return true;
        }

        private static void CreatePart(Transform parent, string name, List<CombineInstance> parts, Material material, ShadowCastingMode shadows)
        {
            int vertexCount = 0;
            foreach (CombineInstance part in parts)
                vertexCount += part.mesh.vertexCount;

            var mesh = new Mesh { name = name, indexFormat = BoardBackdrop.IndexFormatFor(vertexCount) };
            mesh.CombineMeshes(parts.ToArray(), mergeSubMeshes: true, useMatrices: true);
            mesh.UploadMeshData(markNoLongerReadable: true); // 다시 고치지 않으므로 CPU 쪽 사본은 버린다

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }
    }
}
