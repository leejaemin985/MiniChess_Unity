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
                AddHandRig(animator, visual != null ? visual.WeaponPrefab : null);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{prefabFolder}/P_{name}Body.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>양손 소켓을 만들고, 무기가 있으면 쥐는 손 소켓에 붙인다(반대손 IK 는 CharacterHandRig 가 런타임에 건다).</summary>
        private static void AddHandRig(Animator animator, CharacterWeapon weaponPrefab)
        {
            Transform right = CharacterHandRig.CreateSocket(animator, WeaponHand.Right);
            Transform left = CharacterHandRig.CreateSocket(animator, WeaponHand.Left);

            CharacterWeapon weapon = null;
            Transform socket = weaponPrefab == null ? null : weaponPrefab.Hand == WeaponHand.Right ? right : left;
            if (socket != null)
            {
                weapon = ((GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab.gameObject, socket)).GetComponent<CharacterWeapon>();
                weapon.name = "Weapon";
            }

            animator.gameObject.AddComponent<CharacterHandRig>().Setup(right, left, weapon);
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
