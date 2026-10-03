# Prototype battle cycle

Open `Assets/Scenes/SampleScene.unity` and enter Play Mode. The Board object has
GameController attached. It creates the four opponent views and frames the board; Player One units appear as they are placed.

1. Press **Start Game**, select each Player One unit from the list, and click an empty tile in the bottom two rows. Re-select a listed unit to reposition it. After placing all four, press **Start Battle**; the first player is chosen randomly.
2. Click a current-player unit. Yellow marks selection; green marks legal moves;
   red marks enemies in attack range. Blue and red distinguish the players.
3. Click an empty highlighted tile to move or an enemy to attack. Every action
   spends the shared AP shown in the HUD, so a turn can combine several units.
4. Press **End Turn** to give control to the other player, including when passing.
   Leftover AP is not discarded; it carries into your next turn.
5. Destroy every enemy unit to win. There is no leader: each side loses only when
   all four of its units are dead. **Restart** returns to a fresh pre-deployment
   screen with a newly drafted roster, Player One unplaced and no turn history.

Each side is drafted at random: one unit per position from 근접/수호/원거리/제어.
This is a local player-versus-bot prototype on one screen. Player One places its
drafted units by hand; the bot drafts and places its own.
The HUD uses IMGUI and the project's existing legacy mouse/touch input.

## Rules and architecture

- GameState owns the roster, occupancy queries and deep copies for simulation.
- UnitDraft builds a roster (one unit per position) and scatters a side across its
  home rows; FixedDeployment still builds the symmetric lineup the checks use.
  A deployment is ready when all eight units sit inside their own two home rows.
- BattleSystem.TryMove/TryAttack are the gameplay entry points. They validate
  targets before spending actions and complete damage/victory before publishing
  the game-state change. Failed actions leave state and action resources intact.
- TurnSystem handles turn start/end, the per-player AP pool and each unit's
  once-per-turn combat lock. Its low-level
  TryConsume methods are not complete move/attack commands.
- Unit is abstract; one component per unit kind supplies the view, and TestUnit
  remains available for isolated tests. HP changes hide dead views, while roster
  entries remain stable and dead units no longer occupy tiles.
- GameController creates/destroys views and resets the complete match.
- PrototypeUI handles selection, legal-action highlights, input and the HUD.

For AI, use `new TurnSystem(live.State.Clone())` and a BattleSystem for that copy.
Unit data and scene event subscribers are isolated from the original match.

## Automated checks

Run from the project root with .NET 8 SDK:

```
dotnet run --project Tests/TurnSystem/TurnSystem.Tests.csproj
```

No external test packages are required. All game, board and unit scripts compile
against the installed Unity modules. If Unity is installed elsewhere, supply
`-p:UnityEditorPath="path/to/Editor"`.

The 125 scenarios cover deployment, turns, AP budgets, move limits, move patterns and
per-turn move budgets, bot difficulty presets, the v0.4 units with their passives,
fire zones, traps, player-placed obstacles, the capture point and its buff seam, the random draft, targeting, HP, death, notifications, AI copies
and a full match decided by wiping out a side. They do not simulate actual mouse/touch events or Unity rendering.
Play Mode verification: check eight visible units, select/move/attack, pass turns,
confirm a side only loses after its last unit dies, then restart twice and check
exactly eight units and a freshly drafted roster each time.

## v0.1 sample units and skills

All four gameplay components derive from abstract Unit and expose an immutable
UnitDefinition. UnitState carries that definition independently of roster slot;
BattleSystem executes the pure-data rules for both live play and AI copies.
FixedDeployment uses these definitions and GameController creates matching types.

| Component | HP | Basic attack | Skill |
| --- | --- | --- | --- |
| WarriorUnit | 3 | Range 1, damage 1 | Strong Strike: range 1, damage 2 |
| GuardianUnit | 4 | Range 1, damage 1 | Shield: adjacent ally, absorbs damage |
| RangerUnit (Archer) | 2 | Range 3, or 4 with Aim; damage 1 | Snipe: range 4, damage 2, cost 2; ignores intermediate units |
| ControllerUnit | 2 | Range 1, damage 1 | Knockback: adjacent enemy pushed one tile directly away |

