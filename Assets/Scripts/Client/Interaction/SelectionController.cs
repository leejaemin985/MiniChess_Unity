using System;
using System.Collections.Generic;
using MiniChess.Client.Board;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.UI;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Skills;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Interaction
{
    /// <summary>
    /// 유닛 선택과 행동 지시를 담당한다.
    ///   일반 모드: 클릭 → 현재 팀 유닛 선택 → 이동 가능 칸/공격 대상 하이라이트 → 칸/적 클릭 시 이동/공격 요청
    ///   스킬 모드: 스킬 버튼 → 지정 가능 칸 하이라이트(마우스를 올리면 범위 미리보기) → 클릭 시 스킬 사용 요청
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

        private string _pendingSkillId;
        private readonly List<Position> _skillTargets = new List<Position>();

        public Unit Selected => _selected;

        /// <summary>스킬 지정 중인 스킬 Id. 일반 모드면 null.</summary>
        public string PendingSkillId => _pendingSkillId;

        /// <summary>선택 유닛 또는 스킬 지정 모드가 바뀌었다.</summary>
        public event Action SelectionChanged;

        public void Initialize(GameSession session, BoardInput input, BoardView boardView)
        {
            Unbind();

            _session = session;
            _input = input;
            _boardView = boardView;

            _input.CellClicked += OnCellClicked;
            _input.CancelClicked += OnCancelClicked;
            _input.HoverChanged += OnHoverChanged;
            _session.StateChanged += OnStateChanged;
        }

        public void Deselect()
        {
            _selected = null;
            _moveDestinations.Clear();
            _attackTargets.Clear();
            ClearSkillMode();
            _boardView.ClearHighlights();
            SelectionChanged?.Invoke();
        }

        /// <summary>선택한 유닛의 스킬 지정 모드로 들어간다. 사용할 수 없으면 이유를 알리고 그대로 둔다.</summary>
        public void BeginSkill(string skillId)
        {
            GameState state = _session.State;
            if (_selected == null || state == null)
                return;

            SkillFailReason reason = UseSkillAction.ValidateUsable(state, _selected, skillId);
            if (reason != SkillFailReason.None)
            {
                _session.ReportFailure(FailReasonText.Describe(reason));
                return;
            }

            List<Position> targets = SkillQueries.GetValidTargets(state, _selected, skillId);
            if (targets.Count == 0)
            {
                _session.ReportFailure(FailReasonText.Describe(SkillFailReason.InvalidTarget));
                return;
            }

            _pendingSkillId = skillId;
            _skillTargets.Clear();
            _skillTargets.AddRange(targets);
            Redraw();
            SelectionChanged?.Invoke();
        }

        public void CancelSkill()
        {
            if (_pendingSkillId == null)
                return;

            ClearSkillMode();
            Redraw();
            SelectionChanged?.Invoke();
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
                _input.CancelClicked -= OnCancelClicked;
                _input.HoverChanged -= OnHoverChanged;
            }

            if (_session != null)
                _session.StateChanged -= OnStateChanged;
        }

        #region Input

        private void OnCellClicked(Position? clicked)
        {
            GameState state = _session.State;
            if (state == null || state.IsGameOver || !clicked.HasValue)
            {
                Deselect();
                return;
            }

            if (_pendingSkillId != null)
            {
                OnSkillTargetClicked(clicked.Value);
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

        private void OnSkillTargetClicked(Position clicked)
        {
            if (_skillTargets.Contains(clicked))
                _session.TryUseSkill(_selected, _pendingSkillId, clicked);
            else
                CancelSkill();
        }

        private void OnCancelClicked()
        {
            if (_pendingSkillId != null)
                CancelSkill();
            else
                Deselect();
        }

        private void OnHoverChanged(Position? hovered)
        {
            if (_pendingSkillId != null)
                Redraw();
        }

        #endregion

        /// <summary>행동 성공/턴 종료/새 경기 후 선택을 현재 상태에 맞춘다. 스킬 지정 모드는 끝낸다.</summary>
        private void OnStateChanged()
        {
            Unit unit = _selected;
            GameState state = _session.State;
            ClearSkillMode();

            if (unit == null || !unit.IsPlaced || state.IsGameOver || unit.Team != state.CurrentTeam)
            {
                Deselect();
                return;
            }

            Select(unit);
        }

        private void Select(Unit unit)
        {
            _selected = unit;
            ClearSkillMode();

            GameState state = _session.State;
            _moveDestinations.Clear();
            _attackTargets.Clear();
            _moveDestinations.AddRange(ActionQueries.GetMoveDestinations(state, unit));
            _attackTargets.AddRange(ActionQueries.GetAttackableTargets(state, unit));

            Redraw();
            SelectionChanged?.Invoke();
        }

        private void ClearSkillMode()
        {
            _pendingSkillId = null;
            _skillTargets.Clear();
        }

        private void Redraw()
        {
            _boardView.ClearHighlights();
            if (_selected == null || !_selected.IsPlaced)
                return;

            _boardView.Highlight(_selected.Position.Value, CellHighlight.Selected);

            if (_pendingSkillId != null)
            {
                foreach (Position target in _skillTargets)
                    _boardView.Highlight(target, CellHighlight.SkillTarget);

                Position? hovered = _input.HoveredCell;
                if (hovered.HasValue && _skillTargets.Contains(hovered.Value))
                {
                    GameState state = _session.State;
                    foreach (Position cell in SkillQueries.GetAffectedCells(state, _selected, _pendingSkillId, hovered.Value))
                        _boardView.Highlight(cell, CellHighlight.SkillArea);
                }

                return;
            }

            foreach (Position destination in _moveDestinations)
                _boardView.Highlight(destination, CellHighlight.Move);
            foreach (Unit target in _attackTargets)
                _boardView.Highlight(target.Position.Value, CellHighlight.Attack);
        }
    }
}
