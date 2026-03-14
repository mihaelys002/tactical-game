# Commands

## IBattleCommand

```
Unit, Execute(battle) → bool, Undo(battle)
```

Execute returns false if cmd cant run (blocked move, no essential effects).
Undo reverses in exact order using stored deltas.

## MoveCommand

`Unit, From, To`. Execute checks `cell.IsWalkable`, returns false if blocked.

## CompoundCommand (sealed)

Wraps `List<BattleEffect>`. Has `Unit, Weapon, Skill, TargetHex`.
Execute only runs if at least 1 essential effect exists.
Undo reverses effects in reverse order.

## BattleEffect

Base class. `Target, IsEssential, Apply(battle), Reverse(battle)`.
Skills set `IsEssential` on effects they create — its not intrinsic to effect type.

Types:
- **DamageEffect** — `Source, Amount(mutable), AppliedArmorDamage, AppliedHpDamage`. Uses `SplitDamage` then `ChangeArmor/ChangeHP`
- **HealEffect** — `Amount, AppliedHeal`. Uses `ChangeHP`
- **FatigueEffect** — `Amount, AppliedFatigue`. Uses `ChangeFatigue`
- **MoraleEffect** — `Delta, AppliedDelta`. Uses `ChangeMorale`

## Undo

`BattleManager.UndoLastTurn()` pops last turn, undoes all cmds in reverse.
Each effect stores exact applied delta for clean reversal.
