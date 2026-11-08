using System.Text.RegularExpressions;
using MiniChess.Client.Visuals;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 캐릭터 모델(Art/Characters/{이름}/Model/{이름}.fbx)마다 보드 위 몸체 프리팹(Prefabs/P_{이름}Body)을 만들고 CharacterVisual 의 bodyPrefab 에 넣는다.
    ///   - 루트 = 발밑 중앙(회전/크기 없음), 모델은 자식(FBX 프리팹 인스턴스)으로 둔다.
    ///     리깅 안 된 모델(MeshRenderer)은 경계 상자 바닥 중앙이 루트에 오게 옮기고, 리깅된 모델(SkinnedMeshRenderer)은 리그 원점이 발밑이라 그대로 둔다.
    ///   - 리깅된 Humanoid 모델은 Animator 에 CharacterVisual 의 컨트롤러(없으면 공용 AC_Unit)를 넣는다. 루트 모션은 끄고(칸 위치는 UnitView 가 정함), 화면 밖이면 뼈 갱신을 멈춘다.
    ///     양손에 손바닥 소켓을 만들고 CharacterHandRig 를 붙인다. CharacterVisual 에 무기가 있으면 무기의 쥐는 손 소켓에 붙인다(반대손은 런타임 IK).
    ///     상태에 따른 자세가 있으면 무기 보관 지점과 StatusStance 를 붙인다(예: 박격포 설치 중 권총을 허리춤으로).
    ///   - CharacterVisual 의 동반 오브젝트(예: 불 정령, 허리춤 권총)는 루트 기준 지정 위치에 붙인다. followBone 이면 그 자리에서 지정 뼈 아래로 옮긴다.
    ///   - 앞 방향은 +z(Meshy 내보내기 기준). 팀별로 상대 진영을 보게 돌리는 것은 UnitView 가 한다.
    /// 폴더 이름(PascalCase)을 코어 캐릭터 Id(UPPER_SNAKE, 예: ChainGuard → CHAIN_GUARD)로 바꿔 CharacterVisual 을 찾는다.
    /// 모델을 바꾸거나(리깅된 모델 등) 캐릭터가 늘면 다시 실행한다. 프리팹은 덮어쓴다.
    /// </summary>
    public static class CharacterBodyPrefabMenu
    {
        private const string CharacterFolder = "Assets/Art/Characters";
        private const string VisualFolder = "Assets/Art/CharacterVisuals";
        public const string UnitControllerPath = "Assets/Art/Animations/AC_Unit.controller";

        [MenuItem("MiniChess/Build Character Body Prefabs")]
        public static void Build()
        {
            int built = 0, linked = 0;
            foreach (string folder in AssetDatabase.GetSubFolders(CharacterFolder))
            {
                string name = folder.Substring(folder.LastIndexOf('/') + 1);
                string modelPath = $"{folder}/Model/{name}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null)
                    continue;

                CharacterVisual visual = FindVisual(ToCharacterId(name));
                GameObject prefab = BuildPrefab(folder, name, model, visual);
                built++;

                if (visual == null)
                {
                    Debug.LogWarning($"[CharacterBodyPrefabMenu] {name}: CharacterVisual({ToCharacterId(name)}) 이 없어 연결하지 못함");
                    continue;
                }

                var visualObject = new SerializedObject(visual);
                visualObject.FindProperty("bodyPrefab").objectReferenceValue = prefab;
                visualObject.ApplyModifiedPropertiesWithoutUndo();
                linked++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterBodyPrefabMenu] 몸체 프리팹 {built}개, CharacterVisual 연결 {linked}개");
        }

        private static GameObject BuildPrefab(string folder, string name, GameObject model, CharacterVisual visual)
        {
            string prefabFolder = $"{folder}/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
                AssetDatabase.CreateFolder(folder, "Prefabs");

            var root = new GameObject($"P_{name}Body");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);

            bool rigged = instance.GetComponentInChildren<SkinnedMeshRenderer>() != null;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (!rigged && renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                    bounds.Encapsulate(renderer.bounds);
                instance.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            var animator = instance.GetComponent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                animator.runtimeAnimatorController = visual != null && visual.AnimatorController != null
                    ? visual.AnimatorController
                    : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(UnitControllerPath);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                CharacterWeapon weapon = AddHandRig(animator, visual != null ? visual.WeaponPrefab : null, visual != null ? visual.OffHandWeaponPrefab : null);
                if (visual != null)
                    AddStatusStance(root.transform, animator, weapon, visual.StatusStance);
            }

            if (visual != null)
                AddCompanions(root.transform, animator, visual.Companions);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{prefabFolder}/P_{name}Body.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// 양손 소켓을 만들고, 무기가 있으면 쥐는 손 소켓에 붙인다(반대손 IK 는 CharacterHandRig 가 런타임에 건다).
        /// 반대손 무기(offHandWeaponPrefab)는 그 프리팹의 쥐는 손 소켓에 붙이기만 한다(IK 없음, 주 무기와 같은 손이면 붙이지 않는다).
        /// </summary>
        private static CharacterWeapon AddHandRig(Animator animator, CharacterWeapon weaponPrefab, CharacterWeapon offHandWeaponPrefab)
        {
            Transform right = CharacterHandRig.CreateSocket(animator, WeaponHand.Right);
            Transform left = CharacterHandRig.CreateSocket(animator, WeaponHand.Left);

            CharacterWeapon weapon = AttachWeapon(weaponPrefab, right, left, "Weapon");
            if (offHandWeaponPrefab != null && (weaponPrefab == null || offHandWeaponPrefab.Hand != weaponPrefab.Hand))
                AttachWeapon(offHandWeaponPrefab, right, left, "OffHandWeapon");
            else if (offHandWeaponPrefab != null)
                Debug.LogWarning($"[CharacterBodyPrefabMenu] {offHandWeaponPrefab.name}: 주 무기와 같은 손이라 반대손 무기를 붙이지 않음");

            animator.gameObject.AddComponent<CharacterHandRig>().Setup(right, left, weapon);
            return weapon;
        }

        /// <summary>무기 프리팹을 그 쥐는 손 소켓에 붙인다. 프리팹이 없으면 null.</summary>
        private static CharacterWeapon AttachWeapon(CharacterWeapon prefab, Transform right, Transform left, string name)
        {
            if (prefab == null)
                return null;

            Transform socket = prefab.Hand == WeaponHand.Right ? right : left;
            CharacterWeapon weapon = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, socket)).GetComponent<CharacterWeapon>();
            weapon.name = name;
            return weapon;
        }

        /// <summary>
        /// 상태에 따른 자세(StatusStance)를 붙인다: 보관 지점(WeaponStow)을 루트 기준 위치/회전(바인드 포즈)에 만들어 지정 뼈 아래로 옮기고,
        /// 설치물(Deploy)을 루트 아래 지정 위치에 꺼 둔 채 놓는다. 무기가 없으면 보관 지점은 만들지 않는다.
        /// </summary>
        private static void AddStatusStance(Transform root, Animator animator, CharacterWeapon weapon, StatusStanceSettings settings)
        {
            if (string.IsNullOrEmpty(settings.statusId))
                return;

            Transform stow = null;
            Transform bone = animator.GetBoneTransform(settings.stowBone);
            if (weapon != null && bone != null)
            {
                stow = new GameObject("WeaponStow").transform;
                stow.SetParent(root, false);
                stow.localPosition = settings.stowPosition;
                stow.localRotation = Quaternion.Euler(settings.stowEulerAngles);
                stow.SetParent(bone, true);
            }

            GameObject deploy = null;
            if (settings.deployPrefab != null)
            {
                deploy = (GameObject)PrefabUtility.InstantiatePrefab(settings.deployPrefab, root);
                deploy.name = "Deploy";
                deploy.transform.localPosition = settings.deployPosition;
                deploy.transform.localRotation = Quaternion.Euler(settings.deployEulerAngles);
                deploy.SetActive(false);
            }

            animator.gameObject.AddComponent<StatusStance>().Setup(
                settings.statusId, weapon != null ? weapon.transform : null, weapon != null ? weapon.transform.parent : null, stow,
                deploy, settings.bodyPosition, settings.bodyYaw);
        }

        /// <summary>
        /// 동반 오브젝트(예: 불 정령, 허리춤 권총)를 몸체 루트 기준 지정 위치/회전에 프리팹 인스턴스로 놓는다.
        /// 뼈를 따라가는 것은 그 자리(바인드 포즈) 그대로 뼈 아래로 옮긴다. 뼈가 없으면(리깅 안 된 모델) 루트에 둔다.
        /// </summary>
        private static void AddCompanions(Transform root, Animator animator, BodyCompanion[] companions)
        {
            for (int i = 0; i < companions.Length; i++)
            {
                BodyCompanion entry = companions[i];
                if (entry.prefab == null)
                    continue;

                var companion = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, root);
                companion.name = $"Companion_{i}";
                companion.transform.localPosition = entry.localPosition;
                companion.transform.localRotation = Quaternion.Euler(entry.localEulerAngles);

                Transform bone = entry.followBone && animator != null && animator.isHuman ? animator.GetBoneTransform(entry.bone) : null;
                if (bone != null)
                    companion.transform.SetParent(bone, true);
            }
        }

        private static CharacterVisual FindVisual(string characterId)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterVisual", new[] { VisualFolder }))
            {
                var visual = AssetDatabase.LoadAssetAtPath<CharacterVisual>(AssetDatabase.GUIDToAssetPath(guid));
                if (visual != null && visual.CharacterId == characterId)
                    return visual;
            }

            return null;
        }

        /// <summary>"ChainGuard" → "CHAIN_GUARD"</summary>
        private static string ToCharacterId(string folderName)
        {
            return Regex.Replace(folderName, "(?<=[a-z0-9])([A-Z])", "_$1").ToUpperInvariant();
        }
    }
}
