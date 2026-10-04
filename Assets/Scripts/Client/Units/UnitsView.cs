using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>경기의 모든 유닛 뷰를 Unit.Id 기준으로 관리한다.</summary>
    public class UnitsView : MonoBehaviour
    {
        [SerializeField] private Color player1Color = new Color(0.25f, 0.45f, 0.95f);
        [SerializeField] private Color player2Color = new Color(0.9f, 0.3f, 0.25f);
        [SerializeField, Range(0f, 1f)] private float actedDarken = 0.55f;

        private readonly Dictionary<int, UnitView> _views = new Dictionary<int, UnitView>();
        private BoardCoordinates _coordinates;

        public void Build(GameState state, BoardCoordinates coordinates)
        {
            Clear();
            _coordinates = coordinates;

            CreateTeam(state.GetPlayer(Team.Player1), player1Color);
            CreateTeam(state.GetPlayer(Team.Player2), player2Color);

            Sync();
        }

        /// <summary>모든 유닛 뷰를 현재 코어 상태로 맞춘다.</summary>
        public void Sync()
        {
            foreach (UnitView view in _views.Values)
                view.Sync(_coordinates);
        }

        public Color GetTeamColor(Team team)
        {
            return team == Team.Player1 ? player1Color : player2Color;
        }

        public bool TryGetView(Unit unit, out UnitView view)
        {
            return _views.TryGetValue(unit.Id, out view);
        }

        public void Clear()
        {
            foreach (UnitView view in _views.Values)
            {
                if (view != null)
                    Destroy(view.gameObject);
            }

            _views.Clear();
        }

        private void CreateTeam(PlayerState player, Color teamColor)
        {
            Color actedColor = Color.Lerp(teamColor, Color.gray, actedDarken);

            foreach (Unit unit in player.Units)
                _views[unit.Id] = UnitView.Create(transform, unit, teamColor, actedColor);
        }
    }
}
