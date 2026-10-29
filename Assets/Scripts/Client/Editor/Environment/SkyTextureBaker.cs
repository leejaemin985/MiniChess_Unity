using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 하늘 구름 노이즈 텍스처를 구워 M_Sky 에 연결한다. Sky 셰이더가 픽셀마다 노이즈를 계산하지 않고 이 텍스처를 한 번 읽는다(모바일).
    /// 이음새 없이 반복된다. R = 구름 밀도(큰 덩어리), G = 구름 명암(더 잘게). 둘 다 대략 0.2 ~ 0.8.
    /// </summary>
    public static class SkyTextureBaker
    {
        private const int Size = 256;
        private const int Seed = 7100;

        /// <summary>밀도 노이즈의 가장 큰 주기(텍스처 한 장에 들어가는 덩어리 수). Sky 셰이더의 _CloudScale 과 짝이다.</summary>
        private const int DensityPeriod = 4;
        private const int ShadePeriod = 8;

        public static string CloudPath => $"{StoneTextureBaker.Folder}/T_SkyClouds.png";
        private const string SkyMaterialPath = "Assets/Art/Environment/Materials/M_Sky.mat";

        [MenuItem("MiniChess/Environment/Bake Sky Clouds Texture")]
        public static void Bake()
        {
            EnvironmentAssetUtil.EnsureFolder(StoneTextureBaker.Folder);

            var density = new float[Size * Size];
            var shade = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size, v = y / (float)Size;
                    density[y * Size + x] = StoneTextureBaker.Noise.Fbm(u, v, DensityPeriod, 3, Seed);
                    shade[y * Size + x] = StoneTextureBaker.Noise.Fbm(u, v, ShadePeriod, 3, Seed + 50);
                }
            }

            Normalize(density);
            Normalize(shade);

            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(density[i], shade[i], 0f, 1f);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, linear: true);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(CloudPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            // 처음 만드는 파일도 아래에서 바로 불러 머티리얼에 연결할 수 있게 동기로 가져온다.
            AssetDatabase.ImportAsset(CloudPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CloudPath);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = Size;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky != null)
            {
                sky.SetTexture("_CloudMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CloudPath));
                EditorUtility.SetDirty(sky);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[SkyTextureBaker] {CloudPath} 를 구웠습니다 ({Size}px){(sky != null ? ", M_Sky 에 연결" : "")}");
        }

        /// <summary>값 범위를 0.2 ~ 0.8 로 맞춘다(셰이더의 구름 문턱 _Clouds 가 이 범위를 기준으로 한다).</summary>
        private static void Normalize(float[] values)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (float value in values)
            {
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }

            for (int i = 0; i < values.Length; i++)
                values[i] = Mathf.Lerp(0.2f, 0.8f, Mathf.InverseLerp(min, max, values[i]));
        }
    }
}
