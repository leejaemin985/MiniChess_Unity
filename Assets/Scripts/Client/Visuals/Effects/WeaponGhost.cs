using UnityEngine;
using UnityEngine.Rendering;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 손에 든 무기의 잔상(호위 시전 때 방패 강조 등). 무기 메시를 같은 자리/각도에 겹쳐 그리고,
    /// 메시 중심을 기준으로 lifetime 동안 커지며 사라진다. 원본 무기는 그대로 보인다.
    ///   - 매 프레임 원본 Transform 을 따라간다(손을 움직여도 붙어 있다).
    ///   - 크기는 메시 로컬 축 기준이다: endScale.x = 폭, y = 두께, z = 길이(방패 메시는 +Z 가 긴 축).
    ///     폭(x)은 widthCurve, 나머지(y, z)는 sizeCurve 로 1 → endScale 까지 간다(0~1 진행도).
    ///   - 세기는 alphaCurve 로 셰이더 _Alpha 를 줄인다(MiniChess/WeaponGhostRim).
    /// 메시를 GPU 로 바로 그리므로(Graphics.DrawMesh) 메시 읽기 허용(Read/Write)이 필요 없다.
    /// 손 시전 이펙트(HandCastEffect)에 붙여 두면 파티클을 켜는 시점에 그 손의 무기 잔상을 재생한다.
    /// </summary>
    public sealed class WeaponGhost : MonoBehaviour
    {
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [Tooltip("잔상 머티리얼(MiniChess/WeaponGhostRim).")]
        [SerializeField] private Material material;
        [SerializeField, Min(0.05f)] private float lifetime = 0.45f;
        [Tooltip("다 커졌을 때 배율(메시 로컬 축: x 폭, y 두께, z 길이).")]
        [SerializeField] private Vector3 endScale = new Vector3(2.98f, 1.5f, 1.5f);
        [Tooltip("폭(x) 진행 곡선(0~1 → 0~1). 앞에서 빨리 넓어지면 바로 전형적인 방패 비율이 된다.")]
        [SerializeField] private AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3f), new Keyframe(0.35f, 0.92f, 0.6f, 0.6f), new Keyframe(1f, 1f, 0.05f, 0f));
        [Tooltip("두께/길이(y, z) 진행 곡선(0~1 → 0~1).")]
        [SerializeField] private AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0.2f, 0f));
        [Tooltip("세기(0~1 진행 → 셰이더 _Alpha).")]
        [SerializeField] private AnimationCurve alphaCurve = new AnimationCurve(
            new Keyframe(0f, 0.85f), new Keyframe(0.15f, 0.7f), new Keyframe(0.45f, 0.4f), new Keyframe(0.75f, 0.15f), new Keyframe(1f, 0f));

        private MaterialPropertyBlock _block;
        private Transform _source;
        private Mesh _mesh;
        private int _layer;
        private float _elapsed;

        /// <summary>잔상을 그리는 중인지.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>weaponRoot 아래 첫 메시 렌더러(무기)의 잔상을 처음부터 재생한다. 메시가 없으면 무시한다.</summary>
        public void PlayOn(Transform weaponRoot)
        {
            if (weaponRoot == null || material == null)
                return;

            foreach (MeshFilter filter in weaponRoot.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer _))
                    continue;

                _source = filter.transform;
                _mesh = filter.sharedMesh;
                _layer = filter.gameObject.layer;
                _elapsed = 0f;
                IsPlaying = true;
                return;
            }
        }

        public void Stop()
        {
            IsPlaying = false;
        }

        private void OnDisable()
        {
            IsPlaying = false;
        }

        private void LateUpdate()
        {
            if (!IsPlaying)
                return;
            if (_source == null)
            {
                IsPlaying = false;
                return;
            }

            _elapsed += Time.deltaTime;
            float t = _elapsed / lifetime;
            if (t >= 1f)
            {
                IsPlaying = false;
                return;
            }

            float width = widthCurve.Evaluate(t);
            float size = sizeCurve.Evaluate(t);
            var scale = new Vector3(
                Mathf.LerpUnclamped(1f, endScale.x, width),
                Mathf.LerpUnclamped(1f, endScale.y, size),
                Mathf.LerpUnclamped(1f, endScale.z, size));

            // 메시 중심을 기준으로 키운다: 원본 행렬 × (중심으로 옮김 × 배율 × 중심을 원점으로).
            Vector3 center = _mesh.bounds.center;
            Matrix4x4 matrix = _source.localToWorldMatrix
                * Matrix4x4.Translate(center) * Matrix4x4.Scale(scale) * Matrix4x4.Translate(-center);

            _block ??= new MaterialPropertyBlock();
            _block.SetFloat(AlphaId, Mathf.Max(0f, alphaCurve.Evaluate(t)));
            for (int i = 0; i < _mesh.subMeshCount; i++)
                Graphics.DrawMesh(_mesh, matrix, material, _layer, null, i, _block, ShadowCastingMode.Off, false);
        }
    }
}
