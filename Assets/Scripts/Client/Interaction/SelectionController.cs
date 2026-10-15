using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Client.Board;
using MiniChess.Client.Bootstrap;
using MiniChess.Client.UI;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Skills;
using MiniChess.Core.Skills.Areas;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Interaction
{
    /// <summary>
    /// 유닛 선택과 행동 지시를 담당한다.
    ///   일반 모드: 클릭 → 현재 팀 유닛 선택 → 이동 가능 칸/공격 대상(적, 장애물) 하이라이트 → 칸/적/장애물 클릭 시 이동/공격 요청
    ///   스킬 모드: 스킬 버튼 → 지정 가능 칸 하이라이트(마우스를 올리면 범위 미리보기) → 클릭 시 스킬 사용 요청
    ///     - 여러 칸 지정 스킬: 고른 칸을 표시하고 다음 후보를 다시 보여 준다. 다 고르면 사용. 우클릭은 마지막 선택 취소.
    ///     - 자기 자신만 지정하는 스킬: 범위를 바로 보여 주고, 자기 칸이나 범위를 한 번 더 클릭하면 사용.
    ///     - 회전하는 범위의 스킬: 우클릭으로 범위를 시계 방향 90도씩 돌린다(이때 우클릭은 취소가 아니다. 취소는 다른 칸 좌클릭이나 스킬 버튼).
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
        private readonly List<Position> _attackObstacles = new List<Position>();

        private string _pendingSkillId;
        private readonly List<Position> _skillTargets = new List<Position>();
        private readonly List<Position> _chosenTargets = new List<Position>();
        private int _targetCount;
        private bool _confirmSelf;
        private bool _canRotate;
        private AreaRotation _rotation;

        public Unit Selected => _selected;

        /// <summary>스킬 지정 중인 스킬 Id. 일반 모드면 null.</summary>
        public string PendingSkillId => _pendingSkillId;

        /// <summary>지정 중인 스킬의 범위를 회전할 수 있는지.</summary>
        public bool CanRotatePending => _pendingSkillId != null && _canRotate;

        /// <summary>지정 중인 스킬의 범위 회전(시계 방향).</summary>
        public AreaRotation PendingRotation => _rotation;

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
            _attackObstacles.Clear();
            ClearSkillMode();
            _boardView.ClearHighlights();
            SelectionChanged?.Invoke();
        }

        /// <summary>선택한 유닛의 스킬 지정 모드로 들어간다. 사용할 수 없으면 이유를 알리고 그대로 둔다.</summary>
        public void BeginSkill(string skillId)
        {
            GameController game = _session.Game;
            if (_selected == null || game == null)
                return;

            SkillInfo skill = game.GetSkills(_selected.Id).FirstOrDefault(info => info.Id == skillId);
            if (skill == null)
                return;

            if (skill.Usable != SkillFailReason.None)
            {
                _session.ReportFailure(FailReasonText.Describe(skill.Usable));
                return;
            }

            List<Position> targets = game.GetSkillTargets(_selected.Id, skillId);
            if (targets.Count == 0)
            {
                _session.ReportFailure(FailReasonText.Describe(SkillFailReason.InvalidTarget));
                return;
            }

            _pendingSkillId = skillId;
            _chosenTargets.Clear();
            _skillTargets.Clear();
            _skillTargets.AddRange(targets);
            _targetCount = skill.Definition.Targeting.TargetCount;
            _confirmSelf = _targetCount == 1 && targets.Count == 1 && targets[0] == _selected.Position.Value;
            _canRotate = skill.CanRotate;
            _rotation = AreaRotation.R0;
            Redraw();
            SelectionChanged?.Invoke();
        }

        /// <summary>지정 중인 스킬의 범위를 시계 방향 90도씩 steps 번 돌린다(음수면 반시계). 회전할 수 없으면 무시.</summary>
        public void RotateSkill(int steps)
        {
            if (!CanRotatePending)
                return;

            int turns = ((steps % 4) + 4) % 4;
            for (int i = 0; i < turns; i++)
                _rotation = _rotation.Next();

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

            // 배치 단계 입력은 PlacementController 가, 봇 팀의 턴은 BotDriver 가 처리한다.
            if (!AcceptsInput(state))
                return;

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

            // 장애물 클릭 → 장애물 공격 시도(실패해도 선택 유지)
            if (cell.HasObstacle)
            {
                _session.TryAttackObstacle(_selected, clicked.Value);
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
            if (_confirmSelf)
            {
                if (GetSelfPreview().Contains(clicked) || clicked == _selected.Position.Value)
                    _session.TryUseSkill(_selected, _pendingSkillId, new[] { _selected.Position.Value }, _rotation);
                else
                    CancelSkill();
                return;
            }

            if (!_skillTargets.Contains(clicked))
            {
                CancelSkill();
                return;
            }

            _chosenTargets.Add(clicked);
            if (_chosenTargets.Count >= _targetCount)
            {
                // 성공하면 StateChanged 에서 스킬 모드가 끝난다. 실패하면 처음부터 다시 고르게 한다.
                if (!_session.TryUseSkill(_selected, _pendingSkillId, _chosenTargets.ToArray(), _rotation))
                    RestartTargeting();
                return;
            }

            RefreshCandidates();
        }

        /// <summary>고른 칸 기준으로 다음 후보를 다시 구한다. 후보가 없으면 이유를 알리고 스킬 지정을 끝낸다.</summary>
        private void RefreshCandidates()
        {
            List<Position> next = _session.Game.GetSkillTargets(_selected.Id, _pendingSkillId, _chosenTargets);
            if (next.Count == 0)
            {
                _session.ReportFailure(FailReasonText.Describe(SkillFailReason.InvalidTarget));
                CancelSkill();
                return;
            }

            _skillTargets.Clear();
            _skillTargets.AddRange(next);
            Redraw();
        }

        private void RestartTargeting()
        {
            _chosenTargets.Clear();
            RefreshCandidates();
        }

        private IReadOnlyList<Position> GetSelfPreview()
        {
            return _session.Game.GetSkillArea(_selected.Id, _pendingSkillId, _selected.Position.Value, _rotation);
        }

        private void OnCancelClicked()
        {
            if (!AcceptsInput(_session.State))
                return;

            // 회전하는 범위의 스킬을 지정 중이면 우클릭은 회전이다.
            if (CanRotatePending)
            {
                RotateSkill(1);
                return;
            }

            if (_pendingSkillId != null && _chosenTargets.Count > 0)
            {
                _chosenTargets.RemoveAt(_chosenTargets.Count - 1);
                RefreshCandidates();
            }
            else if (_pendingSkillId != null)
                CancelSkill();
            else
                Deselect();
        }

        /// <summary>사람이 조작할 차례인지. 배치 단계와 봇 팀의 턴에는 입력을 받지 않는다.</summary>
        private bool AcceptsInput(GameState state)
        {
            if (state == null)
                return true;
            if (state.Phase == GamePhase.Setup)
                return false;
            return !(state.Phase == GamePhase.Battle && _session.IsBot(state.CurrentTeam));
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

            GameController game = _session.Game;
            _moveDestinations.Clear();
            _attackTargets.Clear();
            _moveDestinations.AddRange(game.GetMoveDestinations(unit.Id));
            _attackTargets.AddRange(game.GetAttackTargets(unit.Id));
            _attackObstacles.Clear();
            _attackObstacles.AddRange(game.GetAttackableObstacles(unit.Id));

            Redraw();
            SelectionChanged?.Invoke();
        }

        private void ClearSkillMode()
        {
            _pendingSkillId = null;
            _skillTargets.Clear();
            _chosenTargets.Clear();
            _targetCount = 0;
            _confirmSelf = false;
            _canRotate = false;
            _rotation = AreaRotation.R0;
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

                if (_confirmSelf)
                {
                    foreach (Position cell in GetSelfPreview())
                        _boardView.Highlight(cell, CellHighlight.SkillArea);
                }
                else
                {
                    Position? hovered = _input.HoveredCell;
                    if (hovered.HasValue && _skillTargets.Contains(hovered.Value))
                    {
                        foreach (Position cell in _session.Game.GetSkillArea(_selected.Id, _pendingSkillId, hovered.Value, _rotation))
                            _boardView.Highlight(cell, CellHighlight.SkillArea);
                    }
                }

                foreach (Position chosen in _chosenTargets)
                    _boardView.Highlight(chosen, CellHighlight.SkillChosen);

                return;
            }

            foreach (Position destination in _moveDestinations)
                _boardView.Highlight(destination, CellHighlight.Move);
            foreach (Unit target in _attackTargets)
                _boardView.Highlight(target.Position.Value, CellHighlight.Attack);
            foreach (Position obstacle in _attackObstacles)
                _boardView.Highlight(obstacle, CellHighlight.Attack);
        }
    }
}
