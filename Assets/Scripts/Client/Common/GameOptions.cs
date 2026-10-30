using System;
using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>
    /// 클라이언트 표시 옵션. Resources/GameOptions 에 하나 두고 GameSession 이 읽는다(비어 있으면 코드 기본값).
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Game Options", fileName = "GameOptions")]
    public class GameOptions : ScriptableObject
    {
        public const string ResourcePath = "GameOptions";

        [SerializeField] private CameraSettings camera = new CameraSettings();

        public CameraSettings Camera => camera;
    }

    /// <summary>보드 카메라 기본 배치와 조작(회전/기울기/확대/이동) 범위.</summary>
    [Serializable]
    public class CameraSettings
    {
        [Tooltip("기본 시점의 내려다보는 각도(도). 90 이면 수직으로 내려다본다.")]
        [SerializeField, Range(20f, 90f)] private float pitch = 60f;

        [Tooltip("보드가 화면에 꽉 차지 않게 두는 여유 배율. 1 이면 보드 외접원이 화면에 딱 맞는다.")]
        [SerializeField, Min(0.5f)] private float padding = 1.1f;

        [Tooltip("시야각(도). 0 이면 카메라에 설정된 값을 그대로 쓴다.")]
        [SerializeField, Range(0f, 120f)] private float fieldOfView;

        [Tooltip("이 화면의 팀 진영 쪽에서 보드를 보게 돌린다. 끄면 항상 Player1 쪽에서 본다.")]
        [SerializeField] private bool faceLocalTeam = true;

        [Header("조작")]
        [Tooltip("가장 눕힐 수 있는 각도(도). 낮을수록 큰 석조물이 판을 가린다.")]
        [SerializeField, Range(20f, 90f)] private float minPitch = 45f;

        [Tooltip("가장 세울 수 있는 각도(도).")]
        [SerializeField, Range(20f, 90f)] private float maxPitch = 80f;

        [Tooltip("가장 가까이 당겼을 때 화면의 좁은 쪽(세로 화면이면 가로)에 들어오는 칸 수. 가장 멀리는 기본 시점(판 전체).")]
        [SerializeField, Min(1f)] private float minVisibleCells = 3f;

        [Tooltip("화면 폭만큼 끌었을 때 판 둘레로 도는 각도(도).")]
        [SerializeField, Min(0f)] private float orbitSpeed = 270f;

        [Tooltip("화면 높이만큼 끌었을 때 기울어지는 각도(도).")]
        [SerializeField, Min(0f)] private float pitchSpeed = 90f;

        [Tooltip("마우스 휠 한 칸에 당겨지는 비율.")]
        [SerializeField, Range(0.01f, 0.5f)] private float wheelZoomStep = 0.12f;

        [Tooltip("카메라가 목표 시점을 따라가는 시간(초). 0 이면 바로 따라간다.")]
        [SerializeField, Min(0f)] private float smoothing = 0.08f;

        public float Pitch => pitch;
        public float Padding => padding;
        public float FieldOfView => fieldOfView;
        public bool FaceLocalTeam => faceLocalTeam;
        public float MinPitch => Mathf.Min(minPitch, maxPitch);
        public float MaxPitch => Mathf.Max(minPitch, maxPitch);
        public float MinVisibleCells => minVisibleCells;
        public float OrbitSpeed => orbitSpeed;
        public float PitchSpeed => pitchSpeed;
        public float WheelZoomStep => wheelZoomStep;
        public float Smoothing => smoothing;
    }
}
