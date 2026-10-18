using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>
    /// 실행 중에 기본 도형(큐브, 캡슐 등)을 만든다. GameObject.CreatePrimitive 대신 이것을 쓴다.
    /// 기본 도형의 기본 머티리얼(Standard 셰이더)은 씬에서 쓰지 않으면 빌드에서 빠져 분홍색으로 보이므로,
    /// Resources 의 머티리얼(Standard)을 씌워 셰이더가 빌드에 들어가게 한다. 색은 각 뷰가 MaterialPropertyBlock 으로 칠한다.
    /// </summary>
    public static class RuntimePrimitive
    {
        public const string MaterialPath = "Materials/RuntimePrimitive";

        private static Material _material;

        public static GameObject Create(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);

            if (_material == null)
                _material = Resources.Load<Material>(MaterialPath);
            if (_material != null)
                go.GetComponent<Renderer>().sharedMaterial = _material;
            else
                Debug.LogError($"[RuntimePrimitive] Resources/{MaterialPath} 머티리얼이 없어 빌드에서 분홍색으로 보일 수 있습니다");

            return go;
        }
    }
}
