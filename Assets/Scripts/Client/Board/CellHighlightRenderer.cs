using System;
using System.Collections.Generic;
using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 칸 하이라이트를 그린다(MiniChess/CellHighlight 셰이더). 하이라이트된 칸마다 빛나는 테두리 바닥 판을 깔고,
    /// 하이라이트 영역의 바깥 경계(이웃 칸이 하이라이트되지 않은 변)에만 위로 연해지는 세로 막을 세운다.
    /// 그래서 이동 범위 안의 선택 칸처럼 영역 안쪽 칸 사이에는 막이 서지 않는다. 막 색은 그 변을 가진 칸의 색이다.
    /// 오브젝트를 만들지 않고 매 프레임 Graphics.DrawMesh 로 그린다(칸 수가 적고 하이라이트가 자주 바뀐다).
    /// </summary>
    internal sealed class CellHighlightRenderer
    {
        public const string MaterialPath = "Materials/CellHighlight";

        /// <summary>하이라이트 한 종류의 모양.</summary>
        public struct Style
        {
            public Color Color;
            public float Border;
            public float Fill;

            /// <summary>영역 경계에 세로 막을 세우는지(잠깐 보이는 미리보기는 바닥 테두리만).</summary>
            public bool Curtain;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BorderId = Shader.PropertyToID("_Border");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int CurtainId = Shader.PropertyToID("_Curtain");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        /// <summary>네 변: 이웃 칸 방향과 그 변에 세울 막의 회전(막 메시는 xy 평면).</summary>
        private static readonly (int X, int Y, float Yaw)[] Edges = { (0, -1, 0f), (0, 1, 0f), (-1, 0, 90f), (1, 0, 90f) };

        private readonly Material _material;
        private readonly Mesh _floor;
        private readonly Mesh _curtain;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        /// <summary>막 높이(월드 단위, 칸 크기 1 기준).</summary>
        public float CurtainHeight { get; set; } = 0.62f;

        /// <summary>바닥 판 한 변(칸 크기 대비). 판석보다 조금 크게 깔아 테두리가 판석 가장자리에 오게 한다.</summary>
        public float FloorSize { get; set; } = 0.98f;

        /// <summary>바닥 판/막을 칸 윗면보다 띄우는 높이(기울어진 판석에 묻히지 않게).</summary>
        public float Lift { get; set; } = 0.025f;

        public CellHighlightRenderer(Material material)
        {
            _material = material;
            _floor = CreateQuad("CellHighlightFloor", horizontal: true);
            _curtain = CreateQuad("CellHighlightCurtain", horizontal: false);
        }

        /// <param name="cells">하이라이트된 칸과 그 모양. 매 프레임 도는 foreach 가 열거자를 박싱하지 않도록 Dictionary 로 받는다.</param>
        /// <param name="top">칸 윗면 중심(월드).</param>
        public void Draw(Dictionary<Position, Style> cells, Func<Position, Vector3> top, float cellSize, Quaternion boardRotation, int layer)
        {
            if (_material == null || cells.Count == 0)
                return;

            float half = cellSize * FloorSize * 0.5f;
            float height = CurtainHeight * cellSize;
            foreach (KeyValuePair<Position, Style> pair in cells)
            {
                Style style = pair.Value;
                Vector3 center = top(pair.Key) + Vector3.up * Lift;

                SetStyle(style, curtain: false);
                Graphics.DrawMesh(_floor, Matrix4x4.TRS(center, boardRotation, new Vector3(cellSize * FloorSize, 1f, cellSize * FloorSize)),
                    _material, layer, null, 0, _block, false, false);

                if (!style.Curtain)
                    continue;

                SetStyle(style, curtain: true);
                _block.SetFloat(AspectId, height / (half * 2f));
                foreach ((int dx, int dy, float yaw) in Edges)
                {
                    if (cells.ContainsKey(new Position(pair.Key.X + dx, pair.Key.Y + dy)))
                        continue;

                    Vector3 edge = center + boardRotation * new Vector3(dx * half, 0f, dy * half);
                    Graphics.DrawMesh(_curtain, Matrix4x4.TRS(edge, boardRotation * Quaternion.Euler(0f, yaw, 0f), new Vector3(half * 2f, height, 1f)),
                        _material, layer, null, 0, _block, false, false);
                }
            }
        }

        private void SetStyle(Style style, bool curtain)
        {
            _block.SetColor(ColorId, style.Color);
            _block.SetFloat(BorderId, style.Border);
            _block.SetFloat(FillId, style.Fill);
            _block.SetFloat(CurtainId, curtain ? 1f : 0f);
        }

        /// <summary>한 변 1 인 사각형. horizontal 이면 xz 평면(가운데 원점), 아니면 xy 평면(아래 변 가운데 원점, 높이 1).</summary>
        private static Mesh CreateQuad(string name, bool horizontal)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = horizontal
                ? new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) }
                : new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