Select a unit, press its skill button, then click a cyan highlighted target.
Shield targets an adjacent ally (not the caster); cancel skill mode to select a
different unit. A '+' on the compact label denotes an active shield. The HUD shows
remaining shared cost and the selected unit's shield amount.

Provisional choices for details not fixed by the document:
- `Assets/Data/BattleRules.asset`: initialCost 6, skillCost 2, shieldAmount 1.
  Changes apply on Restart. Snipe always costs 2 per the ranged spec; skillCost
  configures the other skills. Each side has its own shared pool; no regeneration.
- All four skills consume the combat action. The caster cannot move afterward
  that turn. Basic attacks cost AP (2 by default) and also consume it.
- Guardian uses the shield alternative, without an additional passive damage aura.
  Shields do not stack and expire at the end of the target side's next turn if
  not consumed. Absorbed damage does not reduce HP.
- Controller uses the knockback alternative, without an additional disable effect.
  Pushes deal no damage. Occupied/out-of-board destinations reject the entire
  command without spending cost or combat action.
- There is no leader. A side loses only when every unit it owns is dead, which
  `GameState.IsWipedOut` reports and `TurnSystem.CheckElimination` acts on after
  any damage, including damage dealt by fire at a turn start.

Tests cover each skill, validation, costs, shield expiration, clones, blocked
pushes, lethal skills and reset. Actual Unity rendering/input is not automated.

## Archer ranged spec v0.1

The ranged-specific document supersedes the original sample's range-2 numbers.
RangerUnit remains the component name for compatibility; its displayed name is
Archer. Aim adds 1 to basic range during its owner's active turn if that archer
has not moved voluntarily this turn. UnitState.HasMovedThisTurn records this per unit and
resets at that side's turn start; another ally moving does not disable Aim. Failed movement
and forced displacement do not count as using the movement action. Clones retain
this turn data independently. The HUD and target highlights use effective range.
Snipe has fixed range 4, damage 2 and cost 2 regardless of Aim or the default skill
cost setting. Both basic attacks and Snipe ignore intervening units and use the
same diagonal-inclusive distance calculation. Combat still prevents subsequent
voluntary movement by the acting unit.

DeploymentSystem validates Player One's placements and repositioning before combat. It rejects occupied tiles, coordinates outside the bottom two rows, and commands before Start Game or after battle begins. Restart clears Player One's roster. Player One is human-controlled; Player Two now acts automatically using the DFS bot.


## Player Two DFS bot

DfsBot searches complete turns with DFS Minimax, alpha-beta pruning and iterative
deepening. Within a turn it generates early End Turn, movement, basic attacks,
and all valid skills, including both move/combat orders and different acting
units. Every candidate uses the existing rule APIs on a cloned GameState.
Evaluation prioritizes victory, surviving units, total HP, shields, board control,
resources, attack pressure and approach distance. Every unit now counts the same,
because every unit is a win condition. It is an initial heuristic, not a balanced
or optimal opponent.

Under the v0.3 AP rules a "complete turn" is a whole AP sequence, not one action,
so the bot was adjusted with the rules:

- `CompleteTurns` recurses until AP runs out, letting the bot spend a turn across
  several units. It always yields the legal early End Turn first, so a pass stays
  available and `TrimCandidates` keeps one even when it prunes to 48 candidates.
- `LegalActions` offers moves over the full `-2..2` window, so two-tile moves are
  real candidates, and only for units `TurnSystem.CanMove` still accepts. Because
  the rules already lock a unit after it fights, `attack -> move` never enters the
  tree.
- `StateKey` includes each side's AP plus every unit's `HasMovedThisTurn` and
  `HasCombatActedThisTurn`, so transposed orderings of the same combo collapse to
  one node instead of exploding the branching factor.
