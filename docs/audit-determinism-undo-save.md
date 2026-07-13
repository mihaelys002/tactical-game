# Audit: Determinism / Do-Undo / Save (2026-07-09)

Scope: core, ai, orchestration, messages. Read-only audit — **no code fixed**.
Tests added: `tests/DeterminismReversibilityTests.cs` (16 tests; 14 pass, 2 named
`KnownBug_*` fail on purpose and document the one real bug found).

## 1. Determinism — GOOD

- No unseeded randomness anywhere in core/ai/orchestration. `BattleSetup`
  uses `new Random(teamIndex * 1000)` (seeded). `MessageEngine` RNG is seeded
  and the engine is a pure observer — banter can never affect battle state.
- Multithreaded planning is deterministic: `Parallel.For` writes into indexed
  slots, execution is sequential in unit-list order, and the commander phase
  runs once under a lock iterating `battle.Units` in stable order.
- Tie-breaking everywhere is "first candidate wins" over lists whose order
  derives from insertion order (`_units`, equipment dict, `HexCoord.Neighbors()`
  fixed order, grid cells serialized as an ordered list). Deterministic today,
  including across save/load — but it *rests on* Dictionary enumeration ==
  insertion order. Any future `OrderBy`/`HashSet`/re-keying could silently
  break it; the new determinism tests are the tripwire.

Verified by tests: two identical runs produce identical command logs and
final state; threaded run == sequential run; for both the default AIBrain
planner and the full UtilityAIPlanner.

## 2. Do/Undo — MOSTLY GOOD, one real bug

Architecture is sound: every effect stores its applied delta, `CompoundCommand`
undoes effects in reverse, `BattleManager` pops whole turns, recovery commands
are recorded and undone too.

### BUG: overkill damage is not reversible
`core/Combat/BattleEffect.cs` — `DamageEffect.Apply` stores what `SplitDamage`
*intended* to deal, ignoring the actual clamped deltas returned by
`ChangeArmor`/`ChangeHP`:

```csharp
(AppliedArmorDamage, AppliedHpDamage) = CombatCalculations.SplitDamage(...);
battle.ChangeArmor(Target, -AppliedArmorDamage);  // return value ignored
battle.ChangeHP(Target, -AppliedHpDamage);        // return value ignored
```

When a killing blow overshoots remaining HP (victim at 7 HP hit for 15),
`Reverse` restores +15, reviving the corpse at 15 HP instead of 7. Armor is
safe (`SplitDamage` caps armor damage at current armor); only HP overkill
breaks. Undoing *all* turns can mask the bug — over-restoration clamps at
MaxHP, so units that started at full HP end up "correct" by accident.
Partial undo (undo just the killing turn) exposes it.

Every other effect (`Heal`/`Fatigue`/`Morale`) stores the *returned* actual
delta and is exactly reversible — `DamageEffect` is the one deviation from
the pattern. Fix direction (NOT applied per instructions): store the return
values of the two `Change*` calls, like the other effects do.

Failing repro tests:
- `KnownBug_Undo_OverkillDamage_RestoresExactHP` (granular)
- `KnownBug_Undo_KillingBlowTurn_RestoresPreTurnSnapshot` (integration)

### Latent: recovery command ToString crashes
`CompoundCommand.ToString()` (`core/Commands/CompoundCommand.cs:45`) dereferences
`Skill.Name`, but `CombatPipeline.ResolveRecovery` builds commands with
`null!` skill/weapon. Any code that stringifies a recovery command (debug
overlay, logging) throws NRE. Doesn't affect gameplay; worth knowing.

### Design gap: AI memory is outside the undo boundary
`UtilityAIPlanner` keeps per-unit strategies, persistent goals, `_assignedTurn`
and disobedience events outside `BattleState`. Undo rewinds the battle but not
that memory. In the tested scenarios replay-after-undo produced identical
turns (commander re-runs because the turn number changed; stale goals happened
to re-match), but this is not structurally guaranteed: a stale goal carries a
future `StartTurn`, and goal persistence/hysteresis (`MinTurns`, `SwitchMargin`)
sees a different history than a fresh timeline would. Decision needed:
either accept "AI mood survives undo" as a feature, or snapshot/restore
planner state per turn. Tripwire test: `Undo_ThenReplay_ProducesIdenticalTurns_UtilityPlanner`.

## 3. Save system — GOOD

- Existing `BattleManagerTests` save/load coverage is thorough: object identity
  across the graph (units in cells == units in commands), registry defs
  resolved to the same instances, undo works after load.
- New tests add the behavioral layer, all passing:
  - mid-battle snapshot: loaded state == original state
  - continuation: original session and loaded session play out identically
    to battle end (default planner AND UtilityAIPlanner, this scenario)
  - undo after load == undo without save/load (Applied* fields round-trip)
  - load then undo to turn 0 restores the initial snapshot
- Gap (same as above): `BattleSave` saves `BattleState` only. UtilityAIPlanner
  goals/strategies and MessageEngine battle memory start cold after load.
  Cold planner re-derived the same decisions in the tested scenario, but goal
  persistence across a save boundary is not guaranteed by construction.
  **Addressed after this audit**: `GameSave` (orchestration) now saves battle +
  planner memory in one document — see `docs/serialization.md` and
  `tests/GameSaveTests.cs`. MessageEngine battle memory remains cold-start
  (cosmetic only).
- The overkill bug serializes faithfully (wrong `AppliedHpDamage` is saved
  as-is), so save/load neither hides nor worsens it.

## Verdict

| Question | Answer |
|---|---|
| Determinism | Yes, holds — including threaded planning. Now guarded by tests. |
| Do/Undo any turn | Yes, except killing-blow overkill restores wrong HP (1 bug, 2 failing tests document it). |
| Tests for these | Added — `tests/DeterminismReversibilityTests.cs`. |
| Save system | Solid; loaded battles replay identically. Only AI planner memory lives outside the save. |
