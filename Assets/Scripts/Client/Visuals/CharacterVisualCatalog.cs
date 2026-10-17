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

        public IReadOnlyList<CharacterVisual> Visuals => visuals;

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