- `Evaluate` scores leftover AP at `min(currentAP, maxAP - recoveryAP) * 6`, which
  values only the AP that actually survives the turn-start clamp. That is what
  makes banking AP for a 6 AP turn attractive without rewarding AP that would be
  discarded anyway.
- Generation shares a per-turn budget across first actions (`branchBudget`) so a
  wide AP tree still returns candidates inside `botNodeLimit`/`botTimeLimitMs`.

GameController Inspector defaults:
- botDepth: 2 completed turns (bot action plus opponent response), configurable 1..4.
- botNodeLimit: 20000 simulated action applications, including candidate generation.
- botTimeLimitMs: 300 ms cooperative search budget.
- botActionDelay: 0.3 seconds between visible actions.

Search runs in Task.Run without Unity objects. Main-thread Update applies the
plan through the same rule APIs. Human board input and End Turn are disabled
throughout Player Two's turn; Restart remains available. Restart, disabling or
destroying the controller cancels and discards outstanding work. A match/turn
identity check prevents stale results from executing in a new match.

A budget interruption uses the last fully completed depth; if candidate generation
itself is interrupted, it uses the best fully generated complete turn so far,
with End Turn as the guaranteed legal fallback. Thus depth 2 is a target, not a
guarantee under the limits. Cancellation throws instead of returning a stale plan.
Tests include tactical wins, a depth-two defensive reply, two-unit combo kills
within a 4 AP budget, a two-tile approach before a lethal attack, partial turns,
budget
fallback, cancellation, live-state isolation and a bounded bot-vs-bot smoke run.
Actual Unity input, frame timing and visual action playback still need Play Mode
verification; the .NET checks compile the integration but do not run Unity scenes.

## Editable unit assets (current configuration)

SampleScene's GameController now references these ScriptableObject assets:
`Assets/Data/Units/Warrior.asset`, `Guardian.asset`, `Archer.asset`, `Controller.asset`.
Select one in Project and edit its Inspector. Create alternatives via
**Create > Mini Chess > Unit Definition**, then assign the four roster slots in
GameController's Unit Definitions array.

Fields: display name, role, description, max HP, attack damage/range, Aim range
bonus, skill type/name, cost, range and power. Skill Power means damage for
Strong Strike/Snipe, absorption for Shield, and destination displacement in tiles
for Knockback. For multi-tile knockback only the destination is checked, matching
the original destination-only rule. Defaults preserve the existing v0.1 numbers.
The old fixed Snipe cost/range notes above describe default balance, not constraints
on editable assets. GameController's skillCost/shieldAmount are fallback settings
when an asset is unassigned; assigned asset values take precedence.

Changes apply on **Restart** or the next Play session. Both sides receive the same
immutable runtime definitions. Running matches and AI worker snapshots never read
mutable ScriptableObjects, so editing an asset cannot change a search midway.

During your turn select a unit and press **Details** next to its skill button.
The scrollable panel shows your custom description plus stat/skill numbers derived
from the same runtime definition used by combat. Close it to resume board clicks.
The compact labels above units retain their previous size.

The four added checks verify custom damage/range/cost, passive bonuses, shield and
push values, independent lineup snapshots, and descriptions matching actual values.
Actual ScriptableObject importing and the Details panel require Unity Play Mode
verification; the headless .NET suite validates runtime data and compiles the UI.

## AP turn rule change spec v0.3

The old "one move + one combat per team per turn" limit is gone. Each player owns
a shared Action Point pool and spends it freely across their four units, so a turn
can be a small combo instead of a single action.

| Setting | Test value | Meaning |
| --- | --- | --- |
| Max AP | 6 | Ceiling for the pool |
| Start AP | 4 | Pool on each side's first own turn, with no extra recovery |
| Turn Recovery | +4 | Added at the start of every later own turn |
| Carry Over | allowed | Unspent AP survives End Turn, then is clamped to Max AP |

Turn start is `currentAP = min(maxAP, currentAP + recoveryAP)`.

| Action | AP |
| --- | --- |
| Move 1 tile | 1 |
| Move 2 tiles | 2 |
| Basic attack | 2 |
| Skill | 3 by default, per-unit override for stronger skills |

