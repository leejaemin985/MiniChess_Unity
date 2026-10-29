using UnityEngine;
using UnityEngine.Rendering;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 보드 그림 재료와 분위기(조명). BoardView 가 칸을 만들 때 쓰고, 보드를 만들 때 조명을 씬에 적용한다.
    /// 판석 메시는 칸 크기 1 기준(윗면 y = 0, 두께 SlabThickness)이며 칸 크기에 맞춰 가로/세로로 늘린다.
    /// 나중에 맵마다 다른 분위기를 쓰려면 이 에셋을 맵별로 두면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Board Art", fileName = "BoardArt")]
    public class BoardArt : ScriptableObject
    {
        public const string ResourcePath = "BoardArt";

        /// <summary>판석 메시의 두께(StoneSlabMeshBuilder 와 같은 값).</summary>
        public const float SlabThickness = 0.2f;

        [Header("Floor")]
        [Tooltip("판석 메시 변형들. 칸마다 칸 좌표로 하나를 고른다.")]
        [SerializeField] private Mesh[] slabMeshes;
        [SerializeField] private Material floorMaterial;
        [Tooltip("판석 사이 줄눈으로 보이는 보드 아래 바닥.")]
        [SerializeField] private Material bedMaterial;
        [SerializeField] private Material wallMaterial;
        [Tooltip("판석 높이를 칸마다 흔드는 폭(월드 단위).")]
        [SerializeField, Min(0f)] private float heightJitter = 0.008f;
        [Tooltip("판석 기울기를 칸마다 흔드는 폭(도).")]
        [SerializeField, Min(0f)] private float tiltJitter = 0.6f;

        [Header("Lighting")]
        [SerializeField] private bool applyLighting = true;
        [SerializeField] private Color sunColor = new Color(1f, 0.87f, 0.72f);
        [SerializeField] private float sunIntensity = 1.9f;
        [SerializeField] private Vector3 sunRotation = new Vector3(32f, 225f, 0f);
        [SerializeField] private Color ambientSky = new Color(0.40f, 0.38f, 0.52f);
        [SerializeField] private Color ambientEquator = new Color(0.32f, 0.30f, 0.34f);
        [SerializeField] private Color ambientGround = new Color(0.10f, 0.09f, 0.09f);

        public Material FloorMaterial => floorMaterial;
        public Material BedMaterial => bedMaterial;
        public Material WallMaterial => wallMaterial != null ? wallMaterial : floorMaterial;
        public float HeightJitter => heightJitter;
        public float TiltJitter => tiltJitter;

        public Mesh GetSlabMesh(int index)
        {
            if (slabMeshes == null || slabMeshes.Length == 0)
                return null;
            return slabMeshes[Mathf.Abs(index) % slabMeshes.Length];
        }

        /// <summary>씬의 주광(방향광)과 주변광을 이 분위기로 바꾼다.</summary>
        public void ApplyLighting()
        {
            if (!applyLighting)
                return;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;

            Light sun = RenderSettings.sun != null ? RenderSettings.sun : FindSun();
            if (sun == null)
                return;

            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(sunRotation);
        }

        private static Light FindSun()
        {
            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                    return light;
            }

            return null;
        }
    }
}
