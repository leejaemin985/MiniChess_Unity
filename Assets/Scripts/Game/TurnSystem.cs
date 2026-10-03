using System;

namespace MiniChess
{
    public sealed class TurnSystem
    {
        private readonly Random random;
        public GameState State { get; }
        public event Action StateChanged;

        public TurnSystem(GameState state, Random random = null)
        { State = state ?? throw new ArgumentNullException(nameof(state)); this.random = random ?? new Random(); }

        public bool TryStartBattle()
        {
            if (!State.IsDeploymentReady()) return false;
            return TryStartBattle((PlayerId)random.Next(2));
        }

        public bool TryStartBattle(PlayerId firstPlayer)
        {
            if (!State.IsDeploymentReady() || !IsValidPlayer(firstPlayer)) return false;
            State.Phase = GamePhase.Battle;
            BeginTurn(firstPlayer);
            NotifyStateChanged();
            return true;
        }

        public bool CanMove(PlayerId player, int unitIndex)
        {
            return CanAct(player, unitIndex) && !State.GetUnit(player, unitIndex).HasCombatActedThisTurn
                && !State.GetUnit(player, unitIndex).MovementStoppedThisTurn
                && State.GetAP(player) >= State.Rules.AP.MoveAPPerTile
                && GetRemainingMoveDistance(player, unitIndex) >= 1;
        }

        // Tiles this unit may still cover with basic moves this turn.
        public int GetRemainingMoveDistance(PlayerId player, int unitIndex)
        {
            UnitState unit = State.GetUnit(player, unitIndex);
            return unit == null ? 0 : State.Rules.AP.RemainingMoveDistance(unit.MoveTilesThisTurn);
        }

        public bool CanCombat(PlayerId player, int unitIndex, int? apCost = null)
        {
            int cost = apCost ?? State.Rules.AP.AttackAP;
            return cost > 0 && CanAct(player, unitIndex)
                && !State.GetUnit(player, unitIndex).HasCombatActedThisTurn && State.GetAP(player) >= cost;
        }

        // Resource commits only. BattleSystem validates movement paths and targets first.
        public bool TryConsumeMove(PlayerId player, int unitIndex, int distance = 1, bool notify = true)
        {
            if (distance < 1 || !CanMove(player, unitIndex)
                || distance > GetRemainingMoveDistance(player, unitIndex)) return false;
            long cost = (long)distance * State.Rules.AP.MoveAPPerTile;
            if (cost > State.GetAP(player)) return false;
            State.GetPlayerTurn(player).CurrentAP -= (int)cost;
            UnitState mover = State.GetUnit(player, unitIndex);
            mover.HasMovedThisTurn = true;
            mover.MoveTilesThisTurn += distance;
            mover.LongestMoveThisTurn = Math.Max(mover.LongestMoveThisTurn, distance);
            if (notify) NotifyStateChanged();
            return true;
        }

        // Movement skills spend AP without the combat action or the basic-move budget,
        // so a unit may still attack after leaping or charging.
        public bool TryConsumeSkillMove(PlayerId player, int unitIndex, int apCost, int distance, bool notify = true)
        {
            if (apCost < 1 || distance < 1 || !CanAct(player, unitIndex)) return false;
            UnitState mover = State.GetUnit(player, unitIndex);
            if (mover.HasCombatActedThisTurn || mover.MovementStoppedThisTurn
                || State.GetAP(player) < apCost) return false;
            State.GetPlayerTurn(player).CurrentAP -= apCost;
            mover.HasMovedThisTurn = true;
            mover.LongestMoveThisTurn = Math.Max(mover.LongestMoveThisTurn, distance);
            if (notify) NotifyStateChanged();
            return true;
        }

        public bool TryConsumeCombat(PlayerId player, int unitIndex, int? apCost = null, bool notify = true)
        {
            int cost = apCost ?? State.Rules.AP.AttackAP;
            if (!CanCombat(player, unitIndex, cost)) return false;
            State.GetPlayerTurn(player).CurrentAP -= cost;
            State.GetUnit(player, unitIndex).HasCombatActedThisTurn = true;
            if (notify) NotifyStateChanged();
            return true;
        }

        public bool TryEndTurn(PlayerId player)
        {
            if (State.Phase != GamePhase.Battle || State.CurrentPlayer != player) return false;
            int completed = State.GetTurnCount(player);
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                State.GetUnit(player, index)?.ExpireShield(completed);
                // Echo only lasts the turn it was granted in.
                UnitState unit = State.GetUnit(player, index);
                if (unit != null) unit.EchoCharges = 0;
            }
            for (int side = 0; side < 2; side++)
                for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                    State.GetUnit((PlayerId)side, index)?.ExpireTrapMark(player, completed);
            BeginTurn(player == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne);
            NotifyStateChanged();
            for (int side = 0; side < 2; side++)
                for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                    State.GetUnit((PlayerId)side, index)?.NotifyChanged();
            return true;
        }

