using System;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 캐릭터 하나의 시각 데이터. 코어 캐릭터 Id 로 유닛과 연결한다(코어는 그림을 모른다).
    /// 얼굴은 일러스트 안의 정사각형 영역(중심 + 한 변 길이)으로 지정하며, 배경이 함께 보여도 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Character Visual", fileName = "CharacterVisual")]
    public class CharacterVisual : ScriptableObject
    {
        [SerializeField, CoreCharacterId] private string characterId;
        [SerializeField] private Sprite illustration;

        [Tooltip("얼굴 중심. 일러스트 기준 0~1, 왼쪽 아래가 (0, 0).")]
        [SerializeField] private Vector2 faceCenter = new Vector2(0.5f, 0.75f);

        [Tooltip("얼굴 정사각형 한 변 길이. 일러스트 가로 길이 대비 0~1.")]
        [SerializeField, Range(0.05f, 1f)] private float faceSize = 0.4f;

        [Tooltip("보드 위 몸체로 쓸 캐릭터 모델 프리팹(피벗은 발밑, 높이 약 1). 비우면 팀 색 캡슐을 쓴다.")]
        [SerializeField] private GameObject bodyPrefab;

        [NonSerialized] private Sprite _faceSprite;

        public string CharacterId => characterId;
        public Sprite Illustration => illustration;

        /// <summary>몸체 모델 프리팹. 지정하지 않았으면 null(캡슐).</summary>
        public GameObject BodyPrefab => bodyPrefab;

        /// <summary>
        /// 얼굴 정사각형(텍스처 픽셀 좌표). 일러스트 밖으로 나가지 않게 안쪽으로 밀어 넣는다.
        /// </summary>
        public Rect GetFaceRect()
        {
            Rect area = illustration.rect;
            float size = Mathf.Min(faceSize * area.width, area.width, area.height);

            float x = area.x + faceCenter.x * area.width - size * 0.5f;
            float y = area.y + faceCenter.y * area.height - size * 0.5f;
            x = Mathf.Clamp(x, area.x, area.xMax - size);
            y = Mathf.Clamp(y, area.y, area.yMax - size);

            return new Rect(x, y, size, size);
        }

        /// <summary>얼굴 영역만 잘라 만든 스프라이트(가로세로 1 유닛). 일러스트가 없으면 null.</summary>
        public Sprite GetFaceSprite()
        {
            if (illustration == null)
                return null;

            if (_faceSprite == null)
            {
                Rect rect = GetFaceRect();
                _faceSprite = Sprite.Create(illustration.texture, rect, new Vector2(0.5f, 0.5f), rect.width);
                _faceSprite.name = $"{name} Face";
            }

            return _faceSprite;
        }

        private void OnValidate()
        {
            // 얼굴 영역이 바뀌면 다음 조회 때 다시 만든다.
            _faceSprite = null;
        }
    }
}
