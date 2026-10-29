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
    ///   - 풀: 네 칸이 만나는 줄눈 모서리, 두 칸 사이 줄눈, 벽 칸 둘레, 보드 가장자리(외곽 벽 밑).
    ///   - 돌조각: 벽 칸 둘레(벽 칸 안쪽 가장자리), 보드 가장자리.
    /// 칸 좌표로 정하므로 같은 맵은 항상 같은 모습이다. 줄눈의 풀은 판석 틈(윗면 아래)에서 자라 나온다.
    /// </summary>
    internal static class BoardDecorator
    {
        /// <summary>줄눈 풀의 뿌리 높이(판석 윗면 아래, 틈 속).</summary>
        private const float GroutDepth = -0.05f;

        public static GameObject Build(Transform parent, CoreBoard board, BoardCoordinates coordinates, BoardArt art)
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

            // 줄눈 모서리(보드 안쪽 모서리만. 바깥 둘레는 가장자리 장식이 맡는다)
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

            // 보드 가장자리: 외곽 벽 밑 줄눈(보드 바깥 경계선)
            for (int i = 0; i < board.Width; i++)
            {
                AddEdge(new Vector3(i * cell, 0f, -half), true);
                AddEdge(new Vector3(i * cell, 0f, (board.Height - 1) * cell + half), true);
            }

            for (int i = 0; i < board.Height; i++)
            {
                AddEdge(new Vector3(-half, 0f, i * cell), false);
                AddEdge(new Vector3((board.Width - 1) * cell + half, 0f, i * cell), false);
            }

            void AddEdge(Vector3 center, bool alongX)
            {
                if (random.NextDouble() >= art.BorderDecorChance)
                    return;

                float along = Jitter(0.4f) * cell;
                Vector3 position = center + (alongX ? new Vector3(along, 0f, 0f) : new Vector3(0f, 0f, along));
                if (random.NextDouble() < 0.45)
                    AddRubble(position + Vector3.up * -0.02f, RubbleScale());
                Vector3 beside = alongX ? new Vector3(Jitter(0.15f) * cell, 0f, 0f) : new Vector3(0f, 0f, Jitter(0.15f) * cell);
                AddGrass(position + beside + Vector3.up * GroutDepth, Scale() * 1.4f);
            }

            var root = new GameObject("Decoration");
            root.transform.SetParent(parent, false);
            if (grass.Count > 0)
                CreatePart(root.transform, "Grass", grass, art.GrassMaterial, ShadowCastingMode.Off);
            if (rubble.Count > 0)
                CreatePart(root.transform, "Rubble", rubble, art.WallMaterial, ShadowCastingMode.On);
            return root;
        }

        private static void CreatePart(Transform parent, string name, List<CombineInstance> parts, Material material, ShadowCastingMode shadows)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), mergeSubMeshes: true, useMatrices: true);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }
    }
}