        public bool TryFinishBattle(PlayerId winner)
        {
            if (State.Phase != GamePhase.Battle || !IsValidPlayer(winner)) return false;
            State.Phase = GamePhase.Finished;
            State.Winner = winner;
            State.CurrentPlayer = null;
            NotifyStateChanged();
            return true;
        }

        // A side that has lost every unit loses the match. Checked after any damage.
        internal bool CheckElimination(PlayerId lastActor)
        {
            if (State.Phase != GamePhase.Battle) return false;
            PlayerId other = lastActor == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
            if (State.IsWipedOut(other)) return TryFinishBattle(lastActor);
            if (State.IsWipedOut(lastActor)) return TryFinishBattle(other);
            return false;
        }

        private bool CanAct(PlayerId player, int unitIndex) => State.Phase == GamePhase.Battle
            && State.CurrentPlayer == player && State.GetUnit(player, unitIndex)?.IsAlive == true;

        internal void NotifyStateChanged() => StateChanged?.Invoke();

        private void BeginTurn(PlayerId player)
        {
            // Both sides' first own turn starts at StartAP, without an extra recovery.
            PlayerTurnState turn = State.GetPlayerTurn(player);
            if (State.GetTurnCount(player) > 0)
                turn.CurrentAP = (int)Math.Min(turn.MaxAP, (long)turn.CurrentAP + turn.RecoveryAP);
            State.CurrentPlayer = player;
            State.TurnNumber++;
            if (player == PlayerId.PlayerOne) State.PlayerOneTurnCount++;
            else State.PlayerTwoTurnCount++;
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                UnitState unit = State.GetUnit(player, index);
                if (unit == null) continue;
                unit.HasMovedThisTurn = false;
                unit.HasCombatActedThisTurn = false;
                unit.MoveTilesThisTurn = 0;
                unit.LongestMoveThisTurn = 0;
                unit.MovementStoppedThisTurn = false;
            }
            // Fields the opponent laid burn first; the caster's own fields then expire.
            BurnUnitsAtTurnStart(player);
            State.ExpireAreas(player);
            // Scorch from enemy basic attacks lands now, after fire, and resolves from
            // the unit's own state so losing the capture point cannot cancel it.
            ApplyAttackBurn(player);
            // Last, so a capture buff sees the AP this turn actually starts with and
            // acts on units that have already taken their burn damage.
            AdvanceCapturePoint(player);
        }

        // A round of holding is one of the holder's own turn starts: step onto the tile
        // and you have held it once your next turn comes round. Occupancy is re-read at
        // every turn start, so the other side walking on resets the count immediately.
        private void AdvanceCapturePoint(PlayerId player)
        {
            // A turn-start burn can end the match; no point buffing a finished board.
            if (!State.HasCapturePoint || State.Phase != GamePhase.Battle) return;
            CapturePointRules rules = State.Rules.Capture;
            UnitState occupant = State.GetUnitAt(State.CaptureX, State.CaptureY);
            PlayerId? standing = occupant?.Owner;
            if (standing != State.CaptureHolder)
            {
                State.CaptureHolder = standing;
                State.CaptureHeldRounds = 0;
            }
            // Losing the tile gives the point up unless the rules say a capture sticks.
            if (State.CaptureOwner.HasValue && standing != State.CaptureOwner && !rules.KeepAfterLeaving)
            {
                PlayerId lost = State.CaptureOwner.Value;
                State.CaptureOwner = null;
                rules.Effect.OnLost(State, lost);
            }
            if (State.CaptureHolder == player)
            {
                State.CaptureHeldRounds++;
                if (State.CaptureHeldRounds >= rules.RoundsToCapture && State.CaptureOwner != player)
                {
                    State.CaptureOwner = player;
                    rules.Effect.OnCaptured(State, player);
                }
            }
            if (State.CaptureOwner == player) rules.Effect.OnOwnerTurnStart(State, player);
        }

        private void ApplyAttackBurn(PlayerId player)
        {
            int turnCount = State.GetTurnCount(player);
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                UnitState unit = State.GetUnit(player, index);
                if (unit == null || !unit.IsAlive || unit.AttackBurnDamage <= 0
                    || turnCount < unit.AttackBurnAtOwnTurn) continue;
                int damage = unit.AttackBurnDamage;
                unit.AttackBurnDamage = 0;
                unit.AttackBurnAtOwnTurn = 0;
                // Scorch bypasses nothing: shields and echo charges absorb it as usual.
                unit.ApplyDamage(damage, false);
            }
            CheckElimination(player);
        }

        private void BurnUnitsAtTurnStart(PlayerId player)
        {
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
            {
                UnitState unit = State.GetUnit(player, index);
                if (unit == null || !unit.IsAlive) continue;
                foreach (AreaEffect area in State.Areas)
                {
                    if (area.Owner == player || !area.Covers(unit.X, unit.Y)) continue;
                    if (!area.TryBurn(unit, State.TurnNumber)) continue;
                    unit.ApplyDamage(area.Damage, false);
                    if (!unit.IsAlive) break;
                }
            }
            CheckElimination(player);
        }

        private static bool IsValidPlayer(PlayerId player) => player == PlayerId.PlayerOne || player == PlayerId.PlayerTwo;
    }
}
