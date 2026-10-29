using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MiniChess.Client.Editor.Environment
{
    /// <summary>
    /// 바닥 셰이더 확인용 씬을 만든다: 7x7 판석(칸 중심 = 정수 좌표, 윗면 y = 0) + 줄눈 바닥 + 그림자 확인용 기둥 2개,
    /// 시안 분위기의 조명(노을빛 주광, 보랏빛 주변광)과 시안 구도의 카메라. 텍스처/메시/머티리얼이 없으면 먼저 만든다.
    /// 다시 실행하면 씬을 새로 만들어 덮어쓴다(머티리얼 값은 유지).
    /// </summary>
    public static class FloorTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Test/FloorShaderTest.unity";
        public const string MaterialFolder = "Assets/Art/Environment/Materials";
        public const string StoneMaterialPath = MaterialFolder + "/M_StoneFloor.mat";
        public const string BedMaterialPath = MaterialFolder + "/M_FloorBed.mat";
        public const string VolumeProfilePath = "Assets/Scenes/Test/VP_FloorTest.asset";

        private const int BoardSize = 7;

        [MenuItem("MiniChess/Environment/Build Floor Test Scene")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(StoneTextureBaker.AlbedoPath) == null)
                StoneTextureBaker.Bake();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(StoneSlabMeshBuilder.PathOf(0)) == null)
                StoneSlabMeshBuilder.BuildAll();

            Material stone = StoneMaterial();
            Material bed = BedMaterial();

            EnvironmentAssetUtil.EnsureFolder("Assets/Scenes/Test");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var random = new System.Random(1);
            var board = new GameObject("Board");
            for (int y = 0; y < BoardSize; y++)
            {
                for (int x = 0; x < BoardSize; x++)
                {
                    var slab = new GameObject($"Slab {x},{y}");
                    slab.transform.SetParent(board.transform, false);
                    slab.transform.localPosition = new Vector3(x, Jitter(random, 0.008f), y);
                    slab.transform.localRotation = Quaternion.Euler(Jitter(random, 0.6f), random.Next(4) * 90f, Jitter(random, 0.6f));
                    slab.AddComponent<MeshFilter>().sharedMesh =
                        AssetDatabase.LoadAssetAtPath<Mesh>(StoneSlabMeshBuilder.PathOf(random.Next(StoneSlabMeshBuilder.VariantCount)));
                    slab.AddComponent<MeshRenderer>().sharedMaterial = stone;
                }
            }

            // 줄눈 사이로 보이는 어두운 바닥
            GameObject floorBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorBed.name = "Floor Bed";
            floorBed.transform.SetParent(board.transform, false);
            floorBed.transform.localPosition = new Vector3((BoardSize - 1) * 0.5f, -0.17f, (BoardSize - 1) * 0.5f);
            floorBed.transform.localScale = new Vector3(BoardSize + 0.2f, 0.1f, BoardSize + 0.2f);
            floorBed.GetComponent<MeshRenderer>().sharedMaterial = bed;
            Object.DestroyImmediate(floorBed.GetComponent<Collider>());

            // 그림자 확인용 기둥(판석 메시를 세로로 늘림)
            foreach (Vector2 cell in new[] { new Vector2(2, 4), new Vector2(4, 2) })
            {
                var pillar = new GameObject("Pillar");
                pillar.transform.SetParent(board.transform, false);
                pillar.transform.localPosition = new Vector3(cell.x, 0.7f, cell.y);
                pillar.transform.localScale = new Vector3(0.6f, 3.5f, 0.6f);
                pillar.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(StoneSlabMeshBuilder.PathOf(1));
                pillar.AddComponent<MeshRenderer>().sharedMaterial = stone;
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.87f, 0.72f);
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32f, 225f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.38f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.30f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.09f, 0.09f);
            RenderSettings.skybox = null;

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.27f, 0.40f);
            camera.fieldOfView = 40f;
            camera.transform.position = new Vector3(3f, 7f, -3.4f);
            camera.transform.LookAt(new Vector3(3f, 0f, 3.2f));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var volume = new GameObject("Post Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[FloorTestSceneBuilder] {ScenePath} 를 만들었습니다");
        }

        private static float Jitter(System.Random random, float amount) => ((float)random.NextDouble() * 2f - 1f) * amount;

        private static Material StoneMaterial()
        {
            EnvironmentAssetUtil.EnsureFolder(MaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(StoneMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("MiniChess/StoneSurface"));
                AssetDatabase.CreateAsset(material, StoneMaterialPath);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(StoneTextureBaker.AlbedoPath));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(StoneTextureBaker.NormalPath));
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(StoneTextureBaker.MaskPath));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 확인용 후처리: 톤매핑(밝은 곳이 하얗게 뭉개지지 않게), 약한 대비, 약한 블룸.
        /// 실제 전투 씬의 후처리(GlobalVolumeProfile)에는 아직 블룸만 있다. 맵을 적용할 때 같이 맞춘다.
        /// </summary>
        private static VolumeProfile PostProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null)
                return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.ACES;
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.35f;
            color.contrast.value = 10f;
            color.saturation.value = -12f;
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 0.3f;
            bloom.threshold.value = 1f;

            foreach (VolumeComponent component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static Material BedMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BedMaterialPath);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.13f, 0.11f, 0.10f));
            material.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(material, BedMaterialPath);
            return material;
        }
    }
}
