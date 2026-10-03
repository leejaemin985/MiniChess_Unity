using System;
using UnityEngine;

namespace MiniChess
{
    // Base component for unit prefabs. Rules live in pure C# systems.
    public abstract class Unit : MonoBehaviour
    {
        [SerializeField] private float heightOffset = 0.55f;
        private Board board;
        public UnitState State { get; private set; }
        protected abstract UnitDefinition DefaultDefinition { get; }
        public UnitDefinition Definition => State?.Definition ?? DefaultDefinition;

        public void Initialize(UnitState state, Board targetBoard)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Definition.Kind != DefaultDefinition.Kind)
                throw new ArgumentException("Unit component and state kind must match.", nameof(state));
            if (targetBoard == null) throw new ArgumentNullException(nameof(targetBoard));
            if (state.X >= targetBoard.BoardSize || state.Y >= targetBoard.BoardSize)
                throw new ArgumentException("Unit position is outside the board.", nameof(state));
            if (State != null) State.Changed -= Refresh;
            State = state;
            board = targetBoard;
            transform.SetParent(board.transform, false);
            gameObject.name = $"{State.Owner}_{Definition.Name}_{State.UnitIndex}";
            State.Changed += Refresh;
            Refresh();
        }

        // Subclasses can extend this for HP bars, animation, and selection visuals.
        public virtual void Refresh()
        {
            if (State == null || board == null) return;
            transform.localPosition = new Vector3(
                State.X * board.CellSize, heightOffset, State.Y * board.CellSize);
            gameObject.SetActive(State.IsAlive);
        }

        protected virtual void OnDestroy()
        {
            if (State != null) State.Changed -= Refresh;
        }
    }
}