Per-unit limits, independent of the shared pool:

- A unit may use one combat action (basic attack or skill) per turn.
- After fighting, that unit cannot move voluntarily again that turn, so
  `move -> attack` is legal and `attack -> move` is not.
- Forced displacement such as Knockback ignores both limits: it is not the
  target's own action, costs the target no AP and does not set its moved flag.
- One basic move action covers at most `maxMoveDistance` tiles (2 by default);
  longer single moves require a movement skill. A unit may still take several
  separate move actions while AP lasts, so that setting alone does not cap a
  turn's travel. The optional `maxMoveTilesPerTurn` does; see Configuring the rules.

Movement details: diagonals count as one tile, the destination must be empty, and
a two-tile basic move needs at least one empty intermediate tile, so units block
paths. `BattleSystem.GetMoveDistance` returns the charged step count, and
`TryMove` validates the path before `TurnSystem.TryConsumeMove` spends anything.

AP and the existing match-wide skill resource stay separate. With
`useSkillResource` on, a skill requires both and spends neither unless both are
available; turning it off lets AP alone price skills. End Turn is always legal,
including with AP left and with no legal action available.

## Configuring the rules

Everything tunable lives in ScriptableObjects under `Assets/Data`, and SampleScene's
GameController points at them. Every field falls back to the same value in code when
its asset is left empty, so an unwired scene still plays exactly as before.

| Asset | Menu | Holds |
| --- | --- | --- |
| `BattleRules.asset` | **Mini Chess > Battle Rules** | initialCost, skillCost, shieldAmount, and references to the AP rules and the capture-point rules |
| `ActionPointRules.asset` | **Mini Chess > AP Rules** | maxAP, startAP, recoveryAP, moveAPPerTile, maxMoveDistance, maxMoveTilesPerTurn, movePattern, attackAP, defaultSkillAP, useSkillResource |
| `CapturePointRules.asset` | **Mini Chess > Capture Point Rules** | roundsToCapture, keepAfterLeaving, effect (the buff asset, empty for now) |
| `Bot/Easy.asset`, `Normal`, `Hard` | **Mini Chess > Bot Settings** | difficultyName, searchDepth, nodeLimit, timeLimitMs, actionDelay |
| `Units/*.asset` | **Mini Chess > Unit Definition** | per-unit stats plus **Skill AP Cost** (0 uses defaultSkillAP) |

GameController's **Battle Rules** field takes one BattleRulesAsset, so costs and AP
settings travel together. Swapping that one reference re-tunes the whole match.

Nothing reads these assets mid-match. Values are copied into immutable runtime rules
on **Restart** or at the start of a Play session, so editing an asset cannot disturb
a bot search that is already running on a worker thread.

### The two movement limits are different knobs

`maxMoveDistance` caps a single move action. It does **not** cap how far a unit
travels in a turn: because separate move actions are legal and cost the same AP per
tile, two 1-tile moves match one 2-tile move exactly. Lowering it alone only changes
how many clicks a move takes and how wide the bot's candidate scan is.

`maxMoveTilesPerTurn` is the limit that actually binds. It is the total basic-move
tiles one unit may cover in a turn, tracked on `UnitState.MoveTilesThisTurn` and
reset at that side's turn start. `0` disables it, which is the shipped default and
matches the v0.3 document's reading that only AP bounds a turn's travel. Set it to
2 to make each unit's movement behave like a per-turn allowance instead.

Both limits feed `TurnSystem.GetRemainingMoveDistance`, which `BattleSystem` uses for
pathfinding and highlights and `TurnSystem` uses when spending AP, so the green tiles,
the click, and the bot's generated actions can never disagree.

### Move pattern

`movePattern` decides what shape a single basic move may take:

| Value | A move is | A diagonal tile |
| --- | --- | --- |
| `EightWay` | up to `maxMoveDistance` steps in any of 8 directions, corners allowed | 1 step away |
| `FourWay` | one straight run along a row or column | unreachable in one move |

