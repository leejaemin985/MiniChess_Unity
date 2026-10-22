using MiniChess.Client.Common;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 유닛의 몸체(캡슐 또는 캐릭터 모델 프리팹). 하위 렌더러를 모두 모아 색과 머티리얼을 한곳에서 다룬다.
    /// MeshRenderer 와 SkinnedMeshRenderer 는 둘 다 Renderer 라 몸체 종류와 상관없이 같은 방식으로 처리한다.
    /// 머티리얼 교체(상태 표현 등) 중에는 색을 칠하지 않는다(교체 머티리얼의 _BaseColor 가 팀 색으로 물들지 않게).
    /// </summary>
    public class UnitBody : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        private Renderer[] _renderers;
        private Material[][] _originals;
        private MaterialPropertyBlock _block;
        /// <summary>색을 칠하는 몸체인지. 캡슐만 칠하고, 모델은 텍스처 색이 덮이지 않게 칠하지 않는다.</summary>
        private bool _tintable;
        private Color? _tint;
        private Material _override;

        /// <param name="prefab">캐릭터 모델 프리팹. null 이면 width × height 캡슐을 만든다.</param>
        /// <param name="scale">전체 크기 배율(소환물 축소 등).</param>
        public static UnitBody Create(Transform parent, GameObject prefab, float height, float width, float scale)
        {
            GameObject body;
            if (prefab != null)
            {
                body = Instantiate(prefab, parent, false);
                body.transform.localScale *= scale;
            }
            else
            {
                // 기본 캡슐은 높이 2, 지름 1.
                body = RuntimePrimitive.Create(PrimitiveType.Capsule);
                body.transform.SetParent(parent, false);
                body.transform.localPosition = Vector3.up * (height * 0.5f * scale);
                body.transform.localScale = new Vector3(width, height * 0.5f, width) * scale;
            }

            body.name = "Body";
            var unitBody = body.AddComponent<UnitBody>();
            unitBody.Collect(tintable: prefab == null);
            return unitBody;
        }

        /// <summary>몸체 색(팀 색, 행동 완료 색). 모델 몸체이거나 머티리얼 교체 중이면 기억만 하고 칠하지 않는다.</summary>
        public void SetTint(Color color)
        {
            if (_tint == color)
                return;

            _tint = color;
            ApplyTint();
        }

        /// <summary>모든 렌더러의 모든 머티리얼 칸을 material 로 바꾼다. null 이면 원래 머티리얼로 되돌린다.</summary>
        public void SetOverride(Material material)
        {
            if (_override == material)
                return;

            _override = material;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (material == null)
                {
                    _renderers[i].sharedMaterials = _originals[i];
                    continue;
                }

                var replaced = new Material[_originals[i].Length];
                for (int slot = 0; slot < replaced.Length; slot++)
                    replaced[slot] = material;
                _renderers[i].sharedMaterials = replaced;
            }

            ApplyTint();
        }

        private void Collect(bool tintable)
        {
            _tintable = tintable;
            _block = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _originals = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
                _originals[i] = _renderers[i].sharedMaterials;
        }

        private void ApplyTint()
        {
            bool tint = _tintable && _override == null && _tint.HasValue;
            foreach (Renderer target in _renderers)
            {
                if (!tint)
                {
                    target.SetPropertyBlock(null);
                    continue;
                }

                target.GetPropertyBlock(_block);
                _block.SetColor(ColorId, _tint.Value);
                target.SetPropertyBlock(_block);
            }
        }
    }
}
