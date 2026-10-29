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

        private static readonly int HazeColorId = Shader.PropertyToID("_BackdropHazeColor");
        private static readonly int HazeId = Shader.PropertyToID("_BackdropHaze");

        [Header("Floor")]
        [Tooltip("온전한 판석 메시 변형들. 칸마다 칸 좌표로 하나를 고른다.")]
        [SerializeField] private Mesh[] slabMeshes;
        [Tooltip("깨진 판석 메시 변형들(조각마다 높이/기울기가 다름).")]
        [SerializeField] private Mesh[] brokenSlabMeshes;
        [Tooltip("일반 바닥 칸 중 깨진 판석을 쓰는 비율. 시작 구역/점령 칸은 항상 온전한 판석.")]
        [SerializeField, Range(0f, 1f)] private float brokenChance = 0.18f;
        [SerializeField] private Material floorMaterial;
        [Tooltip("판석 사이 줄눈으로 보이는 보드 아래 바닥.")]
        [SerializeField] private Material bedMaterial;
        [Tooltip("판석 높이를 칸마다 흔드는 폭(월드 단위).")]
        [SerializeField, Min(0f)] private float heightJitter = 0.008f;
        [Tooltip("판석 기울기를 칸마다 흔드는 폭(도).")]
        [SerializeField, Min(0f)] private float tiltJitter = 0.6f;
        [Tooltip("일반 바닥 칸 중 판석이 통째로 내려앉은 칸의 비율.")]
        [SerializeField, Range(0f, 1f)] private float sinkChance = 0.1f;
        [Tooltip("내려앉은 판석의 깊이(월드 단위, 유닛/표식이 묻히지 않게 2~3cm 이내).")]
        [SerializeField, Min(0f)] private float sinkDepth = 0.015f;
        [Tooltip("내려앉은 판석의 기울기 폭(도).")]
        [SerializeField, Min(0f)] private float sinkTilt = 1.8f;

        [Header("Walls")]
        [Tooltip("벽 칸 메시 변형들(바닥 판석 위에 선다).")]
        [SerializeField] private Mesh[] wallMeshes;
        [SerializeField] private Material wallMaterial;
        [Tooltip("보드 둘레 외곽 벽 조각(원점 중심, x 방향 길이 1). 비우면 외곽 벽을 그리지 않는다.")]
        [SerializeField] private Mesh[] borderMeshes;
        [SerializeField] private Mesh borderCornerMesh;
        [Tooltip("외곽 벽 조각 중심이 보드 가장자리에서 떨어진 거리.")]
        [SerializeField] private float borderOffset = 0.23f;

        [Header("Decoration (풀/돌조각, BoardDecorator)")]
        [Tooltip("풀 덤불 메시 변형들(뿌리 y = 0).")]
        [SerializeField] private Mesh[] grassMeshes;
        [SerializeField] private Material grassMaterial;
        [Tooltip("돌조각 무더기 메시 변형들(돌 머티리얼 = 벽 머티리얼로 그린다).")]
        [SerializeField] private Mesh[] rubbleMeshes;
        [Tooltip("네 칸이 만나는 줄눈 모서리마다 풀이 날 확률.")]
        [SerializeField, Range(0f, 1f)] private float grassCornerChance = 0.65f;
        [Tooltip("두 칸 사이 줄눈마다 풀이 날 확률.")]
        [SerializeField, Range(0f, 1f)] private float grassEdgeChance = 0.3f;
        [Tooltip("벽 칸의 변마다 풀/돌조각이 붙을 확률.")]
        [SerializeField, Range(0f, 1f)] private float wallDecorChance = 0.9f;
        [Tooltip("보드 가장자리(외곽 벽 밑) 칸마다 풀/돌조각이 붙을 확률.")]
        [SerializeField, Range(0f, 1f)] private float borderDecorChance = 0.75f;

        [Header("Vines (넝쿨, BoardDecorator)")]
        [Tooltip("벽 꼭대기에서 늘어지는 넝쿨 메시 변형들(원점 = 꼭대기 모서리, 벽면 z = 0, 바깥 -z). 풀 머티리얼로 그린다.")]
        [SerializeField] private Mesh[] hangingVineMeshes;
        [Tooltip("벽 밑동에서 타고 오르는 담쟁이 메시 변형들(원점 = 밑동, 벽면 z = 0, 바깥 -z).")]
        [SerializeField] private Mesh[] climbingVineMeshes;
        [SerializeField] private bool vinesOnWalls = true;
        [SerializeField] private bool vinesOnBorder = true;
        [SerializeField] private bool vinesOnCliff = true;
        [Tooltip("벽 칸의 드러난 면마다 늘어지는 넝쿨 / 담쟁이가 각각 붙을 확률.")]
        [SerializeField, Range(0f, 1f)] private float wallVineChance = 0.55f;
        [Tooltip("외곽 벽 조각의 안/바깥 면마다 늘어지는 넝쿨 / 담쟁이가 각각 붙을 확률.")]
        [SerializeField, Range(0f, 1f)] private float borderVineChance = 0.25f;
        [Tooltip("절벽 가장자리를 따라 일정 간격마다 넝쿨이 늘어질 확률.")]
        [SerializeField, Range(0f, 1f)] private float cliffVineChance = 0.4f;

        [Header("Props (보드 밖 석조물, BoardProps)")]
        [Tooltip("외곽 벽 바깥 땅에 세우는 석조물. 보드 안 벽 칸에는 쓰지 않는다(유닛/하이라이트를 가리지 않게).")]
        [SerializeField] private PropPlacement[] props;

        [Header("Background (하늘/절벽/숲/산, BoardBackdrop)")]
        [SerializeField] private bool applyBackground = true;
        [Tooltip("하늘 스카이박스 머티리얼. 비우면 카메라 배경을 backgroundColor 단색으로 칠한다.")]
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Color backgroundColor = new Color(0.38f, 0.31f, 0.45f);
        [Tooltip("절벽/숲/산을 그리는 머티리얼(Backdrop 셰이더). 비우면 보드 바깥 배경을 만들지 않는다.")]
        [SerializeField] private Material backdropMaterial;
        [Tooltip("먼 곳과 절벽 아래가 묻히는 안개 색. 하늘 지평선 색으로도 쓴다.")]
        [SerializeField] private Color hazeColor = new Color(0.40f, 0.34f, 0.48f);
        [Tooltip("카메라에서 x 거리부터 안개가 끼기 시작해 y 거리에서 완전히 묻힌다.")]
        [SerializeField] private Vector2 hazeDistance = new Vector2(25f, 320f);
        [Tooltip("절벽 아래 낮게 깔린 안개의 진하기(보드 높이에서 0, 숲 바닥에서 이 값).")]
        [SerializeField, Range(0f, 1f)] private float lowMist = 0.4f;
        [Tooltip("모서리 단 판석 메시(저폴리). 보드 판석과 같은 돌 머티리얼로 그린다.")]
        [SerializeField] private Mesh[] pavingMeshes;
        [Tooltip("성벽(벽돌 무늬를 그리는 Masonry 셰이더).")]
        [SerializeField] private Material masonryMaterial;
        [Tooltip("보드에서 숲 바닥까지 성벽 높이(월드 단위).")]
        [SerializeField, Min(1f)] private float cliffDepth = 14f;
        [SerializeField, Min(0)] private int treeCount = 500;
        [Tooltip("나무가 자라는 범위(보드 중심에서의 거리).")]
        [SerializeField, Min(10f)] private float forestRadius = 90f;

        [Header("Lighting")]
        [Tooltip("돌 색은 거의 중립이고, 분위기는 여기서 정한다: 햇빛 = 밝은 면의 따뜻함, 주변광 = 그늘의 보랏빛.")]
        [SerializeField] private bool applyLighting = true;
        [SerializeField] private Color sunColor = new Color(1f, 0.88f, 0.74f);
        [SerializeField] private float sunIntensity = 2.3f;
        [Tooltip("기본은 앞 왼쪽에서 비추는 빛(그림자가 뒤 오른쪽으로). 카메라 쪽 벽면이 밝게 보인다.")]
        [SerializeField] private Vector3 sunRotation = new Vector3(38f, 40f, 0f);
        [SerializeField] private Color ambientSky = new Color(0.50f, 0.42f, 0.62f);
        [SerializeField] private Color ambientEquator = new Color(0.44f, 0.37f, 0.47f);
        [SerializeField] private Color ambientGround = new Color(0.14f, 0.11f, 0.13f);

        public Material FloorMaterial => floorMaterial;
        public Material BedMaterial => bedMaterial;
        public Material WallMaterial => wallMaterial != null ? wallMaterial : floorMaterial;
        public float HeightJitter => heightJitter;
        public float TiltJitter => tiltJitter;
        public float BrokenChance => brokenChance;
        public float SinkChance => sinkChance;
        public float SinkDepth => sinkDepth;
        public float SinkTilt => sinkTilt;
        public bool HasBrokenSlabs => brokenSlabMeshes != null && brokenSlabMeshes.Length > 0;
        public Mesh BorderCornerMesh => borderCornerMesh;
        public bool HasBorder => borderMeshes != null && borderMeshes.Length > 0;
        public float BorderOffset => borderOffset;

        public Material GrassMaterial => grassMaterial;
        public bool HasGrass => grassMaterial != null && grassMeshes != null && grassMeshes.Length > 0;
        public bool HasRubble => rubbleMeshes != null && rubbleMeshes.Length > 0;
        public float GrassCornerChance => grassCornerChance;
        public float GrassEdgeChance => grassEdgeChance;
        public float WallDecorChance => wallDecorChance;
        public float BorderDecorChance => borderDecorChance;
        public Mesh GetGrassMesh(int index) => Pick(grassMeshes, index);
        public Mesh GetRubbleMesh(int index) => Pick(rubbleMeshes, index);

        public bool HasVines => grassMaterial != null && (HasMeshes(hangingVineMeshes) || HasMeshes(climbingVineMeshes));
        public bool VinesOnWalls => vinesOnWalls;
        public bool VinesOnBorder => vinesOnBorder;
        public bool VinesOnCliff => vinesOnCliff;
        public float WallVineChance => wallVineChance;
        public float BorderVineChance => borderVineChance;
        public float CliffVineChance => cliffVineChance;
        public Mesh GetHangingVineMesh(int index) => Pick(hangingVineMeshes, index);
        public Mesh GetClimbingVineMesh(int index) => Pick(climbingVineMeshes, index);
        private static bool HasMeshes(Mesh[] meshes) => meshes != null && meshes.Length > 0;

        public PropPlacement[] Props => props;

        /// <summary>
        /// 석조물 하나의 자리. 맵 크기가 달라도 따라가도록 보드 외곽 직사각형 기준으로 정한다.
        /// 위치 = 기준점(anchor, 0~1) + 바깥으로 떨어진 거리(offset, 칸 단위). 높이는 외곽 벽 바깥 땅.
        /// </summary>
        [System.Serializable]
        public struct PropPlacement
        {
            public GameObject prefab;
            [Tooltip("보드 외곽 직사각형 위 기준점. (0,0) = 왼쪽-가까운(Player1 쪽) 모서리, (1,1) = 오른쪽-먼 모서리.")]
            public Vector2 anchor;
            [Tooltip("기준점에서 떨어진 거리(칸 단위, x/z). 바깥쪽이 되게 부호를 준다.")]
            public Vector2 offset;
            [Tooltip("y 회전(도).")]
            public float yaw;
            [Tooltip("크기 배율(0 이면 1).")]
            public float scale;
        }

        public Material BackdropMaterial => applyBackground ? backdropMaterial : null;
        public Material MasonryMaterial => masonryMaterial;
        public bool HasPavingMeshes => HasMeshes(pavingMeshes);
        public Mesh GetPavingMesh(int index) => Pick(pavingMeshes, index);
        public float CliffDepth => cliffDepth;
        public int TreeCount => treeCount;
        public float ForestRadius => forestRadius;

        public Mesh GetSlabMesh(int index) => Pick(slabMeshes, index);
        public Mesh GetBrokenSlabMesh(int index) => Pick(brokenSlabMeshes, index);
        public Mesh GetWallMesh(int index) => Pick(wallMeshes, index);
        public Mesh GetBorderMesh(int index) => Pick(borderMeshes, index);

        private static Mesh Pick(Mesh[] meshes, int index)
        {
            if (meshes == null || meshes.Length == 0)
                return null;
            return meshes[Mathf.Abs(index) % meshes.Length];
        }

        /// <summary>씬의 주광(방향광)과 주변광, 하늘(또는 카메라 배경색), 배경 안개를 이 분위기로 바꾼다.</summary>
        public void ApplyLighting()
        {
            if (applyBackground)
            {
                if (skyMaterial != null)
                    RenderSettings.skybox = skyMaterial;
                if (Camera.main != null)
                {
                    Camera.main.clearFlags = skyMaterial != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                    Camera.main.backgroundColor = backgroundColor;
                }

                // Sky/Backdrop 셰이더가 읽는 전역 값(a = 1 이면 설정됨)
                Shader.SetGlobalColor(HazeColorId, new Color(hazeColor.r, hazeColor.g, hazeColor.b, 1f));
                Shader.SetGlobalVector(HazeId, new Vector4(hazeDistance.x, hazeDistance.y, cliffDepth, lowMist));
            }

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