The shipped `ActionPointRules.asset` uses `FourWay`, so a unit only ever slides
상하좌우 and never ends a move on a diagonal tile. The code default stays
`EightWay`, which is what the rule checks assume unless a scenario asks otherwise.

`FourWay` bans the *shape*, not just the diagonal step. An earlier version banned
only the step, which left a diagonal neighbour reachable as a two-step L for two
AP — and since a unit is drawn at its destination rather than walked there, that
looked exactly like a one-tile diagonal move on screen. A straight-only run has no
such loophole. It also turns no corners, so a blocker ends the run instead of being
walked around; `EightWay` still detours, at the cost of the extra steps.

The pattern governs walking, and Charge follows it too, so a Breaker cannot dash
diagonally while diagonals are banned. Everything that measures a *range* — basic
attacks, every skill range, Shadow Leap, Knockback pushes — keeps the
diagonal-inclusive distance the v0.3 document specifies, so turning diagonals off
never silently shrinks a weapon. `ActionPointRules.MoveSteps` returns the tiles a
move would cover, or `-1` when the pattern rules that shape out; `BattleSystem`
resolves the run from it, so highlights, clicks and bot candidates stay in
agreement.

### Bot difficulty

`botDifficulties` on GameController is an array of BotSettingsAssets and
`botDifficultyIndex` picks the starting entry. When more than one is assigned, the
HUD shows a **Bot: \<name\>** button that cycles presets during play; the change
applies from the bot's next search, so it is safe mid-match.

| Preset | Depth | Nodes | Time | Measured cost per bot turn |
| --- | --- | --- | --- | --- |
| Easy | 1 | 4000 | 120 ms | ~9 ms |
| Normal | 2 | 20000 | 300 ms | ~135 ms |
| Hard | 3 | 200000 | 1200 ms | ~1200 ms |

Those costs come from an uncapped headless sweep, so in the editor each preset is
additionally clamped by its own `timeLimitMs`. Raising depth past 3 mostly buys
latency: at 6x6 the node budget, not the depth setting, is what usually stops the
search first.

Difficulty presets do not currently change who wins. In a 12-game headless sweep
with the shipped unit assets, the side that moved first won every game at every
preset, because the opening lets a leader be reached before the defender acts.
Board size, opening distance or leader HP need adjusting before win rate is a
usable difficulty signal; search effort is measurable today, outcome is not.

## Additional unit spec v0.4

Five units join the roster, and the leader rule is gone: a side loses only when
every unit it owns is dead.

| Unit | Position | HP / ATK / RNG | Passive | Skill (AP) |
| --- | --- | --- | --- | --- |
| 암살자 Assassin | 근접 | 5 / 2 / 1 | 고립 사냥: +1 damage when the target has no ally beside it | 그림자 도약 (3): teleport up to 3 tiles to an empty tile, ignoring the path |
| 화염술사 Pyromancer | 원거리 | 5 / 2 / 2 | 잔불: +1 damage against an enemy on this caster's own fire | 화염 지대 (3): fire on a tile and its four neighbours, lasting two own turns |
| 워프술사 Warper | 제어 | 5 / 2 / 2 | 공간 잔향: allies it repositions shrug 1 off their next hit | 위치 교환 (3): swap two allies within 3 |
| 돌진전사 Breaker | 근접 | 7 / 2 / 1 | 충격: +1 damage after covering 2+ tiles in one move | 돌진 (3): straight dash up to 3 tiles, blocked by any unit |
| 트래퍼 Trapper | 제어 | 5 / 2 / 2 | 사냥감 포착: +1 damage against an enemy its trap caught | 덫 설치 (2): arm an empty tile within 2 |

### Targeting

`UnitDefinition.Targeting` says what a skill needs, and both the HUD and the bot
read it: an enemy unit, an ally unit, an empty tile, any tile, or two allies.
`BattleSystem.TrySkillAt(player, index, x, y, x2, y2)` is the one entry point;
the older `TrySkill(..., targetPlayer, targetIndex)` now resolves the unit's tile
and calls it. Swap is the only skill that uses the second tile.

