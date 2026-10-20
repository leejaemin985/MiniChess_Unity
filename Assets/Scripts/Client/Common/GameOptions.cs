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

    /// <summary>보드 카메라 기본 배치.</summary>
    [Serializable]
    public class CameraSettings
    {
        [Tooltip("내려다보는 각도(도). 90 이면 수직으로 내려다본다.")]
        [SerializeField, Range(20f, 90f)] private float pitch = 60f;

        [Tooltip("보드가 화면에 꽉 차지 않게 두는 여유 배율. 1 이면 보드 외접원이 화면에 딱 맞는다.")]
        [SerializeField, Min(0.5f)] private float padding = 1.1f;

        [Tooltip("시야각(도). 0 이면 카메라에 설정된 값을 그대로 쓴다.")]
        [SerializeField, Range(0f, 120f)] private float fieldOfView;

        [Tooltip("이 화면의 팀 진영 쪽에서 보드를 보게 돌린다. 끄면 항상 Player1 쪽에서 본다.")]
        [SerializeField] private bool faceLocalTeam = true;

        public float Pitch => pitch;
        public float Padding => padding;
        public float FieldOfView => fieldOfView;
        public bool FaceLocalTeam => faceLocalTeam;
    }
}
