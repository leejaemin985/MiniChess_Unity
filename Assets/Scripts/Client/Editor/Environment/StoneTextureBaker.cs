using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 돌 표면 텍스처 3장(색, 노멀, 마스크)을 노이즈로 구워 PNG 로 저장한다(StoneSurface 셰이더용). 상하좌우로 이어 붙여도 이음새가 없다.
    /// 마스크: R = 금, G = 움푹한 곳, B = 매끈함 변화.
    /// 화풍을 바꾸려면 아래 수치(색, 노이즈 주기/세기)를 고치고 메뉴를 다시 실행한다. 같은 시드면 같은 결과가 나온다.
    /// </summary>
    public static class StoneTextureBaker
    {
        public const string Folder = "Assets/Art/Environment/Textures";
        public const string AlbedoPath = Folder + "/T_Stone_Albedo.png";
        public const string NormalPath = Folder + "/T_Stone_Normal.png";
        public const string MaskPath = Folder + "/T_Stone_Mask.png";

        private const int Size = 512;
        private const int Seed = 7;

        // 돌 자체 색은 거의 중립인 밝은 회베이지로 둔다. 따뜻함은 햇빛(주광), 보랏빛 그늘은 주변광이 만든다(BoardArt).
        // 그래서 맵마다 햇빛 색/방향을 바꿔도 돌 색은 그대로 쓸 수 있다.
        private static readonly Color Warm = new Color(0.68f, 0.64f, 0.59f);
        private static readonly Color Cool = new Color(0.63f, 0.62f, 0.61f);
        private static readonly Color Mauve = new Color(0.65f, 0.61f, 0.63f);

        [MenuItem("MiniChess/Environment/Bake Stone Textures")]
        public static void Bake()
        {
            EnvironmentAssetUtil.EnsureFolder(Folder);

            var height = new float[Size * Size];
            var crack = new float[Size * Size];
            var albedo = new Color[Size * Size];
            var smooth = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size;
                    float v = (float)y / Size;
                    int i = y * Size + x;

                    // 큰 덩어리 굴곡 + 잔 굴곡
                    float broad = Noise.Fbm(u, v, 3, 4, Seed);
                    float fine = Noise.Fbm(u, v, 16, 3, Seed + 1);

                    // 작은 구멍(풍화된 돌의 패인 자리)
                    Noise.Worley(u, v, 11, Seed + 2, out float pitF1, out _);
                    float pits = Smooth(0.22f, 0f, pitF1) * Smooth(0.05f, 0.25f, Noise.Fbm(u, v, 4, 2, Seed + 3));

                    // 금: 큰 세포 경계(F2 - F1 이 작은 곳) 중 일부만. 굵기도 노이즈로 흔든다.
                    Noise.Worley(u, v, 3, Seed + 4, out float f1, out float f2);
                    float width = 0.012f + 0.01f * Noise.Fbm(u, v, 8, 2, Seed + 5);
                    float jitter = 0.02f * Noise.Fbm(u, v, 24, 2, Seed + 6);
                    float bigCrack = Smooth(width, 0f, f2 - f1 + jitter) * Smooth(0.0f, 0.25f, Noise.Fbm(u, v, 2, 2, Seed + 7));
                    Noise.Worley(u, v, 7, Seed + 8, out float g1, out float g2);
                    float smallCrack = Smooth(0.02f, 0f, g2 - g1 + jitter * 0.6f) * Smooth(0.15f, 0.35f, Noise.Fbm(u, v, 4, 2, Seed + 9)) * 0.6f;
                    float c = Mathf.Clamp01(bigCrack + smallCrack);

                    // 금은 셰이더가 칸마다 골라 그린다(마스크 R). 여기서는 높이에 얕게만 남겨 모든 칸에 금이 보이지 않게 한다.
                    height[i] = 0.5f + broad * 0.35f + fine * 0.12f - pits * 0.08f - c * 0.08f;
                    crack[i] = c;

                    // 색: 큰 얼룩으로 세 색을 섞고, 중간 크기 붓 자국 같은 밝기 변화를 얹는다.
                    float t1 = Mathf.Clamp01(Noise.Fbm(u, v, 2, 3, Seed + 10) * 1.6f + 0.5f);
                    float t2 = Mathf.Clamp01(Noise.Fbm(u, v, 3, 3, Seed + 11) * 1.6f + 0.5f);
                    Color color = Color.Lerp(Color.Lerp(Warm, Cool, t1), Mauve, t2 * 0.5f);
                    float blotch = Noise.Fbm(u, v, 5, 3, Seed + 12);
                    float stain = Smooth(0.1f, 0.35f, Noise.Fbm(u, v, 3, 3, Seed + 13));
                    float speck = Noise.Fbm(u, v, 64, 1, Seed + 14);
                    float shade = 1f + blotch * 0.16f + speck * 0.05f - stain * 0.12f;
                    albedo[i] = color * shade;
                    smooth[i] = Mathf.Clamp01(0.5f + stain * 0.4f - fine * 0.4f);
                }
            }

            // 움푹한 곳: 주변(넓게 흐린 높이)보다 낮은 정도
            float[] blurred = BoxBlur(BoxBlur(height, 6), 6);
            var albedoPixels = new Color[Size * Size];
            var normalPixels = new Color[Size * Size];
            var maskPixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float cavity = Mathf.Clamp01((blurred[i] - height[i]) * 7f);
                    Color a = albedo[i] * (1f - cavity * 0.45f);
                    a.a = 1f;
                    albedoPixels[i] = a;
                    maskPixels[i] = new Color(crack[i], cavity, smooth[i], 1f);

                    // 높이 기울기로 노멀(접선 공간). 가장자리는 반대편과 이어진다.
                    float dx = height[Index(x + 1, y)] - height[Index(x - 1, y)];
                    float dy = height[Index(x, y + 1)] - height[Index(x, y - 1)];
                    var n = new Vector3(-dx * 6f, -dy * 6f, 1f).normalized;
                    normalPixels[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            Save(AlbedoPath, albedoPixels, linear: false);
            Save(NormalPath, normalPixels, linear: true);
            Save(MaskPath, maskPixels, linear: true);
            AssetDatabase.Refresh();

            Configure(AlbedoPath, TextureImporterType.Default, srgb: true);
            Configure(NormalPath, TextureImporterType.NormalMap, srgb: false);
            Configure(MaskPath, TextureImporterType.Default, srgb: false);
            Debug.Log($"[StoneTextureBaker] {Folder} 에 돌 텍스처 3장을 구웠습니다 ({Size}px)");
        }

        private static int Index(int x, int y) => ((y % Size + Size) % Size) * Size + (x % Size + Size) % Size;

        /// <summary>a → b 에서 0 → 1(a > b 면 거꾸로).</summary>
        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>이어 붙는 상자 흐림(가장자리는 반대편으로 이어진다).</summary>
        private static float[] BoxBlur(float[] source, int radius)
        {
            var horizontal = new float[source.Length];
            var result = new float[source.Length];
            float scale = 1f / (radius * 2 + 1);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++)
                        sum += source[Index(x + k, y)];
                    horizontal[y * Size + x] = sum * scale;
                }
            }

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++)
                        sum += horizontal[Index(x, y + k)];
                    result[y * Size + x] = sum * scale;
                }
            }

            return result;
        }

        private static void Save(string path, Color[] pixels, bool linear)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, linear);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void Configure(string path, TextureImporterType type, bool srgb)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = Size;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        /// <summary>주기(period)마다 반복되는 노이즈. u, v 는 0~1 이고 1 에서 0 과 이어진다.</summary>
        private static class Noise
        {
            /// <summary>주기적 그래디언트 노이즈의 합(fBm). 대략 -0.5 ~ 0.5.</summary>
            public static float Fbm(float u, float v, int period, int octaves, int seed)
            {
                float sum = 0, amplitude = 0.5f;
                for (int o = 0; o < octaves; o++)
                {
                    sum += Gradient(u * period, v * period, period, seed + o * 101) * amplitude;
                    period *= 2;
                    amplitude *= 0.5f;
                }

                return sum;
            }

            private static float Gradient(float x, float y, int period, int seed)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                float sx = Fade(fx), sy = Fade(fy);

                float n00 = Dot(x0, y0, period, seed, fx, fy);
                float n10 = Dot(x0 + 1, y0, period, seed, fx - 1, fy);
                float n01 = Dot(x0, y0 + 1, period, seed, fx, fy - 1);
                float n11 = Dot(x0 + 1, y0 + 1, period, seed, fx - 1, fy - 1);
                return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
            }

            private static float Dot(int ix, int iy, int period, int seed, float dx, float dy)
            {
                float angle = Hash(Wrap(ix, period), Wrap(iy, period), seed) * Mathf.PI * 2f;
                return Mathf.Cos(angle) * dx + Mathf.Sin(angle) * dy;
            }

            /// <summary>가장 가까운 점까지의 거리 F1 과 두 번째 F2(세포 크기 1/period 기준으로 0~1 근처).</summary>
            public static void Worley(float u, float v, int period, int seed, out float f1, out float f2)
            {
                float x = u * period, y = v * period;
                int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
                f1 = f2 = float.MaxValue;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int gx = cx + ox, gy = cy + oy;
                        int wx = Wrap(gx, period), wy = Wrap(gy, period);
                        float px = gx + Hash(wx, wy, seed);
                        float py = gy + Hash(wx, wy, seed + 57);
                        float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                        if (d < f1)
                        {
                            f2 = f1;
                            f1 = d;
                        }
                        else if (d < f2)
                        {
                            f2 = d;
                        }
                    }
                }
            }

            private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

            private static int Wrap(int i, int period) => (i % period + period) % period;

            private static float Hash(int x, int y, int seed)
            {
                unchecked
                {
                    uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                    h = (h ^ (h >> 13)) * 1274126177u;
                    h ^= h >> 16;
                    return (h & 0xFFFFFF) / (float)0x1000000;
                }
            }
        }
    }

    internal static class EnvironmentAssetUtil
    {
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