Shadow Leap and Charge are movement skills: they spend AP but not the combat
action, so the unit may still attack afterwards, and they are refused once it has
already fought. They also bypass the per-turn basic-move budget, which stays
reserved for ordinary movement.

### Capture point

One tile, designated by the player during setup with **점령지 지정**; clicking the
same tile again clears it, and choosing another moves it rather than adding a second.
A wall and the capture tile cannot share a square, refused in either order. Like
obstacles it is frozen once the battle starts, and **편성 다시** keeps it.

Holding is counted in the holder's **own turn starts**: step on during your turn and
you have held it for one round when your next turn comes round. `roundsToCapture` of
1 therefore means "still standing there after the opponent has replied". Occupancy is
re-read at every turn start, so the other side walking on resets the count to zero
immediately, and an empty tile clears the holder. With `keepAfterLeaving` off — the
default — losing the tile gives a completed capture back up; with it on, a capture is
permanent once earned. `TurnSystem.AdvanceCapturePoint` runs last in `BeginTurn`, after
AP recovery and burn damage, so a buff sees the AP the turn actually starts with and
acts on units that have already taken their damage.

The HUD shows **점령 청군 1/2라운드** while a hold is building and **점령: 청군** once
it lands. The tile is tinted teal while unclaimed and in the owner's colour after,
with a 점 marker.

#### The buffs

Both apply to **every unit on the owning side** and both ship on, toggled and tuned
from `CapturePointRules.asset`:

| Buff | Field | Default | What it does |
| --- | --- | --- | --- |
| 화상 | `burnOnAttack`, `burnDamage` | on, 2 | A **basic attack** leaves a scorch that lands at the start of the victim's next turn |
| 보호막 | `permanentShield`, `shieldAmount` | on, 3 | Every living unit carries a standing shield, topped back up each owner turn start |

Burn details: only basic attacks apply it, never skills. Two hits before the tick
stack, so the victim pays for both. The effect only writes the pending damage onto the
victim and `TurnSystem.ApplyAttackBurn` resolves it at that side's turn start — which
means giving the point up does **not** cancel a scorch already inflicted, and nothing
has to run during turn resolution. The damage is ordinary damage: shields and Spatial
Echo charges soak it.

Shield details: `GrantAuraShield` raises the shield to at least the configured amount
and gives it no expiry, so it does not tick away on the Guardian's timer. Spending it
in a fight is not permanent — it is topped back up at the owner's next turn start.
Losing the point calls `RemoveAuraShield`, which subtracts the same amount. One rough
edge worth knowing: a unit that also carries a larger Guardian shield keeps that
shield's own finite expiry, and losing the point subtracts from the combined total
rather than separating the two. Untangling them would need a second shield field, and
the prototype does not need that yet.

#### The buff seam

`ICapturePointEffect` has five hooks: `Describe` for the HUD line, `OnCaptured`,
`OnLost`, `OnOwnerTurnStart` for a recurring effect, and `OnBasicAttack`, which
`BattleSystem.TryAttack` calls only while that side owns the point.
`NoCapturePointEffect.Instance` is the default, so the rules never null-check, and
`CapturePointRules.Effect` is never null. `CompositeCapturePointEffect` runs several
as one, which is how the two above are combined.

To add another: subclass `CapturePointEffectAsset` (a ScriptableObject), add
`[CreateAssetMenu]`, and list the asset under **customEffects**. Two constraints are
load-bearing, because these hooks run inside the bot's search on a background thread
against cloned states:

- **Touch nothing but the GameState handed in.** No Unity API, no scene objects, no
  logging, no statics. A Unity call off the main thread throws.
- **Be deterministic.** The same state and owner must give the same result, or the
  bot's search and the real match will disagree about what happened.

`SpyCaptureEffect` in the scenarios counts calls, so the hooks are known to fire in
the right order independently of what any real buff does.

#### What the bot makes of it

