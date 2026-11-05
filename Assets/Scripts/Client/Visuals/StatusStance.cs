using System.Collections.Generic;
using MiniChess.Core.Statuses;
using UnityEngine;

namespace MiniChess.Client.Visuals
{
    /// <summary>
    /// 상태(예: 박격포 설치)에 걸려 있는 동안 몸체 자세를 바꾼다(Animator 와 같은 오브젝트 = 캐릭터 모델에 붙는다).
    ///   - 손에 쥔 무기를 보관 위치(허리춤 등, 뼈 아래 지점)로 옮기고, 풀리면 손 소켓으로 되돌린다. 두 지점 모두 무기 루트(손잡이 지점)가 그대로 겹친다.
    ///   - 설치물(박격포 등, 몸체 루트 아래에 꺼 둔 것)을 켜고, 캐릭터 모델을 몸체 루트 기준 지정 위치로 옮겨 지정 방향을 보게 한다. 풀리면 끄고 제자리로.
    ///   - Animator 의 AltIdle 을 켜서 대기 대신 보조 대기(캐릭터별 오버라이드, 예: 박격포 조작 자세)를 재생한다.
    /// 몸체 프리팹 생성 메뉴가 CharacterVisual 의 설정으로 붙이고, UnitBody 가 유닛 상태가 바뀔 때마다 Sync 한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class StatusStance : MonoBehaviour
    {
        /// <summary>보조 대기로 바꾸는 Animator bool 파라미터.</summary>
        public const string AltIdleParameter = "AltIdle";
        private static readonly int AltIdleId = Animator.StringToHash(AltIdleParameter);

        [SerializeField, CoreStatusId] private string statusId;
        [SerializeField] private Transform weapon;
        [SerializeField] private Transform hand;
        [SerializeField] private Transform stow;
        [SerializeField] private GameObject deployed;
        [SerializeField] private Vector3 restPosition;
        [SerializeField] private Quaternion restRotation = Quaternion.identity;
        [SerializeField] private Vector3 activePosition;
        [SerializeField] private Quaternion activeRotation = Quaternion.identity;

        private Animator _animator;
        private bool? _active;

        /// <summary>에디터(프리팹 생성 메뉴)에서 연결한다. 모델의 지금 로컬 자리를 평소 자리로 기억한다.</summary>
        /// <param name="deployObject">상태 동안 켤 설치물(몸체 루트 아래, 꺼 둔 상태). 없으면 null.</param>
        /// <param name="bodyPosition">상태 동안 모델 자리(몸체 루트 기준).</param>
        /// <param name="bodyYaw">상태 동안 모델이 보는 방향(도, 0 = +z).</param>
        public void Setup(string status, Transform weaponRoot, Transform handSocket, Transform stowPoint, GameObject deployObject, Vector3 bodyPosition, float bodyYaw)
        {
            statusId = status;
            weapon = weaponRoot;
            hand = handSocket;
            stow = stowPoint;
            deployed = deployObject;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            activePosition = bodyPosition;
            activeRotation = Quaternion.Euler(0f, bodyYaw, 0f) * restRotation;
        }

        /// <summary>걸린 상태 목록을 보고 자세를 맞춘다. 바뀔 때만 무기를 옮기고 파라미터를 바꾼다.</summary>
        public void Sync(IReadOnlyList<StatusEffect> statuses)
        {
            bool active = false;
            foreach (StatusEffect status in statuses)
            {
                if (status.Definition.Id == statusId)
                {
                    active = true;
                    break;
                }
            }

            if (_active == active)
                return;

            _active = active;
            Transform target = active ? stow : hand;
            if (weapon != null && target != null)
            {
                weapon.SetParent(target, false);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.identity;
            }

            if (deployed != null)
                deployed.SetActive(active);
            transform.localPosition = active ? activePosition : restPosition;
            transform.localRotation = active ? activeRotation : restRotation;

            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (HasParameter(_animator, AltIdleId))
                _animator.SetBool(AltIdleId, active);
        }

        private static bool HasParameter(Animator animator, int id)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == id)
                    return true;
            }

            return false;
        }
    }
}
