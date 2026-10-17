using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 전체 캐릭터 시각 데이터 목록. 코어 캐릭터 Id 로 찾는다.
    /// GameSession 에 직접 연결하거나, Resources/CharacterVisualCatalog 에 두면 자동으로 불러온다.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniChess/Character Visual Catalog", fileName = "CharacterVisualCatalog")]
    public class CharacterVisualCatalog : ScriptableObject
    {
        public const string ResourcePath = "CharacterVisualCatalog";

        [SerializeField] private List<CharacterVisual> visuals = new List<CharacterVisual>();

        [Header("Face Shape (비우면 원)")]
        [Tooltip("얼굴을 자를 모양. 흰색/불투명 부분만 보인다. 정사각형 스프라이트 권장.")]
        [SerializeField] private Sprite faceMask;
        [Tooltip("얼굴 뒤 테두리 모양. 팀 색으로 칠해진다. 정사각형 스프라이트 권장.")]
        [SerializeField] private Sprite faceFrame;

        [Header("Action Icons")]
        [Tooltip("기본 공격(유닛/장애물) 때 공격자 얼굴 위에 잠깐 띄우는 아이콘.")]
        [SerializeField] private Sprite basicAttackIcon;
        [Tooltip("스킬 시전 때 시전자 얼굴 위에 잠깐 띄우는 아이콘(모든 스킬 공통).")]
        [SerializeField] private Sprite skillCastIcon;

        public IReadOnlyList<CharacterVisual> Visuals => visuals;

        /// <summary>얼굴 마스크 모양. 지정하지 않았으면 null(FaceBadge 가 원을 쓴다).</summary>
        public Sprite FaceMask => faceMask;

        /// <summary>얼굴 테두리 모양. 지정하지 않았으면 null(FaceBadge 가 원을 쓴다).</summary>
        public Sprite FaceFrame => faceFrame;

        /// <summary>기본 공격 아이콘. 지정하지 않았으면 null(표시 안 함).</summary>
        public Sprite BasicAttackIcon => basicAttackIcon;

        /// <summary>스킬 시전 아이콘. 지정하지 않았으면 null(표시 안 함).</summary>
        public Sprite SkillCastIcon => skillCastIcon;

        /// <summary>캐릭터 Id 의 시각 데이터. 없으면 null.</summary>
        public CharacterVisual Find(string characterId)
        {
            foreach (CharacterVisual visual in visuals)
            {
                if (visual != null && visual.CharacterId == characterId)
                    return visual;
            }

            return null;
        }

        /// <summary>캐릭터 Id 의 얼굴 스프라이트. 데이터나 일러스트가 없으면 null.</summary>
        public Sprite FindFace(string characterId)
        {
            return Find(characterId)?.GetFaceSprite();
        }
    }
}