Hold progress and pending scorch are both in the transposition key: the same board one
round deeper into a hold, or with 2 burn owed, is a different node. The capture tile
itself is fixed for the match, so it stays out of the key.

`Evaluate` scores owning the point at **70**, roughly half a body, plus **16 per round**
of a hold in progress and **24** for simply standing on a contested point — the last
one because without it the search never takes the first step that the rounds pay for.
70 is deliberately modest rather than generous: what the buffs actually do already
shows up in the existing terms — the standing shield in `unit.Shield * 12`, the scorch
in a new `-22` per point of pending burn — so a large number here would count the same
advantage twice.

Measured before and after adding these terms, 40 matches with the point at the centre
of a 7×7 board:

| | Point stood on | Captured | Turn starts on the tile |
| --- | --- | --- | --- |
| No capture term | 5/40 | 1/40 | 16 of 1,614 |


### Obstacles

The player draws the walls before the match. In deployment, **장애물 놓기** switches
clicks from placing units to laying walls: any free tile on the whole board takes
one, clicking it again lifts it, and **장애물 비우기** clears the lot. A wall tile is
tinted dark grey and carries a 벽 marker. **편성 다시** keeps the layout, so a reroll
does not cost the drawing; only a wall the newly placed bot lands on is dropped.

They live in GameState as packed tiles and may only be edited during deployment.
That restriction is what lets `Clone` share the list rather than copy it: a bot
search cannot change the layout, so every node can read the same one. This also
keeps them out of the transposition key, since all nodes in one search agree.

`GameState.IsBlocked(x, y)` is the single question movement asks — a living unit or
a wall both answer yes — and every occupancy check in the movement code routes
through it, so a wall blocks exactly what a unit blocks, and one place governs both.

What a wall stops:

- Standing on it: no basic move, dash, leap, knockback, deployment or random draft
  may put a unit there, and a formation with a unit on one is not ready.
- Walking through it: a `FourWay` run ends at it, and an `EightWay` move must detour
  around it and may run out of steps doing so.
- Leaping over it: Shadow Leap clears *units* but not walls. Its destination must be
  reachable by some chain of single steps that avoids every wall, within the skill's
  range — units along the way are ignored, walls are not. With no wall placed the
  check short-circuits, so the skill behaves exactly as before.
- Holding a trap or fire: neither may be centred on a wall, and the fire zone's plus
  shape is clipped around one.

What a wall does **not** stop: attacks and ranged skills. There is no line-of-sight
rule, so a ranger still shoots over a wall. Blocking shots would change the feel of
every ranged unit, so it is left out until the prototype asks for it.

### Fire zones and traps

Both live in GameState as pure data (`Areas`, `Traps`), are deep-copied by
`Clone`, and are part of the bot's transposition key, so two positions that differ
only in board effects stay distinct nodes.

- Fire burns an enemy that ends a move on it and again at that side's turn start.
  One field cannot burn the same unit twice in a turn (`AreaEffect.TryBurn`).
  Allies of the caster are never burned, and a field expires at the caster's
  second own turn start. Casting onto an occupied tile burns the occupant at once.
- A trap springs on the first enemy that **enters** its tile, whether the unit stops
  there or merely crosses it: damage, removal, and a mark that lets its owner hit
  that unit harder until the owner's next turn ends. Each side keeps at most two;
  arming a third discards the oldest. Teleports and forced movement (Shadow Leap,
  Knockback, Swap) do not spring traps. A charge is a walk, so its whole lane
  springs. A unit killed part-way stops on the trap that killed it. A sprung trap
  also pins its victim: `MovementStoppedThisTurn` bars further walking and movement
  skills for that turn, though the unit may still fight from where it was caught.

Traps are hidden information. Only the side that laid one can see it: the HUD draws
the player's own traps and nothing for the bot's, and the bot searches
`State.CloneAsSeenBy(side)`, a copy with the opponent's traps removed. Without that
the feature was dead on arrival — a full-information sweep laid 32 traps across 40
matches and sprang none of them, because a depth-2 search simply routed around every
one.

