using UnityEditor;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 자루 무기(Targets 에 등록한 FBX)를 임포트할 때 자루만 가늘게 만든다(Tripo 메시의 자루가 손에 비해 굵을 때).
    ///   - 메시 길이(가장 긴 축)의 아래 끝(머리)에서 ShaftStart 부터 위쪽 정점을 자루 중심축 쪽으로 Scale 배 모은다(고리 장식도 같은 비율).
    ///   - 머리와 이어지는 곳은 Blend 길이만큼 1 → Scale 로 서서히 바꿔 끊기지 않게 한다.
    /// 다시 임포트해도 같은 결과가 나오도록 원본 FBX 대신 임포트 단계에서 바꾼다.
    /// </summary>
    public class WeaponShaftThinning : AssetPostprocessor
    {
        private class Target
        {
            public string Path;
            /// <summary>자루 시작(머리 끝) 위치: 아래 끝에서 무기 길이 대비.</summary>
            public float ShaftStart;
            /// <summary>머리 → 자루 사이 서서히 바꾸는 길이(무기 길이 대비).</summary>
            public float Blend;
            /// <summary>자루 굵기 배율.</summary>
            public float Scale;
        }

        private static readonly Target[] Targets =
        {
            new Target { Path = "Assets/Art/Characters/Inquisitor/Weapon/WarHammer.fbx", ShaftStart = 0.35f, Blend = 0.04f, Scale = 0.65f },
        };

        private void OnPostprocessModel(GameObject root)
        {
            Target target = System.Array.Find(Targets, t => t.Path == assetPath);
            if (target == null)
                return;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                Thin(filter.sharedMesh, target);
        }

        private static void Thin(Mesh mesh, Target target)
        {
            if (mesh == null)
                return;

            Vector3[] vertices = mesh.vertices;
            Bounds bounds = mesh.bounds;
            int axis = LongestAxis(bounds.size);
            int side1 = (axis + 1) % 3, side2 = (axis + 2) % 3;
            float min = bounds.min[axis], length = bounds.size[axis];
            float start = min + target.ShaftStart * length, end = start + target.Blend * length;

            // 자루 중심축: 자루 구간 정점들의 옆 방향 가운데.
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 v in vertices)
            {
                if (v[axis] < end)
                    continue;
                lo = Vector2.Min(lo, new Vector2(v[side1], v[side2]));
                hi = Vector2.Max(hi, new Vector2(v[side1], v[side2]));
            }

            Vector2 center = (lo + hi) * 0.5f;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, v[axis]));
                float scale = Mathf.Lerp(1f, target.Scale, t);
                v[side1] = center.x + (v[side1] - center.x) * scale;
                v[side2] = center.y + (v[side2] - center.y) * scale;
                vertices[i] = v;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static int LongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
                return 0;
            return size.y >= size.z ? 1 : 2;
        }
    }
}
