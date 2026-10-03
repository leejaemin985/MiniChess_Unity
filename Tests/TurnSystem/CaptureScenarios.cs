using MiniChess;

internal static partial class Program
{
    // Records what the rules asked of the buff, so the seam can be tested before any
    // buff exists. A real effect would change the state instead of counting.
    private sealed class SpyCaptureEffect : ICapturePointEffect
    {
        public int Captured, Lost, OwnerTurns, Attacks;
        public PlayerId? LastOwner, LastLoser;
        public string Describe(BattleRules rules) => "시험용 효과";
        public void OnCaptured(GameState state, PlayerId owner) { Captured++; LastOwner = owner; }
        public void OnLost(GameState state, PlayerId previousOwner) { Lost++; LastLoser = previousOwner; }
        public void OnOwnerTurnStart(GameState state, PlayerId owner) { OwnerTurns++; }
        public void OnBasicAttack(GameState state, UnitState attacker, UnitState target) { Attacks++; }
    }

    private static void RunCaptureScenarios()
    {
        Run("A capture point is one designated tile, chosen before the battle", () =>
        {
            var state = new GameState(7, CaptureRules(2));
            Check(!state.HasCapturePoint && state.CaptureTile == -1);
            Check(state.CaptureX == -1 && state.CaptureY == -1);
            Check(state.TrySetCapturePoint(3, 3) && state.IsCaptureTile(3, 3));
            Check(state.CaptureX == 3 && state.CaptureY == 3);
            // Designating again moves it rather than adding a second.
            Check(state.TrySetCapturePoint(4, 2) && state.IsCaptureTile(4, 2));
            Check(!state.IsCaptureTile(3, 3));
            Check(!state.TrySetCapturePoint(-1, 0) && !state.TrySetCapturePoint(0, 7));
            // A wall and the capture tile cannot share a square, in either order.
            Check(state.TryAddObstacle(1, 1));
            Check(!state.TrySetCapturePoint(1, 1));
            Check(state.TrySetCapturePoint(5, 5) && !state.TryAddObstacle(5, 5));
            Check(state.ClearCapturePoint() && !state.HasCapturePoint);
            Check(!state.ClearCapturePoint());
        });
        Run("The layout freezes when the battle starts and survives cloning", () =>
        {
            GameState state = FixedDeployment.Create(7, CaptureRules(2));
            Check(state.TrySetCapturePoint(3, 3));
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            Check(!state.TrySetCapturePoint(4, 4) && !state.ClearCapturePoint());
            Check(state.IsCaptureTile(3, 3));
            Check(state.Clone().IsCaptureTile(3, 3));
        });
        Run("Holding the tile for the configured rounds captures it", () =>
        {
            var spy = new SpyCaptureEffect();
            GameState state = FixedDeployment.Create(7, CaptureRules(2, effect: spy));
            Check(state.TrySetCapturePoint(3, 3));
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            Check(!state.CaptureHolder.HasValue && state.CaptureHeldRounds == 0);
            // Step on during this turn. Nothing is counted until a turn start sees it.
            state.GetUnit(One, 0).SetPosition(3, 2);
            Check(battle.TryMove(One, 0, 3, 3));
            Check(!state.CaptureHolder.HasValue && !state.CaptureOwner.HasValue);
            Check(turns.TryEndTurn(One));
            // Player Two's turn start registers who is standing there, but counts no
            // round for them, since the round belongs to the holder.
            Check(state.CaptureHolder == One && state.CaptureHeldRounds == 0);
            Check(turns.TryEndTurn(Two));
            Check(state.CaptureHeldRounds == 1 && !state.CaptureOwner.HasValue); // Round one.
            Check(spy.Captured == 0);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(state.CaptureHeldRounds == 2 && state.CaptureOwner == One); // Round two.
            Check(spy.Captured == 1 && spy.LastOwner == One);
            // The recurring hook fires on the owner's turn starts, including this one.
            Check(spy.OwnerTurns == 1);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(spy.OwnerTurns == 2 && spy.Captured == 1); // Captured once, not again.
        });
        Run("A one-round setting captures as soon as the next own turn comes round", () =>
        {
            GameState state = FixedDeployment.Create(7, CaptureRules(1));
            Check(state.TrySetCapturePoint(3, 3));
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            state.GetUnit(One, 0).SetPosition(3, 2);
            Check(battle.TryMove(One, 0, 3, 3));
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(state.CaptureOwner == One && state.CaptureHeldRounds == 1);
        });
        Run("Stepping off hands the point back, and the enemy resets the count", () =>
        {
            var spy = new SpyCaptureEffect();
            GameState state = FixedDeployment.Create(7, CaptureRules(1, effect: spy));
            Check(state.TrySetCapturePoint(3, 3));
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            var battle = new BattleSystem(turns);
            state.GetUnit(One, 0).SetPosition(3, 2);
            Check(battle.TryMove(One, 0, 3, 3));
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(state.CaptureOwner == One);
            // Walk off: the next turn start finds the tile empty and the point lapses.
            Check(new BattleSystem(turns).TryMove(One, 0, 3, 2));
            Check(turns.TryEndTurn(One));
            Check(!state.CaptureOwner.HasValue && !state.CaptureHolder.HasValue);
            Check(state.CaptureHeldRounds == 0 && spy.Lost == 1 && spy.LastLoser == One);
            // The other side taking the tile starts its own count from zero.
            state.GetUnit(Two, 0).SetPosition(3, 4);
            Check(new BattleSystem(turns).TryMove(Two, 0, 3, 3));
            Check(turns.TryEndTurn(Two));
            Check(state.CaptureHolder == Two && state.CaptureHeldRounds == 0);
            Check(turns.TryEndTurn(One));
            Check(state.CaptureOwner == Two && spy.LastOwner == Two);
        });
        Run("KeepAfterLeaving makes a capture permanent once earned", () =>
        {
            var spy = new SpyCaptureEffect();
            GameState state = FixedDeployment.Create(7, CaptureRules(1, keep: true, effect: spy));
            Check(state.TrySetCapturePoint(3, 3));
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            state.GetUnit(One, 0).SetPosition(3, 2);
            Check(new BattleSystem(turns).TryMove(One, 0, 3, 3));
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(state.CaptureOwner == One);
            Check(new BattleSystem(turns).TryMove(One, 0, 3, 2));
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(state.CaptureOwner == One && spy.Lost == 0);
        });
        Run("With no capture point designated nothing is tracked or called", () =>
        {
            var spy = new SpyCaptureEffect();
            GameState state = FixedDeployment.Create(7, CaptureRules(1, effect: spy));
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(One));
            for (int i = 0; i < 3; i++) Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(!state.CaptureHolder.HasValue && !state.CaptureOwner.HasValue);
            Check(spy.Captured == 0 && spy.Lost == 0 && spy.OwnerTurns == 0);
        });
        Run("Capture rules validate their settings and describe themselves in Korean", () =>
        {
            Reject(() => new CapturePointRules(roundsToCapture: 0));
            Reject(() => new CapturePointRules(roundsToCapture: -1));
            var rules = new CapturePointRules();
            Check(rules.RoundsToCapture == 2 && !rules.KeepAfterLeaving);
            // No effect supplied means the no-op, never null, so the rules never check.
            Check(rules.Effect == NoCapturePointEffect.Instance);
            CheckKorean(rules.GetSummary());
            CheckKorean(rules.Effect.Describe(new BattleRules()));
            Check(rules.GetSummary().Contains("2라운드"));
            Check(new CapturePointRules(3, true).GetSummary().Contains("유지"));
            // BattleRules always hands out capture rules, so callers need no fallback.
            Check(new BattleRules().Capture != null);
        });
        Run("Burn buff: a basic attack scorches on the victim's next turn, once", () =>
        {
            var turns = CapturedPointBattle(One, new BurnOnAttackCaptureEffect(2));
            GameState state = turns.State;
            var battle = new BattleSystem(turns);
            UnitState attacker = state.GetUnit(One, 0);
            UnitState victim = state.GetUnit(Two, 0);
            attacker.SetPosition(2, 3);
            victim.SetPosition(2, 4);
            int hp = victim.HP;
            int damage = battle.GetAttackDamage(One, 0, Two, 0);
            Check(battle.TryAttack(One, 0, Two, 0));
            // The hit lands now; the scorch is only pencilled in.
            Check(victim.HP == hp - damage);
            Check(victim.AttackBurnDamage == 2);
            Check(turns.TryEndTurn(One));
            // Player Two's turn starts: the scorch lands and clears.
            Check(victim.HP == hp - damage - 2 && victim.AttackBurnDamage == 0);
            Check(turns.TryEndTurn(Two) && turns.TryEndTurn(One));
            Check(victim.HP == hp - damage - 2); // It does not tick a second time.
        });
        Run("Burn buff: two hits stack, skills do not scorch, and losing the point keeps it", () =>
        {
            var turns = CapturedPointBattle(One, new BurnOnAttackCaptureEffect(2));
            GameState state = turns.State;
            var battle = new BattleSystem(turns);
            UnitState victim = state.GetUnit(Two, 0);
            victim.SetPosition(2, 4);
            state.GetUnit(One, 0).SetPosition(2, 3);
            state.GetUnit(One, 1).SetPosition(1, 3);
            Check(battle.TryAttack(One, 0, Two, 0));
            Check(battle.TryAttack(One, 1, Two, 0));
            Check(victim.AttackBurnDamage == 4); // Both hits are owed.
            int owed = victim.AttackBurnDamage;
            // Give the point up before the scorch lands: already-inflicted burn stays.
            Check(new BattleSystem(turns).TryMove(One, 3, 3, 2));
            Check(turns.TryEndTurn(One));
            Check(victim.AttackBurnDamage == 0); // It landed at Two's turn start.
            Check(owed == 4);
        });
        Run("Burn buff: only the owning side scorches, and shields soak it", () =>
        {
            // Nobody owns the point, so a basic attack leaves nothing behind.
            GameState open = FixedDeployment.Create(7, CaptureRules(2,
                effect: new BurnOnAttackCaptureEffect(2)));
            Check(open.TrySetCapturePoint(0, 3));
            var openTurns = new TurnSystem(open);
            Check(openTurns.TryStartBattle(One));
            var openBattle = new BattleSystem(openTurns);
            open.GetUnit(One, 0).SetPosition(2, 3);
            open.GetUnit(Two, 0).SetPosition(2, 4);
            Check(openBattle.TryAttack(One, 0, Two, 0));
            Check(open.GetUnit(Two, 0).AttackBurnDamage == 0);
            // With a shield up, the scorch is absorbed like any other damage.
            var turns = CapturedPointBattle(One, new BurnOnAttackCaptureEffect(2));
            GameState state = turns.State;
            var battle = new BattleSystem(turns);
            UnitState victim = state.GetUnit(Two, 0);
            victim.SetPosition(2, 4);
            state.GetUnit(One, 0).SetPosition(2, 3);
            victim.GrantAuraShield(9);
            int hp = victim.HP;
            Check(battle.TryAttack(One, 0, Two, 0));
            Check(turns.TryEndTurn(One));
            Check(victim.HP == hp && victim.Shield > 0); // All of it soaked.
        });
        Run("Shield buff: capturing shields the whole side and losing it takes it back", () =>
        {
            var effect = new PermanentShieldCaptureEffect(3);
            var turns = CapturedPointBattle(One, effect);
            GameState state = turns.State;
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                Check(state.GetUnit(One, index).Shield == 3);
            // The other side gets nothing from it.
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                Check(state.GetUnit(Two, index).Shield == 0);
            // Spending the shield in a fight is topped back up next own turn start.
            UnitState holder = state.GetUnit(One, 3);
            holder.TakeDamage(2);
            Check(holder.Shield == 1);
            Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(holder.Shield == 3 && state.CaptureOwner == One);
            // Step off: the point lapses and the standing shield goes with it.
            Check(new BattleSystem(turns).TryMove(One, 3, 3, 2));
            Check(turns.TryEndTurn(One));
            Check(!state.CaptureOwner.HasValue);
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                Check(state.GetUnit(One, index).Shield == 0);
        });
        Run("Shield buff: a standing shield does not expire on the Guardian's timer", () =>
        {
            var turns = CapturedPointBattle(One, new PermanentShieldCaptureEffect(3));
            GameState state = turns.State;
            UnitState unit = state.GetUnit(One, 0);
            Check(unit.Shield == 3);
            // Three full rounds with no skill involved: a timed shield would be gone.
            for (int i = 0; i < 3; i++) Check(turns.TryEndTurn(One) && turns.TryEndTurn(Two));
            Check(unit.Shield == 3 && state.CaptureOwner == One);
        });
        Run("Both buffs run together, and each can be described in Korean", () =>
        {
            var both = new CompositeCapturePointEffect(
                new BurnOnAttackCaptureEffect(2), new PermanentShieldCaptureEffect(3));
            Check(both.Count == 2);
            CheckKorean(both.Describe(new BattleRules()));
            Check(both.Describe(new BattleRules()).Contains("화상"));
            Check(both.Describe(new BattleRules()).Contains("보호막"));
            var turns = CapturedPointBattle(One, both);
            GameState state = turns.State;
            var battle = new BattleSystem(turns);
            Check(state.GetUnit(One, 0).Shield == 3); // Shield half fired on capture.
            UnitState victim = state.GetUnit(Two, 0);
            victim.SetPosition(2, 4);
            state.GetUnit(One, 0).SetPosition(2, 3);
            Check(battle.TryAttack(One, 0, Two, 0));
            Check(victim.AttackBurnDamage == 2);   // Burn half fired on the attack.
            // An empty composite falls back to saying nothing happens.
            Check(new CompositeCapturePointEffect().Count == 0);
            CheckKorean(new CompositeCapturePointEffect().Describe(new BattleRules()));
            Reject(() => new BurnOnAttackCaptureEffect(0));
            Reject(() => new PermanentShieldCaptureEffect(0));
        });
        Run("Burn and shield amounts are settings, not constants", () =>
        {
            // Deliberately neither default, so a hardcoded 2 or 3 anywhere fails here.
            var burn = new BurnOnAttackCaptureEffect(5);
            var shield = new PermanentShieldCaptureEffect(7);
            Check(burn.Damage == 5 && shield.Amount == 7);
            Check(burn.Describe(new BattleRules()).Contains("5"));
            Check(shield.Describe(new BattleRules()).Contains("7"));
            var turns = CapturedPointBattle(One, new CompositeCapturePointEffect(burn, shield));
            GameState state = turns.State;
            var battle = new BattleSystem(turns);
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                Check(state.GetUnit(One, index).Shield == 7);
            UnitState victim = state.GetUnit(Two, 0);
            victim.SetPosition(2, 4);
            state.GetUnit(One, 0).SetPosition(2, 3);
            // A 3 HP warrior would simply die to a scorch of 5, and a clamped 0 proves
            // nothing about the number. Shielding it reads the exact amount instead.
            victim.GrantAuraShield(9);
            int hp = victim.HP;
            int damage = battle.GetAttackDamage(One, 0, Two, 0);
            Check(battle.TryAttack(One, 0, Two, 0));
            Check(victim.AttackBurnDamage == 5 && victim.Shield == 9 - damage);
            Check(turns.TryEndTurn(One));
            Check(victim.Shield == 9 - damage - 5 && victim.HP == hp);
            // And a third pair of numbers, to be sure nothing is pinned to 5 or 7 either.
            var other = CapturedPointBattle(Two, new PermanentShieldCaptureEffect(1));
            for (int index = 0; index < GameState.UnitsPerPlayer; index++)
                Check(other.State.GetUnit(Two, index).Shield == 1);
            Check(other.State.GetUnit(One, 0).Shield == 0);
        });
        Run("Capture progress survives cloning and the bot searches it legally", () =>
        {
            GameState state = FixedDeployment.Create(7, CaptureRules(3));
            Check(state.TrySetCapturePoint(3, 3));
            var turns = new TurnSystem(state);
            Check(turns.TryStartBattle(Two));
            state.GetUnit(Two, 0).SetPosition(3, 3);
            state.CaptureHolder = Two;
            state.CaptureHeldRounds = 1;
            state.CaptureOwner = null;
            // A clone must carry the hold, or a search would forget how far along it is.
            GameState copy = state.Clone();
            Check(copy.CaptureHolder == Two && copy.CaptureHeldRounds == 1);
            Check(copy.IsCaptureTile(3, 3) && !copy.CaptureOwner.HasValue);
            // Changing the copy leaves the real board alone.
            copy.CaptureHeldRounds = 2;
            Check(state.CaptureHeldRounds == 1);
            // The bot throws on its own illegal actions, so a clean search plus a clean
            // replay is the assertion. It may well walk off the point; that is its call.
            BotPlan plan = new DfsBot(2, 8000, 0).FindTurn(state);
            foreach (BotAction action in plan.Actions) Check(action.Apply(turns));
        });
    }

    // A battle where One already owns the point, with unit 3 standing on it, so buff
    // behaviour can be tested without replaying the hold every time.
    private static TurnSystem CapturedPointBattle(PlayerId owner, ICapturePointEffect effect)
    {
        GameState state = FixedDeployment.Create(7, CaptureRules(1, effect: effect));
        Check(state.TrySetCapturePoint(3, 3));
        var turns = new TurnSystem(state);
        Check(turns.TryStartBattle(owner));
        state.GetUnit(owner, 3).SetPosition(3, 3);
        // One round of holding, then back to the owner's turn with the point taken.
        Check(turns.TryEndTurn(owner));
        Check(turns.TryEndTurn(owner == One ? Two : One));
        Check(state.CaptureOwner == owner);
        return turns;
    }

    private static BattleRules CaptureRules(int rounds, bool keep = false,
        ICapturePointEffect effect = null) =>
        new BattleRules(ap: new ActionPointRules(startAP: 6, useSkillResource: false),
            capture: new CapturePointRules(rounds, keep, effect));
}