Because the bot plans on incomplete information, its plan can stop being valid
mid-execution: a trap cuts a move short and the attack that was meant to follow no
longer reaches. `GameController` treats that as expected and re-searches from the
revealed position instead of forfeiting the turn, up to `MaxBotReplans` times per
turn. Each replan follows a sprung trap and a side holds at most two, so the budget
is slack rather than load-bearing.

One asymmetry remains: traps the bot lays *during* its own search stay visible
inside that search, so it models the player dodging them and undervalues laying one.
Fixing that properly means a per-side information set in the search, which is more
machinery than the prototype needs.

Movement therefore resolves tile by tile rather than as one hop. `ResolveMove`
returns the route as well as its length: a straight run for `FourWay`, and for
`EightWay` the shortest path the breadth-first search found, traced back through
the search marks. Ties break in scan order, so a given request always walks the
same tiles. `GetMoveDistance` is the same call with the route discarded, which is
what `CanMove`, the highlights and the bot use.

Prototype simplifications, to revisit once the feel is right:
- Fire still reacts only to the tile a unit ends on, so a unit may walk through a
  field untouched while a trap on the same route goes off. Extending the crossing
  rule to fire is a one-line change in `WalkRoute`.
- A sprung trap does not stop the mover; it pays the damage and travels its full
  distance.
- Fire does not block movement, and allies take no fire damage, per the document's
  MVP note.

### Visuals

Prototype-grade, meant only to be legible: a fire tile is tinted orange, a trap tile
purple and a wall dark grey whenever no action highlight has claimed the tile, and
each carries a small "화" / "덫" / "벽" marker drawn over the board so it stays
visible under a green or red highlight. A wall wins that tint over fire and traps,
since it is the one thing that cannot change mid-match. While 장애물 놓기 is on, every
tile a wall could go on is tinted slate so the whole legal area is visible at once.
Unit shape now reads the position instead of leader status
— cube 근접, cylinder 수호, capsule 원거리, sphere 제어.

### Random draft

`UnitDraft.Draft` picks one candidate per position from the pool, and
`UnitDraft.PlaceRandomly` scatters a side over distinct free tiles in its two home
rows. GameController drafts both sides on Restart, places the bot itself, and
leaves Player One to place by hand; **편성 다시** redraws before placement. The
pool comes from GameController's **Unit Pool** assets, or the built-in nine units
when that list is empty. 수호 currently has a single candidate, so that slot is
fixed until another guard unit exists.

### Measured behaviour

A 30-match headless bot-vs-bot sweep with random drafts: every match ran without a
single illegal generated action, 24 of 30 finished inside 40 plies, fire zones
appeared in 23 matches and traps in 5. Draft counts over 480 slots matched the
pool shape (Guardian 60 as the only guard; Ranger 30 / Pyromancer 30 splitting
원거리).

A later 40-match sweep under `FourWay` played 683 basic moves, none of which ended
on a diagonal tile, with zero illegal actions. It sprang none of the 32 traps laid:
at depth 2 the bot saw a trap's damage and routed around it, so a full-information
bot sweep never exercised the crossing rule at all. That measurement is what made
traps hidden information. Driving the same matches with random legal moves does
exercise it: over ~14,500 moves per pattern, 131 traps (`FourWay`) and 195
(`EightWay`) sprang on a tile merely crossed, 19 units died on one mid-route, and
HP lost always matched the number of traps consumed.

A walled-board sweep (4–9 obstacles, 60 matches, `FourWay`) generated zero illegal
actions and left no unit resting inside a wall, with 1,388 wall tiles correctly
refused as destinations. It finished 24 of 60 matches against 29 of 40 on an open
board; walls slow the approach, and the passive evaluation noted below compounds it.
A same-seed control across 0/3/6/9 walls was started to separate the two causes but
did not finish, so the split is unmeasured.

The elimination rule also removed the first-move lock the old leader rule had:
matches now run ~20 plies instead of ~5, and over 40 random matches the first
mover won 22, the second 12, with 6 unfinished — no longer a guaranteed win.
