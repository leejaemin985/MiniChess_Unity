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
    /// 유닛 선택과 행동 지시를 담당한다. 칸/대상을 클릭하면 바로 행동하지 않고 후보(Candidate)로 고른 뒤, 확정(ConfirmCandidate)해야 실행한다.
    ///   일반 모드: 클릭 → 현재 팀 유닛 선택 → 이동 가능 칸/공격 대상(적, 장애물) 하이라이트
    ///     → 이동 칸/공격 대상 클릭 시 후보로 고르고 경로(이동)·대상과 AP 비용을 보여준다. 다른 유효한 칸/대상을 누르면 후보만 바뀐다.
    ///   스킬 모드: 스킬 버튼 → 지정 가능 칸 하이라이트(마우스를 올리면 범위 미리보기) → 칸을 다 고르면 후보(범위와 비용 표시)
    ///     - 여러 칸 지정 스킬: 고른 칸을 표시하고 다음 후보를 다시 보여 준다. 다 고른 뒤 마지막 단계의 다른 칸을 누르면 마지막 칸만 바뀌고,
    ///       첫 단계의 칸을 누르면 그 칸부터 다시 고른다. 우클릭은 마지막 선택 취소.
    ///     - 자기 자신만 지정하는 스킬: 스킬 버튼을 누르면 바로 후보(범위 표시).
    ///     - 회전하는 범위의 스킬: 우클릭으로 범위를 시계 방향 90도씩 돌린다(이때 우클릭은 취소가 아니다. 취소는 다른 칸 좌클릭, 스킬 버튼, 취소 버튼).
    ///   평타 모드: 평타 버튼 → 이동 칸 대신 기본 공격 사거리와 대상만 보임 → 대상(적, 장애물)을 누르면 공격 후보. 다른 칸을 누르면 평타 지정을 끝낸다.
    ///   후보가 있으면 하단에 취소/확정 버튼이 뜬다(UnitInfoPanel). 실제 AP 는 확정한 행동이 성공한 뒤에 차감된다.
    /// 실행 가능 여부 판정은 GameSession(→ 코어)에 맡기고, 거부 이유 표시도 그쪽 이벤트로 처리된다.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        /// <summary>확정을 기다리는 행동의 종류.</summary>
        public enum CandidateKind
        {
            None,
            Move,
            Attack,
            AttackObstacle,
            Skill,
        }

        private GameSession _session;
        private BoardInput _input;
        private BoardView _boardView;

        private Unit _selected;
        private Unit _inspected;
        private readonly List<Position> _moveDestinations = new List<Position>();
        private readonly List<Unit> _attackTargets = new List<Unit>();
        private readonly List<Position> _attackObstacles = new List<Position>();

        /// <summary>일반 모드의 후보(이동/공격/장애물 공격). 스킬 후보는 스킬 지정 상태(다 고름)로 판단한다.</summary>
        private CandidateKind _actionCandidate;
        private Position _candidateCell;
        private Unit _candidateTarget;
        private MovePreview _movePreview;

        /// <summary>평타 버튼으로 들어간 기본 공격 지정 모드(사거리와 대상만 보이고, 대상을 누르면 공격 후보).</summary>
        private bool _attackPending;
        /// <summary>평타 버튼에 마우스를 올렸다(사거리 미리보기).</summary>
        private bool _previewAttack;

        private string _pendingSkillId;
        /// <summary>마우스를 올린 스킬 버튼의 스킬(사거리 미리보기). 지정 중인 스킬이 있으면 그리지 않는다.</summary>
        private string _previewSkillId;
        private readonly List<Position> _skillTargets = new List<Position>();
        private readonly List<Position> _chosenTargets = new List<Position>();
        private int _targetCount;
        private bool _confirmSelf;
        private bool _canRotate;
        private AreaRotation _rotation;

        public Unit Selected => _selected;

        /// <summary>행동 없이 정보만 보는 유닛(상대 유닛 등). 선택한 유닛이 있으면 null.</summary>
        public Unit Inspected => _inspected;

        /// <summary>정보 패널에 보여줄 유닛: 선택한 유닛, 없으면 보는 중인 유닛.</summary>
        public Unit Displayed => _selected ?? _inspected;

        /// <summary>스킬 지정 중인 스킬 Id. 일반 모드면 null.</summary>
        public string PendingSkillId => _pendingSkillId;

        /// <summary>평타 버튼으로 기본 공격 대상을 고르는 중인지.</summary>
        public bool IsAttackPending => _attackPending;

        /// <summary>지정 중인 스킬의 범위를 회전할 수 있는지.</summary>
        public bool CanRotatePending => _pendingSkillId != null && _canRotate;

        /// <summary>지정 중인 스킬의 범위 회전(시계 방향).</summary>
        public AreaRotation PendingRotation => _rotation;

        /// <summary>확정을 기다리는 행동. 없으면 None.</summary>
        public CandidateKind Candidate
        {
            get
            {
                if (_selected == null)
                    return CandidateKind.None;
                if (_pendingSkillId != null)
                    return IsSkillComplete ? CandidateKind.Skill : CandidateKind.None;
                return _actionCandidate;
            }
        }

        /// <summary>후보 행동의 AP 비용(이동은 경로 끝까지 간 비용). 후보가 없으면 0.</summary>
        public int CandidateCost
        {
            get
            {
                switch (Candidate)
                {
                    case CandidateKind.Move: return _movePreview?.Cost ?? 0;
                    case CandidateKind.Attack:
                    case CandidateKind.AttackObstacle: return BasicAttackCost;
                    case CandidateKind.Skill: return GetSkillCost(_pendingSkillId);
                    default: return 0;
                }
            }
        }

        /// <summary>
        /// 하단 AP 게이지에 미리 보일 비용(쓸 AP): 후보 행동 → 평타/스킬 지정 중 → 마우스를 올린 스킬/평타 버튼 순. 없으면 0.
        /// 실제 AP 는 행동이 확정된 뒤에 차감된다.
        /// </summary>
        public int PreviewApCost
        {
            get
            {
                if (Candidate != CandidateKind.None)
                    return CandidateCost;
                if (_attackPending)
                    return BasicAttackCost;
                if (_pendingSkillId != null || _previewSkillId != null)
                    return GetSkillCost(_pendingSkillId ?? _previewSkillId);
                return _previewAttack ? BasicAttackCost : 0;
            }
        }

        /// <summary>PreviewApCost 를 쓸 팀(미리보는 행동의 유닛 팀). 미리보기가 없으면 null.</summary>
        public Team? PreviewApTeam => PreviewApCost > 0
            ? (Candidate != CandidateKind.None || _pendingSkillId != null || _attackPending ? _selected : Displayed)?.Team
            : null;

        private int BasicAttackCost => _session.State?.Rules.ActionCost.BasicAttackCost ?? 0;

        /// <summary>지금 후보를 확정할 수 있는지(조작할 차례이고 서버 결과 대기/행동 연출 중이 아님).</summary>
        public bool CanConfirm => Candidate != CandidateKind.None && AcceptsInput(_session.State);

        /// <summary>선택 유닛, 스킬 지정 모드, 후보가 바뀌었다.</summary>
        public event Action SelectionChanged;

        /// <summary>하이라이트(미리보기)를 다시 그렸다. AP 미리보기(PreviewApCost)가 바뀌었을 수 있다.</summary>
        public event Action PreviewChanged;

        private bool IsSkillComplete => _pendingSkillId != null && (_confirmSelf || (_targetCount > 0 && _chosenTargets.Count >= _targetCount));

        public void Initialize(GameSession session, BoardInput input, BoardView boardView)
        {
            Unbind();

            _session = session;
            _input = input;
            _boardView = boardView;

            _input.CellClicked += OnCellClicked;
            _input.CancelClicked += OnCancelClicked;
            _input.HoverChanged += OnHoverChanged;
            _input.ResolveOverlap = ResolveOverlap;
            _session.StateChanged += OnStateChanged;
        }

        public void Deselect()
        {
            _selected = null;
            _inspected = null;
            _moveDestinations.Clear();
            _attackTargets.Clear();
            _attackObstacles.Clear();
            ClearActionCandidate();
            ClearSkillMode();
            Redraw();
            SelectionChanged?.Invoke();
        }

        /// <summary>선택한 유닛의 스킬 지정 모드로 들어간다. 사용할 수 없으면 이유를 알리고 그대로 둔다. 자기 자신만 지정하는 스킬은 바로 후보가 된다.</summary>
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

            ClearActionCandidate();
            _attackPending = false;
            _pendingSkillId = skillId;
            _chosenTargets.Clear();
            _skillTargets.Clear();
            _skillTargets.AddRange(targets);
            _targetCount = skill.Definition.Targeting.TargetCount;
            _confirmSelf = skill.Definition.Targeting.IsSelfOnly;
            _canRotate = skill.CanRotate;
            _rotation = AreaRotation.R0;
            Redraw();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// 평타 버튼: 선택한 유닛의 기본 공격 지정 모드로 들어간다(이동 칸은 숨기고 사거리와 공격 대상만 보인다). 공격할 대상이 없으면 이유를 알리고 그대로 둔다.
        /// 대상(적, 장애물)을 누르면 공격 후보가 되고, 확정 버튼으로 실행한다.
        /// </summary>
        public void BeginAttack()
        {
            if (_selected == null || _session.Game == null)
                return;

            if (_attackTargets.Count == 0 && _attackObstacles.Count == 0)
            {
                _session.ReportFailure(FailReasonText.Describe(AttackFailReason.InvalidTarget));
                return;
            }

            ClearActionCandidate();
            ClearSkillMode();
            _attackPending = true;
            Redraw();
            SelectionChanged?.Invoke();
        }

        public void CancelAttack()
        {
            if (!_attackPending)
                return;

            _attackPending = false;
            ClearActionCandidate();
            Redraw();
            SelectionChanged?.Invoke();
        }

        /// <summary>평타 버튼에 마우스를 올렸을 때 기본 공격 사거리를 미리 그린다(on = false 면 끈다).</summary>
        public void PreviewAttack(bool on)
        {
            if (_previewAttack == on)
                return;

            _previewAttack = on;
            Redraw();
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

        /// <summary>후보 행동을 실행한다. 실패하면(거부 이유는 GameSession 이 알린다) 이동/공격 후보는 지우고, 스킬은 처음부터 다시 고르게 한다.</summary>
        public void ConfirmCandidate()
        {
            if (!CanConfirm)
                return;

            switch (Candidate)
            {
                case CandidateKind.Move:
                    // 성공하면 StateChanged 에서 후보가 지워진다.
                    if (!_session.TryMove(_selected, _candidateCell))
                        CancelCandidate();
                    break;
                case CandidateKind.Attack:
                    if (!_session.TryAttack(_selected, _candidateTarget))
                        CancelCandidate();
                    break;
                case CandidateKind.AttackObstacle:
                    if (!_session.TryAttackObstacle(_selected, _candidateCell))
                        CancelCandidate();
                    break;
                case CandidateKind.Skill:
                    IReadOnlyList<Position> targets = _confirmSelf ? new[] { _selected.Position.Value } : _chosenTargets.ToArray();
                    if (!_session.TryUseSkill(_selected, _pendingSkillId, targets, _rotation) && !_confirmSelf)
                        RestartTargeting();
                    break;
            }
        }

        /// <summary>후보 행동을 취소한다(취소 버튼). 유닛 선택은 그대로 두고, 스킬/평타 지정 중이었으면 그것도 끝낸다.</summary>
        public void CancelCandidate()
        {
            if (_pendingSkillId != null)
            {
                CancelSkill();
                return;
            }

            if (_attackPending)
            {
                CancelAttack();
                return;
            }

            if (_actionCandidate == CandidateKind.None)
                return;

            ClearActionCandidate();
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
                _input.ResolveOverlap = null;
            }

            if (_session != null)
                _session.StateChanged -= OnStateChanged;
        }

        #region Input

        private void OnCellClicked(Position? clicked)
        {
            GameState state = _session.State;

            // 배치 단계 입력은 PlacementController 가 처리한다. 경기 전(온라인 상대 대기)과 서버 결과 대기/행동 연출 중에는 무시한다.
            if (state == null || state.Phase == GamePhase.Setup || _session.IsBusy)
                return;

            // 이 화면에서 조작하지 않는 팀(봇, 온라인 상대)의 턴에는 유닛 정보 보기만 된다.
            if (!AcceptsInput(state))
            {
                Unit unit = clicked.HasValue ? state.Board.GetCell(clicked.Value).Occupant : null;
                if (unit != null)
                    Inspect(unit);
                else
                    Deselect();
                return;
            }

            if (state.IsGameOver || !clicked.HasValue)
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

            // 평타 지정 중: 공격할 수 있는 적/장애물은 공격 후보, 그 밖의 칸은 평타 지정을 끝낸다(이동하지 않는다).
            if (_attackPending)
            {
                if (occupant != null && _attackTargets.Contains(occupant))
                    SetActionCandidate(CandidateKind.Attack, clicked.Value, occupant, null);
                else if (cell.HasObstacle && _attackObstacles.Contains(clicked.Value))
                    SetActionCandidate(CandidateKind.AttackObstacle, clicked.Value, null, null);
                else
                    CancelAttack();
                return;
            }

            // 상대 유닛 클릭 → 지금 공격할 수 있으면 공격 후보, 아니면(선택 없음/사거리 밖 등) 정보 보기
            if (occupant != null && !(_selected != null && _attackTargets.Contains(occupant)))
            {
                Inspect(occupant);
                return;
            }

            if (_selected == null || cell.IsWall)
            {
                Deselect();
                return;
            }

            // 장애물 클릭 → 공격할 수 있으면 공격 후보. 아니면 공격을 요청해 거부 이유를 알린다(실행되지 않으며 선택은 유지).
            if (cell.HasObstacle)
            {
                if (_attackObstacles.Contains(clicked.Value))
                    SetActionCandidate(CandidateKind.AttackObstacle, clicked.Value, null, null);
                else
                    _session.TryAttackObstacle(_selected, clicked.Value);
                return;
            }

            // 공격 가능한 적 → 공격 후보
            if (occupant != null)
            {
                SetActionCandidate(CandidateKind.Attack, clicked.Value, occupant, null);
                return;
            }

            // 빈 칸 → 이동할 수 있으면 이동 후보(경로와 비용), 아니면 이유를 알리고 선택 해제
            MovePreview move = _session.Game.GetMovePreview(_selected.Id, clicked.Value);
            if (move.FailReason == MoveFailReason.None)
            {
                SetActionCandidate(CandidateKind.Move, clicked.Value, null, move);
                return;
            }

            _session.ReportFailure(FailReasonText.Describe(move.FailReason));
            Deselect();
        }

        private void OnSkillTargetClicked(Position clicked)
        {
            if (_confirmSelf)
            {
                // 이미 후보다. 자기 칸이나 범위를 누르면 그대로 두고, 다른 칸을 누르면 스킬 지정을 끝낸다.
                if (!GetSelfPreview().Contains(clicked) && clicked != _selected.Position.Value)
                    CancelSkill();
                return;
            }

            if (IsSkillComplete)
            {
                OnCompleteSkillClicked(clicked);
                return;
            }

            if (!_skillTargets.Contains(clicked))
            {
                CancelSkill();
                return;
            }

            _chosenTargets.Add(clicked);
            if (IsSkillComplete)
            {
                // 다 골랐다: 후보(범위/비용 표시). 실행은 확정 버튼으로.
                Redraw();
                SelectionChanged?.Invoke();
                return;
            }

            RefreshCandidates();
        }

        /// <summary>
        /// 다 고른 스킬(후보)에서 칸을 다시 눌렀다. 마지막 단계의 다른 칸이면 마지막 칸만 바꾸고,
        /// 여러 칸 지정 스킬의 첫 단계 칸이면 그 칸부터 다시 고른다. 어느 쪽도 아니면 스킬 지정을 끝낸다.
        /// </summary>
        private void OnCompleteSkillClicked(Position clicked)
        {
            if (_skillTargets.Contains(clicked))
            {
                _chosenTargets[_chosenTargets.Count - 1] = clicked;
                Redraw();
                SelectionChanged?.Invoke();
                return;
            }

            if (_targetCount > 1 && _session.Game.GetSkillTargets(_selected.Id, _pendingSkillId).Contains(clicked))
            {
                _chosenTargets.Clear();
                _chosenTargets.Add(clicked);
                RefreshCandidates();
                SelectionChanged?.Invoke();
                return;
            }

            CancelSkill();
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
            SelectionChanged?.Invoke();
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
                SelectionChanged?.Invoke();
            }
            else if (_pendingSkillId != null)
                CancelSkill();
            else if (_attackPending && _actionCandidate != CandidateKind.None)
            {
                // 평타 후보만 지우고 대상 고르기로 돌아간다.
                ClearActionCandidate();
                Redraw();
                SelectionChanged?.Invoke();
            }
            else if (_actionCandidate != CandidateKind.None || _attackPending)
                CancelCandidate();
            else
                Deselect();
        }

        /// <summary>
        /// 탭이 유닛 몸통(unitCell)과 그 뒤 바닥 칸(floorCell)에 함께 걸렸을 때 쓸 칸(BoardInput.ResolveOverlap).
        /// 유닛이 지금 행동 대상이면 유닛, 아니면 바닥 칸이 행동 대상일 때 바닥, 둘 다 아니면 유닛(선택/정보 보기).
        /// 예: 선택한 유닛 자신이나 공격할 수 없는 적의 머리가 이동 칸을 가리면 이동 칸이 눌린다.
        /// </summary>
        private Position ResolveOverlap(Position unitCell, Position floorCell)
        {
            if (IsActionable(unitCell, isUnit: true))
                return unitCell;
            if (IsActionable(floorCell, isUnit: false))
                return floorCell;
            return unitCell;
        }

        /// <summary>
        /// 지금 누르면 행동(후보 고르기, 지정, 다른 아군 선택)이 되는 칸인지. 선택한 유닛이 없거나 조작할 차례가 아니면 어느 칸도 아니다.
        /// isUnit 이면 그 칸의 유닛을 누른 것: 다른 아군은 선택 전환 대상이다(선택한 유닛 자신은 아니다).
        /// </summary>
        private bool IsActionable(Position cell, bool isUnit)
        {
            GameState state = _session.State;
            if (!AcceptsInput(state) || state.IsGameOver || _selected == null || !_selected.IsPlaced)
                return false;

            if (_pendingSkillId != null)
            {
                if (_confirmSelf)
                    return cell == _selected.Position.Value || GetSelfPreview().Contains(cell);
                if (_skillTargets.Contains(cell))
                    return true;
                return IsSkillComplete && _targetCount > 1 && _session.Game.GetSkillTargets(_selected.Id, _pendingSkillId).Contains(cell);
            }

            bool attackable = _attackTargets.Exists(target => target.Position == cell) || _attackObstacles.Contains(cell);
            if (_attackPending || attackable)
                return attackable;

            if (isUnit)
            {
                Unit occupant = state.Board.GetCell(cell).Occupant;
                return occupant != null && occupant.Team == state.CurrentTeam && occupant != _selected;
            }

            return _moveDestinations.Contains(cell);
        }

        /// <summary>
        /// 사람이 조작할 차례인지. 배치 단계, 이 화면에서 조작하지 않는 팀(봇, 온라인 상대)의 턴,
        /// 서버 결과를 기다리거나 행동을 연출하는 중에는 입력을 받지 않는다.
        /// </summary>
        private bool AcceptsInput(GameState state)
        {
            if (state == null || state.Phase == GamePhase.Setup || _session.IsBusy)
                return false;
            return !(state.Phase == GamePhase.Battle && !_session.IsLocallyControlled(state.CurrentTeam));
        }

        /// <summary>
        /// 미는 스킬(자리 교정 등)의 예상 도착 칸을 표시한다: 다 골랐으면 고른 칸으로, 아니면 마지막 지정 칸에 마우스를 올렸을 때.
        /// 막혀서 밀리지 않으면 대상의 현재 칸이 표시된다. 미는 스킬이 아니면 코어가 null 을 돌려줘 아무것도 하지 않는다.
        /// </summary>
        private void HighlightPushPreview()
        {
            List<Position> targets;
            if (IsSkillComplete && !_confirmSelf)
                targets = new List<Position>(_chosenTargets);
            else
            {
                Position? hovered = _input.HoveredCell;
                if (!hovered.HasValue || !_skillTargets.Contains(hovered.Value) || _chosenTargets.Count != _targetCount - 1)
                    return;

                targets = new List<Position>(_chosenTargets) { hovered.Value };
            }

            Position? landing = _session.Game.GetPushPreview(_selected.Id, _pendingSkillId, targets);
            if (landing.HasValue)
                _boardView.Highlight(landing.Value, CellHighlight.PushDestination);
        }

        private void OnHoverChanged(Position? hovered)
        {
            if (_pendingSkillId != null && !IsSkillComplete)
                Redraw();
        }

        #endregion

        /// <summary>행동 성공/턴 종료/새 경기 후 선택을 현재 상태에 맞춘다. 스킬 지정 모드와 후보는 끝낸다.</summary>
        private void OnStateChanged()
        {
            GameState state = _session.State;

            // 온라인은 상태가 바뀔 때마다 경기를 새로 만든다(서버 스냅샷 복원). 붙잡고 있던 유닛을 같은 Id 의 새 객체로 바꾼다.
            _selected = _selected != null ? state?.FindUnit(_selected.Id) : null;
            _inspected = _inspected != null ? state?.FindUnit(_inspected.Id) : null;

            Unit unit = _selected;
            ClearActionCandidate();
            ClearSkillMode();

            if (unit == null || !unit.IsPlaced || state.IsGameOver || unit.Team != state.CurrentTeam)
            {
                // 보던 유닛은 살아 있으면 계속 보여준다(봇 턴 동안 지켜보기 등).
                Unit inspected = _inspected;
                Deselect();
                if (inspected != null && inspected.IsPlaced && state != null && !state.IsGameOver)
                    Inspect(inspected);
                return;
            }

            Select(unit);
        }

        /// <summary>행동 없이 유닛 정보만 보여준다(상대 유닛, 봇 턴 중 유닛 등). 칸만 표시하고 이동/공격 범위는 그리지 않는다.</summary>
        private void Inspect(Unit unit)
        {
            _selected = null;
            _inspected = unit;
            _moveDestinations.Clear();
            _attackTargets.Clear();
            _attackObstacles.Clear();
            ClearActionCandidate();
            ClearSkillMode();

            Redraw();
            SelectionChanged?.Invoke();
        }

        private void Select(Unit unit)
        {
            _selected = unit;
            _inspected = null;
            ClearActionCandidate();
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

        /// <summary>일반 모드의 후보(이동/공격/장애물 공격)를 고른다. 다른 후보가 있었으면 바꾼다.</summary>
        private void SetActionCandidate(CandidateKind kind, Position cell, Unit target, MovePreview move)
        {
            _actionCandidate = kind;
            _candidateCell = cell;
            _candidateTarget = target;
            _movePreview = move;
            Redraw();
            SelectionChanged?.Invoke();
        }

        private void ClearActionCandidate()
        {
            _actionCandidate = CandidateKind.None;
            _candidateTarget = null;
            _movePreview = null;
        }

        /// <summary>
        /// 보고 있는 유닛(선택 또는 정보만 보는 유닛)의 스킬 사거리를 미리 그린다(스킬 버튼에 마우스를 올렸을 때).
        /// 대상이 없거나 지금 쓸 수 없어도 사거리는 보이고, 내 유닛이면 지금 지정할 수 있는 칸을 더 진하게 그린다. 지정 중인 스킬이 있으면 그리지 않는다.
        /// </summary>
        public void PreviewSkill(string skillId)
        {
            if (_previewSkillId == skillId)
                return;

            _previewSkillId = skillId;
            Redraw();
        }

        /// <summary>skillId 미리보기를 끈다(다른 스킬로 바뀌었으면 그대로 둔다).</summary>
        public void ClearPreview(string skillId)
        {
            if (_previewSkillId != skillId)
                return;

            _previewSkillId = null;
            Redraw();
        }

        /// <summary>지정 모드(스킬, 평타)를 끝낸다.</summary>
        private void ClearSkillMode()
        {
            _attackPending = false;
            _pendingSkillId = null;
            _skillTargets.Clear();
            _chosenTargets.Clear();
            _targetCount = 0;
            _confirmSelf = false;
            _canRotate = false;
            _rotation = AreaRotation.R0;
        }

        private int GetSkillCost(string skillId)
        {
            return skillId != null ? _session.State?.Skills.Find(skillId)?.ApCost ?? 0 : 0;
        }

        private void Redraw()
        {
            DrawHighlights();
            PreviewChanged?.Invoke();
        }

        private void DrawHighlights()
        {
            _boardView.ClearHighlights();

            // 후보나 지정 모드가 있으면 버튼 사거리 미리보기보다 그것을 먼저 보여준다.
            bool idle = _pendingSkillId == null && !_attackPending && _actionCandidate == CandidateKind.None;
            if (idle && _previewSkillId != null && DrawSkillPreview())
                return;
            if (idle && _previewAttack && DrawAttackPreview())
                return;

            if (_selected == null && _inspected != null && _inspected.IsPlaced)
            {
                _boardView.Highlight(_inspected.Position.Value, CellHighlight.Selected);
                return;
            }

            if (_selected == null || !_selected.IsPlaced)
                return;

            _boardView.Highlight(_selected.Position.Value, CellHighlight.Selected);

            if (_pendingSkillId != null)
            {
                DrawSkillTargeting();
                return;
            }

            // 평타 지정 중에는 이동 칸을 숨기고 사거리만 옅게 깐다.
            if (_attackPending)
            {
                foreach (Position cell in _session.Game.GetAttackReach(_selected.Id))
                    _boardView.Highlight(cell, CellHighlight.AttackRange);
            }
            else
            {
                foreach (Position destination in _moveDestinations)
                    _boardView.Highlight(destination, CellHighlight.Move);
            }
            foreach (Unit target in _attackTargets)
                _boardView.Highlight(target.Position.Value, CellHighlight.Attack);
            foreach (Position obstacle in _attackObstacles)
                _boardView.Highlight(obstacle, CellHighlight.Attack);

            // 후보: 이동은 지나갈 경로와 도착 칸, 공격은 대상 칸.
            if (_actionCandidate == CandidateKind.Move && _movePreview != null)
            {
                foreach (Position step in _movePreview.Path)
                    _boardView.Highlight(step, CellHighlight.Path);
            }
            if (_actionCandidate != CandidateKind.None)
                _boardView.Highlight(_candidateCell, CellHighlight.Candidate);
        }

        /// <summary>스킬 지정 중: 지정 가능 칸, 범위(자기 지정은 자기 칸, 다 골랐으면 고른 칸, 아니면 마우스를 올린 칸 기준), 고른 칸, 밀려날 칸.</summary>
        private void DrawSkillTargeting()
        {
            if (!IsSkillComplete || _confirmSelf)
            {
                foreach (Position target in _skillTargets)
                    _boardView.Highlight(target, CellHighlight.SkillTarget);
            }

            if (_confirmSelf)
            {
                foreach (Position cell in GetSelfPreview())
                    _boardView.Highlight(cell, CellHighlight.SkillArea);
            }
            else
            {
                Position? areaTarget = IsSkillComplete ? _chosenTargets[_chosenTargets.Count - 1] : _input.HoveredCell;
                if (areaTarget.HasValue && (IsSkillComplete || _skillTargets.Contains(areaTarget.Value)))
                {
                    foreach (Position cell in _session.Game.GetSkillArea(_selected.Id, _pendingSkillId, areaTarget.Value, _rotation))
                        _boardView.Highlight(cell, CellHighlight.SkillArea);
                }
            }

            foreach (Position chosen in _chosenTargets)
                _boardView.Highlight(chosen, CellHighlight.SkillChosen);

            HighlightPushPreview();
        }

        /// <summary>기본 공격 사거리(옅게)와, 내 유닛이면 지금 공격할 수 있는 대상(진하게). 보고 있는 유닛이 없으면 false.</summary>
        private bool DrawAttackPreview()
        {
            Unit unit = Displayed;
            GameController game = _session.Game;
            if (unit == null || !unit.IsPlaced || game == null)
                return false;

            _boardView.Highlight(unit.Position.Value, CellHighlight.Selected);
            foreach (Position cell in game.GetAttackReach(unit.Id))
                _boardView.Highlight(cell, CellHighlight.AttackRange);

            if (unit == _selected)
            {
                foreach (Unit target in _attackTargets)
                    _boardView.Highlight(target.Position.Value, CellHighlight.Attack);
                foreach (Position obstacle in _attackObstacles)
                    _boardView.Highlight(obstacle, CellHighlight.Attack);
            }

            return true;
        }

        /// <summary>미리보기 스킬의 사거리(옅게)와, 내 유닛이면 지금 지정할 수 있는 칸(진하게). 보고 있는 유닛이 없으면 false.</summary>
        private bool DrawSkillPreview()
        {
            Unit unit = Displayed;
            GameController game = _session.Game;
            if (unit == null || !unit.IsPlaced || game == null)
                return false;

            _boardView.Highlight(unit.Position.Value, CellHighlight.Selected);
            foreach (Position cell in game.GetSkillReach(unit.Id, _previewSkillId))
                _boardView.Highlight(cell, CellHighlight.SkillRange);

            if (unit == _selected)
            {
                foreach (Position cell in game.GetSkillTargets(unit.Id, _previewSkillId))
                    _boardView.Highlight(cell, CellHighlight.SkillTarget);
            }

            return true;
        }
    }
}
