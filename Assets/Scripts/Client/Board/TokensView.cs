using MiniChess.Client.Common;
using MiniChess.Core.State;
using MiniChess.Core.Tokens;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>코어의 토큰 상태에 맞춰 HP/AP 프리팹을 보드 위에 표시한다.</summary>
    public class TokensView : MonoBehaviour
    {
        public const string HpPrefabPath = "Tokens/Prefabs/P_Token_HP";
        public const string ApPrefabPath = "Tokens/Prefabs/P_Token_AP";

        [Tooltip("비우면 Resources/Tokens/Prefabs/P_Token_HP 를 사용한다.")]
        [SerializeField] private GameObject hpPrefab;
        [Tooltip("비우면 Resources/Tokens/Prefabs/P_Token_AP 를 사용한다.")]
        [SerializeField] private GameObject apPrefab;

        private BoardCoordinates _coordinates;
        private Token _shown;
        private GameObject _marker;

        public void Build(BoardCoordinates coordinates)
        {
            Clear();
            _coordinates = coordinates;
        }

        /// <summary>온라인에서는 상태 객체가 매번 바뀌므로 토큰의 값으로 비교한다.</summary>
        public void Sync(GameState state)
        {
            Token token = state?.Token;
            if (token?.Kind == _shown?.Kind && token?.Position == _shown?.Position && token?.Team == _shown?.Team)
                return;

            Clear();
            if (token == null || _coordinates == null)
                return;

            GameObject prefab = GetPrefab(token.Kind);
            if (prefab == null)
                return;

            _marker = Instantiate(prefab, transform, false);
            _marker.name = $"Token {token.Kind} {token.Position}";
            _marker.transform.position = _coordinates.ToWorld(token.Position);
            _shown = token;
        }

        public void Clear()
        {
            if (_marker != null)
                Destroy(_marker);
            _marker = null;
            _shown = null;
        }

        private GameObject GetPrefab(TokenKind kind)
        {
            bool isHeal = kind == TokenKind.Heal;
            string path = isHeal ? HpPrefabPath : ApPrefabPath;
            GameObject prefab = isHeal ? hpPrefab : apPrefab;
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>(path);
                if (isHeal) hpPrefab = prefab;
                else apPrefab = prefab;
            }

            if (prefab == null)
                Debug.LogError($"[TokensView] Resources/{path} 프리팹이 없음", this);
            return prefab;
        }
    }
}
