using System;
using MiniChess.Client.Visuals.Effects;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 몸체 곁에 붙어 다니는 오브젝트 하나. 위치/회전은 몸체 루트(발밑 가운데, 앞 = +z) 기준이며 모델의 기본 자세(바인드 포즈)에서 잰다.
    /// followBone 이면 그 자리에서 bone 에 붙여 뼈를 따라 움직인다(예: 허리춤 권총 → Hips). 아니면 몸체 루트에 고정(예: 떠다니는 불 정령).
    /// </summary>
    [Serializable]
    public struct BodyCompanion
    {
        public GameObject prefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public bool followBone;
        public HumanBodyBones bone;
    }

    /// <summary>
    /// 상태에 따른 자세 바꾸기(StatusStance). statusId 가 비어 있으면 쓰지 않는다. 위치/회전은 모두 몸체 루트(칸 가운데 발밑, 앞 = +z) 기준.
    /// 그 상태인 동안:
    ///   - 손에 쥔 무기를 stowBone 아래 보관 지점으로 옮긴다(위치/회전은 모델의 기본 자세(바인드 포즈)에서 잰다. 무기 루트(손잡이 지점)가 이 지점에 겹친다).
    ///   - 설치물(deployPrefab, 예: 박격포)을 칸에 보이고, 캐릭터 모델을 bodyPosition 으로 옮겨 bodyYaw 방향을 보게 한다.
    ///   - 보조 대기(AltIdle)를 재생한다.
    /// </summary>
    [Serializable]
    public struct StatusStanceSettings
    {
        [CoreStatusId] public string statusId;
        public HumanBodyBones stowBone;
        public Vector3 stowPosition;
        public Vector3 stowEulerAngles;
        public GameObject deployPrefab;
        public Vector3 deployPosition;
        public Vector3 deployEulerAngles;
        public Vector3 bodyPosition;
        public float bodyYaw;
    }

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

        [Tooltip("리깅된 몸체의 Animator 컨트롤러(보통 공용 AC_Unit 의 캐릭터별 오버라이드). 비우면 AC_Unit. 몸체 프리팹 생성 메뉴가 넣는다.")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [Tooltip("손에 쥘 무기 프리팹. 쥐는 손과 반대손 IK 지점은 무기 프리팹(CharacterWeapon)에서 정한다. 몸체 프리팹 생성 메뉴가 붙인다.")]
        [SerializeField] private CharacterWeapon weaponPrefab;
        [Tooltip("반대손에 따로 쥘 무기 프리팹(예: 조각가 → 오른손 망치 + 왼손 끌). 그 프리팹의 쥐는 손 소켓에 붙이기만 한다(반대손 IK 는 주 무기만). 몸체 프리팹 생성 메뉴가 붙인다.")]
        [SerializeField] private CharacterWeapon offHandWeaponPrefab;
        [Tooltip("몸체 곁에 늘 붙어 다니는 오브젝트(예: 화염술사 → 불 정령). 몸체 프리팹 생성 메뉴가 몸체 루트 아래에 붙인다.")]
        [SerializeField] private BodyCompanion[] companions = Array.Empty<BodyCompanion>();
        [Tooltip("상태에 따른 자세(예: 박격포 설치 → 권총을 허리춤에 넣고 박격포 조작 대기). 상태를 비우면 쓰지 않는다. 몸체 프리팹 생성 메뉴가 붙인다.")]
        [SerializeField] private StatusStanceSettings statusStance;
        [Tooltip("기본 공격 타격 이펙트(예: 낫 → 몸통을 가로지르는 베기 선). 비우면 공용(CharacterVisualCatalog.BasicAttackEffect).")]
        [SerializeField] private HitEffect basicAttackEffect;

        [Header("Summon (이 캐릭터가 부른 소환물, 예: 낫 → 그림자 분신)")]
        [Tooltip("소환물 몸체의 기본 머티리얼(예: 그림자). 비우면 팀 색을 옅게 칠한다. 지정하면 소환물 발밑에 팀 색 고리를 둔다.")]
        [SerializeField] private Material summonBodyMaterial;
        [Tooltip("소환물에 붙여 두는 상주 연출 프리팹(예: 발밑 그림자 연기). 소환물이 있는 동안 계속 재생된다.")]
        [SerializeField] private GameObject summonAura;
        [Tooltip("소환할 때 소환자 → 소환 칸으로 재생하는 이펙트(예: 그림자가 미끄러져 가 연기로 솟음).")]
        [SerializeField] private LineEffect summonSpawnEffect;
        [Tooltip("소환 뒤 몸체가 바닥에서 솟아오르기 시작하기까지(초). 소환 이펙트가 소환 칸에 닿는 시점과 맞춘다.")]
        [SerializeField, Min(0f)] private float summonAppearDelay = 0.25f;
        [Tooltip("몸체가 바닥에서 솟아올라 형체를 갖추는 시간(초). 0 이면 바로 보인다.")]
        [SerializeField, Min(0f)] private float summonAppearTime = 0.35f;
        [Tooltip("소환물이 사라질(사망) 때 그 자리에서 재생하는 이펙트(예: 보라 연기로 흩어짐).")]
        [SerializeField] private CastEffect summonVanishEffect;

        [NonSerialized] private Sprite _faceSprite;

        public string CharacterId => characterId;
        public Sprite Illustration => illustration;

        /// <summary>몸체 모델 프리팹. 지정하지 않았으면 null(캡슐).</summary>
        public GameObject BodyPrefab => bodyPrefab;

        /// <summary>몸체 Animator 컨트롤러. 지정하지 않았으면 null(공용 컨트롤러).</summary>
        public RuntimeAnimatorController AnimatorController => animatorController;

        /// <summary>손에 쥘 무기 프리팹. 없으면 null.</summary>
        public CharacterWeapon WeaponPrefab => weaponPrefab;

        /// <summary>반대손에 따로 쥘 무기 프리팹. 없으면 null.</summary>
        public CharacterWeapon OffHandWeaponPrefab => offHandWeaponPrefab;

        /// <summary>몸체 곁에 붙어 다니는 오브젝트들(없으면 빈 배열).</summary>
        public BodyCompanion[] Companions => companions ?? Array.Empty<BodyCompanion>();

        /// <summary>상태에 따른 자세 설정. statusId 가 비어 있으면 쓰지 않는다.</summary>
        public StatusStanceSettings StatusStance => statusStance;

        /// <summary>이 캐릭터만의 기본 공격 타격 이펙트. 없으면 null(공용을 쓴다).</summary>
        public HitEffect BasicAttackEffect => basicAttackEffect;

        /// <summary>소환물 몸체 기본 머티리얼. 없으면 null(팀 색).</summary>
        public Material SummonBodyMaterial => summonBodyMaterial;
        public GameObject SummonAura => summonAura;
        public LineEffect SummonSpawnEffect => summonSpawnEffect;
        public float SummonAppearDelay => summonAppearDelay;
        public float SummonAppearTime => summonAppearTime;
        public CastEffect SummonVanishEffect => summonVanishEffect;

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
