using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 맵 씬의 배경 안개(셰이더 전역 값). 하늘/주변광/햇빛/볼륨은 맵 씬 자체에 저장되지만, 이 값은 씬에 저장되지 않는 셰이더 전역 값이라
    /// 이 컴포넌트가 켜질 때 넣고 꺼질 때(맵 씬을 내릴 때) 지운다. 맵 씬마다 하나 둔다.
    ///   _BackdropHazeColor = 안개 색(하늘 지평선 색으로도 쓴다, a = 1 이면 설정됨) → Sky/Backdrop 셰이더
    ///   _BackdropHaze = (안개 시작 거리, 완전히 묻히는 거리, 절벽 깊이, 절벽 바닥의 낮은 안개 진하기) → Backdrop 셰이더
    /// 에디터에서도 동작해 씬 뷰에서 그대로 보인다.
    /// </summary>
    [ExecuteAlways]
    public class MapEnvironment : MonoBehaviour
    {
        private static readonly int HazeColorId = Shader.PropertyToID("_BackdropHazeColor");
        private static readonly int HazeId = Shader.PropertyToID("_BackdropHaze");

        [Tooltip("먼 곳과 보드 아래가 묻히는 안개 색. 하늘 지평선 색으로도 쓴다.")]
        [SerializeField] private Color hazeColor = new Color(0.40f, 0.34f, 0.48f);

        [Tooltip("카메라에서 x 거리부터 안개가 끼기 시작해 y 거리에서 완전히 묻힌다.")]
        [SerializeField] private Vector2 hazeDistance = new Vector2(25f, 320f);

        [Tooltip("보드 아래 낮게 깔린 안개의 진하기(보드 높이에서 0, 숲 바닥에서 이 값).")]
        [SerializeField, Range(0f, 1f)] private float lowMist = 0.4f;

        [Tooltip("보드에서 숲 바닥까지 깊이(월드 단위). 낮은 안개가 이 깊이에서 가장 진하다.")]
        [SerializeField, Min(1f)] private float cliffDepth = 30f;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
                Apply();
        }

        private void OnDisable()
        {
            Shader.SetGlobalColor(HazeColorId, Color.clear);
            Shader.SetGlobalVector(HazeId, Vector4.zero);
        }

        private void Apply()
        {
            Shader.SetGlobalColor(HazeColorId, new Color(hazeColor.r, hazeColor.g, hazeColor.b, 1f));
            Shader.SetGlobalVector(HazeId, new Vector4(hazeDistance.x, hazeDistance.y, cliffDepth, lowMist));
        }
    }
}
