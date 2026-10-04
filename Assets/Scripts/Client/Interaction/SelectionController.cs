using System.Collections.Generic;
using MiniChess.Client.Board;
using MiniChess.Client.Bootstrap;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Interaction
{
    /// <summary>
    /// 유닛 선택과 행동 지시를 담당한다.
    /// 클릭 → 현재 팀 유닛 선택 → 이동 가능 칸/공격 대상 하이라이트 → 칸/적 클릭 시 GameSession 에 행동 요청.
    /// 실행 가능 여부 판정은 GameSession(→ 코어)에 맡기고, 거부 이유 표시도 그쪽 이벤트로 처리된다.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        private GameSession _session;
        private BoardInput _input;
        private BoardView _boardView;

        private Unit _selected;
        private readonly List<Position> _moveDestinations = new List<Position>();
        private readonly List<Unit> _attackTargets = new List<Unit>();

        public void Initialize(GameSession session, BoardInput input, BoardView boardView)
        {
            Unbind();

            _session = session;
            _input = input;
            _boardView = boardView;

            _input.CellClicked += OnCellClicked;
            _input.CancelClicked += Deselect;
            _session.StateChanged += OnStateChanged;
        }

        public void Deselect()
        {
            _selected = null;
            _moveDestinations.Clear();
            _attackTargets.Clear();
            _boardView.ClearHighlights();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (_input != null)
            {
                _input.CellClicked -= OnCellClicked;
                _input.CancelClicked -= Deselect;
            }

            if (_session != null)
                _session.StateChanged -= OnStateChanged;
        }

        private void OnCellClicked(Position? clicked)
        {
            GameState state = _session.State;
            if (state == null || state.IsGameOver || !clicked.HasValue)
            {
                Deselect();
                return;
            }

            BoardCell cell = state.Board.GetCell(clicked.Value);
            Unit occupant = cell.Occupant;

            // 아군 클릭 → 선택 전환
            if (occupant != null && occupant.Team == state.CurrentTeam)
            {
                Select(occupant);
                return;
            }

            if (_selected == null || cell.IsWall)
            {
                Deselect();
                return;
            }

            // 적 클릭 → 공격 시도(실패해도 선택 유지), 빈 칸 클릭 → 이동 시도(실패하면 선택 해제)
            if (occupant != null)
                _session.TryAttack(_selected, occupant);
            else if (!_session.TryMove(_selected, clicked.Value))
                Deselect();
        }

        /// <summary>행동 성공/턴 종료/새 경기 후 선택을 현재 상태에 맞춘다.</summary>
        private void OnStateChanged()
        {
            Unit unit = _selected;
            GameState state = _session.State;

            if (unit == null || !unit.IsPlaced || state.IsGameOver || unit.Team != state.CurrentTeam)
            {
                Deselect();
                return;
            }

            Select(unit);
            if (_moveDestinations.Count == 0 && _attackTargets.Count == 0)
                Deselect();
        }

        private void Select(Unit unit)
        {
            Deselect();
            _selected = unit;

            GameState state = _session.State;
            _moveDestinations.AddRange(ActionQueries.GetMoveDestinations(state, unit));
            _attackTargets.AddRange(ActionQueries.GetAttackableTargets(state, unit));

            _boardView.Highlight(unit.Position.Value, CellHighlight.Selected);
            foreach (Position destination in _moveDestinations)
                _boardView.Highlight(destination, CellHighlight.Move);
            foreach (Unit target in _attackTargets)
                _boardView.Highlight(target.Position.Value, CellHighlight.Attack);
        }
    }
}
