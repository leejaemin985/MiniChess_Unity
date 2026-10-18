using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Client.Board;
using MiniChess.Client.Bootstrap;
using MiniChess.Core.Actions;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Data;
using MiniChess.Core.State;
using MiniChess.Core.Turns;
using UnityEngine;

namespace MiniChess.Client.Interaction
{
    /// <summary>
    /// 배치(Setup) 단계의 입력. 한 화면 교대 플레이라 선공 팀부터 한 팀씩 배치한다. 봇 팀은 BotDriver 가 배치한다.
    ///   캐릭터 버튼 선택 → 자기 시작 칸(하이라이트) 클릭 → 배치. 놓인 칸을 다시 클릭하면 빼고 그 캐릭터를 다시 고른 상태가 된다.
    ///   팀 인원을 다 놓으면 확정할 수 있고, 확정은 코어 배치 요청으로 보낸다(규칙 검사는 코어가 한다).
    /// 확정 전 배치는 클라이언트에만 있는 임시 목록이다.
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        private GameSession _session;
        private BoardInput _input;
        private BoardView _boardView;

        private readonly Dictionary<Position, string> _pending = new Dictionary<Position, string>();
        private string _selectedCharacterId;
        private GameController _game;

        /// <summary>지금 배치 중인 팀. 배치 단계가 아니면 null.</summary>
        public Team? PlacingTeam { get; private set; }

        /// <summary>확정 전 배치(칸 → 캐릭터 Id).</summary>
        public IReadOnlyDictionary<Position, string> Pending => _pending;

        /// <summary>다음에 놓을 캐릭터. 없으면 null.</summary>
        public string SelectedCharacterId => _selectedCharacterId;

        public int RequiredCount => _session.State?.Rules.Match.UnitsPerTeam ?? 0;
        public bool CanConfirm => PlacingTeam.HasValue && _pending.Count == RequiredCount;

        /// <summary>배치 중인 팀, 임시 배치, 선택 캐릭터가 바뀌었다.</summary>
        public event Action Changed;

        public void Initialize(GameSession session, BoardInput input, BoardView boardView)
        {
            Unbind();

            _session = session;
            _input = input;
            _boardView = boardView;

            _input.CellClicked += OnCellClicked;
            _input.CancelClicked += OnCancelClicked;
            _session.StateChanged += OnStateChanged;
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
            }

            if (_session != null)
                _session.StateChanged -= OnStateChanged;
        }

        /// <summary>다음에 놓을 캐릭터를 고른다. 같은 캐릭터를 다시 고르면 선택을 푼다.</summary>
        public void SelectCharacter(string characterId)
        {
            if (!PlacingTeam.HasValue)
                return;

            _selectedCharacterId = _selectedCharacterId == characterId ? null : characterId;
            Changed?.Invoke();
        }

        public bool IsPlaced(string characterId)
        {
            return _pending.ContainsValue(characterId);
        }

        /// <summary>임시 배치를 코어에 확정 요청한다. 성공하면 다음 팀 배치 또는 Battle 로 넘어간다.</summary>
        public void Confirm()
        {
            if (!PlacingTeam.HasValue)
                return;

            List<UnitPlacement> placements = _pending
                .Select(pair => new UnitPlacement(pair.Value, pair.Key))
                .ToList();

            // 성공하면 StateChanged → OnStateChanged 에서 다음 팀으로 넘어간다.
            _session.TryPlaceUnits(PlacingTeam.Value, placements);
        }

        private void OnStateChanged()
        {
            GameState state = _session.State;
            Team? team = state != null && state.Phase == GamePhase.Setup ? GetNextPlacingTeam(state) : null;

            // 봇 팀의 배치는 BotDriver 가 한다. 사람 배치 UI 는 비활성.
            if (team.HasValue && _session.IsBot(team.Value))
                team = null;

            // 다음 팀으로 넘어가거나 새 경기가 시작되면 임시 배치를 비운다.
            if (team != PlacingTeam || _session.Game != _game)
            {
                _pending.Clear();
                _selectedCharacterId = null;
            }

            // 배치 중이었거나 배치 중일 때만 다시 그린다. 전투 중에는 하이라이트가 SelectionController 몫이다.
            bool wasPlacing = PlacingTeam.HasValue;
            PlacingTeam = team;
            _game = _session.Game;
            if (wasPlacing || team.HasValue)
                Redraw();
            Changed?.Invoke();
        }

        /// <summary>선공 팀부터, 아직 배치하지 않은 팀. 둘 다 배치했으면 null.</summary>
        public static Team? GetNextPlacingTeam(GameState state)
        {
            Team first = state.Rules.Match.FirstTeam;
            if (!state.GetPlayer(first).HasPlaced)
                return first;

            Team second = TurnSystem.GetOpponent(first);
            return state.GetPlayer(second).HasPlaced ? (Team?)null : second;
        }

        private void OnCellClicked(Position? clicked)
        {
            if (!PlacingTeam.HasValue || !clicked.HasValue)
                return;

            Position cell = clicked.Value;

            if (_pending.TryGetValue(cell, out string placedId))
            {
                // 놓인 캐릭터를 빼고 다시 고른 상태로 둔다.
                _pending.Remove(cell);
                _selectedCharacterId = placedId;
                Redraw();
                Changed?.Invoke();
                return;
            }

            if (!_session.Game.GetPlacementCells(PlacingTeam.Value).Contains(cell))
            {
                _session.ReportFailure("자기 영역(시작 칸)에만 배치할 수 있습니다");
                return;
            }

            if (_selectedCharacterId == null)
            {
                _session.ReportFailure("배치할 캐릭터를 먼저 고르세요");
                return;
            }

            if (!_pending.ContainsValue(_selectedCharacterId) && _pending.Count >= RequiredCount)
            {
                _session.ReportFailure($"팀당 {RequiredCount}명까지 배치할 수 있습니다");
                return;
            }

            // 이미 놓인 캐릭터면 자리를 옮긴다.
            foreach (Position previous in _pending.Where(pair => pair.Value == _selectedCharacterId).Select(pair => pair.Key).ToList())
                _pending.Remove(previous);

            _pending[cell] = _selectedCharacterId;
            _selectedCharacterId = null;
            Redraw();
            Changed?.Invoke();
        }

        private void OnCancelClicked()
        {
            if (!PlacingTeam.HasValue || _selectedCharacterId == null)
                return;

            _selectedCharacterId = null;
            Changed?.Invoke();
        }

        /// <summary>자기 시작 칸은 이동 색, 이미 놓은 칸은 선택 색으로 표시한다.</summary>
        private void Redraw()
        {
            _boardView.ClearHighlights();
            if (!PlacingTeam.HasValue)
                return;

            foreach (Position cell in _session.Game.GetPlacementCells(PlacingTeam.Value))
                _boardView.Highlight(cell, _pending.ContainsKey(cell) ? CellHighlight.Selected : CellHighlight.Move);
        }

        /// <summary>캐릭터를 배치에 쓸 수 있는지(필수 수치가 채워졌는지).</summary>
        public static bool IsConfigured(CharacterDefinition character)
        {
            return character.GetMissingFields().Count == 0;
        }
    }
}
