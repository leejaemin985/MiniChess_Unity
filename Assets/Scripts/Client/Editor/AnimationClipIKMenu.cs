using MiniChess.Client.Visuals;
using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// Project 창에서 고른 애니메이션 클립(.anim)에 반대손 IK 세기 커브(OffHandIK, CharacterHandRig 가 읽음)를 넣거나 뺀다.
    /// 클립 전체에 일정한 값으로 넣으며, 구간별로 바꾸려면 넣은 뒤 Animation 창에서 키를 편집한다.
    /// 커브를 빼면 그 클립은 Animator 상태의 OffHandIKState 값을 쓴다. FBX 안의 클립은 읽기 전용이라 건너뛴다.
    /// </summary>
    public static class AnimationClipIKMenu
    {
        private const string MenuRoot = "Assets/MiniChess/Off-Hand IK/";

        private static readonly EditorCurveBinding Binding =
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), CharacterHandRig.OffHandIKParameter);

        [MenuItem(MenuRoot + "Set 1 (양손으로 쥠)")]
        private static void SetOn() => Apply(1f);

        [MenuItem(MenuRoot + "Set 0 (반대손 자유)")]
        private static void SetOff() => Apply(0f);

        [MenuItem(MenuRoot + "Remove (상태 기본값)")]
        private static void Remove() => Apply(null);

        [MenuItem(MenuRoot + "Set 1 (양손으로 쥠)", true)]
        [MenuItem(MenuRoot + "Set 0 (반대손 자유)", true)]
        [MenuItem(MenuRoot + "Remove (상태 기본값)", true)]
        private static bool HasClip() => Selection.GetFiltered<AnimationClip>(SelectionMode.Assets).Length > 0;

        /// <summary>clip 에 weight 로 일정한 OffHandIK 커브를 넣는다. null 이면 커브를 뺀다. 읽기 전용(FBX 안) 클립이면 false.</summary>
        public static bool SetCurve(AnimationClip clip, float? weight)
        {
            if (!AssetDatabase.GetAssetPath(clip).EndsWith(".anim"))
                return false;

            Undo.RecordObject(clip, "Off-Hand IK Curve");
            AnimationCurve curve = weight.HasValue ? AnimationCurve.Constant(0f, clip.length, weight.Value) : null;
            AnimationUtility.SetEditorCurve(clip, Binding, curve);
            EditorUtility.SetDirty(clip);
            return true;
        }

        private static void Apply(float? weight)
        {
            int done = 0;
            foreach (AnimationClip clip in Selection.GetFiltered<AnimationClip>(SelectionMode.Assets))
            {
                if (SetCurve(clip, weight))
                    done++;
                else
                    Debug.LogWarning($"[AnimationClipIKMenu] {clip.name}: FBX 안의 클립은 고칠 수 없어 건너뜀(.anim 으로 꺼내서 쓴다)");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationClipIKMenu] 클립 {done}개 OffHandIK = {(weight.HasValue ? weight.Value.ToString("0.##") : "없음(상태 기본값)")}");
        }
    }
}
