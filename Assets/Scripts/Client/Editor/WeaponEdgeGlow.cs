using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 날붙이 무기(Targets 에 등록한 FBX)를 임포트할 때 날 끝 발광 마스크를 정점 색 R 에 구워 넣는다(CharacterToon 셰이더의 _EMISSION 이 쓴다).
    ///   - 날 끝: 자루 끝에 달린 날(낫)의 안쪽 날(자루 아래쪽을 향한 오목한 면)을 따라 일정 폭을 1, 안쪽으로 갈수록 0.
    ///     메시를 길이(가장 긴 축) × 옆(나머지) 평면에 놓고, 자루에서의 거리 구간마다 가장 낮은 정점을 날 끝으로 본다.
    ///     자루에 가까운 장식(가드) 쪽은 서서히 끈다.
    ///   - 보석: 베이스 컬러({이름}_basecolor.png, 같은 폴더)가 선명한 빨강인 정점도 1.
    /// Tripo 메시는 날 끝 색이 텍스처에 안 들어오고 UV 도 잘게 쪼개져 있어서, 텍스처 마스크 대신 메시 위치로 만든다.
    /// </summary>
    public class WeaponEdgeGlow : AssetPostprocessor
    {
        private class Target
        {
            public string Path;
            /// <summary>띠 폭: 그 자리 날 폭(날 끝 ~ 등) 대비 비율.</summary>
            public float BandRatio;
            /// <summary>자루에서 이 거리(무기 길이 대비)부터 켜기 시작해 GuardEnd 에서 다 켠다.</summary>
            public float GuardStart;
            public float GuardEnd;
        }

        private static readonly Target[] Targets =
        {
            new Target { Path = "Assets/Art/Characters/Scythe/Weapon/CrimsonScythe.fbx", BandRatio = 0.1f, GuardStart = 0.045f, GuardEnd = 0.08f },
        };

        /// <summary>자루에서의 거리 구간 크기(무기 길이 대비).</summary>
        private const float BinRatio = 0.01f;
        /// <summary>띠 폭 최소값(무기 길이 대비). 날 끝(뾰족한 곳)에서 폭이 0 으로 줄어도 선이 남게.</summary>
        private const float MinBandRatio = 0.006f;

        private void OnPostprocessModel(GameObject root)
        {
            Target target = System.Array.Find(Targets, t => t.Path == assetPath);
            if (target == null)
                return;

            Color32[] basecolor = LoadBasecolor(out int width, out int height);
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                Bake(filter.sharedMesh, target, basecolor, width, height);
        }

        private Color32[] LoadBasecolor(out int width, out int height)
        {
            string path = $"{Path.GetDirectoryName(assetPath)}/{Path.GetFileNameWithoutExtension(assetPath)}_basecolor.png";
            width = height = 0;
            if (!File.Exists(path))
                return null;

            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            width = texture.width;
            height = texture.height;
            Color32[] pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        private static void Bake(Mesh mesh, Target target, Color32[] basecolor, int width, int height)
        {
            if (mesh == null)
                return;

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            Vector3 size = mesh.bounds.size;

            // 축: 길이 = 가장 긴 축, 두께 = 가장 짧은 축, 옆 = 나머지.
            int along = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int side = 3 - along - thin;
            float length = size[along];
            float min = mesh.bounds.min[along];

            // 자루 위치: 길이 아래쪽 40% 정점의 옆 좌표 중앙값(날이 없는 구간).
            var shaft = new List<float>();
            foreach (Vector3 v in vertices)
            {
                if (v[along] - min < length * 0.4f)
                    shaft.Add(v[side]);
            }
            shaft.Sort();
            float shaftSide = shaft.Count > 0 ? shaft[shaft.Count / 2] : 0f;

            // 날 위치: 자루에서 멀리 나간 정점이 많은 끝(위)과 쪽.
            float topSum = 0f, sideSum = 0f;
            foreach (Vector3 v in vertices)
            {
                float offset = v[side] - shaftSide;
                if (Mathf.Abs(offset) < length * 0.05f)
                    continue;
                topSum += v[along] - (min + length * 0.5f);
                sideSum += offset;
            }
            float up = topSum >= 0f ? 1f : -1f;
            float outward = sideSum >= 0f ? 1f : -1f;

            // 자루 거리 구간마다 날 높이 범위(낮은 쪽 = 안쪽 날 끝).
            float bin = length * BinRatio;
            int binCount = Mathf.CeilToInt(size[side] / bin) + 1;
            var low = new float[binCount];
            var high = new float[binCount];
            for (int i = 0; i < binCount; i++)
            {
                low[i] = float.MaxValue;
                high[i] = float.MinValue;
            }

            var distance = new float[vertices.Length];
            var heights = new float[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                distance[i] = (vertices[i][side] - shaftSide) * outward;
                heights[i] = vertices[i][along] * up;
                if (distance[i] <= 0f)
                    continue;
                int b = Mathf.Min((int)(distance[i] / bin), binCount - 1);
                low[b] = Mathf.Min(low[b], heights[i]);
                high[b] = Mathf.Max(high[b], heights[i]);
            }

            var colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float mask = 0f;
                if (distance[i] > 0f)
                {
                    int b = Mathf.Min((int)(distance[i] / bin), binCount - 1);
                    float edge = low[b];
                    float bladeWidth = high[b] - low[b];
                    float band = Mathf.Max(bladeWidth * target.BandRatio, length * MinBandRatio);
                    mask = 1f - Mathf.SmoothStep(0f, 1f, (heights[i] - edge) / band);
                    mask *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(target.GuardStart * length, target.GuardEnd * length, distance[i]));
                }

                if (basecolor != null && i < uvs.Length && IsGem(basecolor, width, height, uvs[i]))
                    mask = 1f;

                colors[i] = new Color(mask, 0f, 0f, 1f);
            }

            mesh.colors = colors;
        }

        /// <summary>uv 자리 베이스 컬러가 선명한 빨강(보석)인가.</summary>
        private static bool IsGem(Color32[] pixels, int width, int height, Vector2 uv)
        {
            int x = Mathf.Clamp((int)(Mathf.Repeat(uv.x, 1f) * width), 0, width - 1);
            int y = Mathf.Clamp((int)(Mathf.Repeat(uv.y, 1f) * height), 0, height - 1);
            Color32 c = pixels[y * width + x];
            return c.r > 140 && c.g < 70 && c.b < 70;
        }
    }
}
