# Test Observations

Observations and potential design issues discovered while writing unit tests.

## 1. `MoveCommand.Undo` correctly checks walkability

Undo goes through `MoveUnit` which validates walkability. This is intentional — if the initial state was valid, the undo cell must be free (the unit came from it). If it's not free, the undo order is wrong, which is a caller bug that should throw. Tests verify both correct-order undo and wrong-order-throws.

## 2. `BattleManager.UndoLastTurn` doesn't undo fatigue recovery

`StepTurn` recovers 15 fatigue for all alive units at the start, but `UndoLastTurn` only reverses the commands — it doesn't re-add the recovered fatigue. After undo, units have less fatigue than they should. This needs a design decision: either track recovery as a command/effect, or store pre-recovery fatigue values for restoration.

## 3. `RegisterTeam` has no duplicate guard

Nothing prevents registering the same unit on two teams. `GetTeamIndex` would return the last-registered team, and the unit would appear in both `GetAllies` and `GetEnemies` results.

## 4. AI uses `Stats.MaxFatigue` instead of effective max fatigue

`AIBrain.ScoreSkills` checks `unit.Stats.CurrentFatigue + skill.FatigueCost > unit.Stats.MaxFatigue`, ignoring any equipment `MaxFatigue` bonuses. If `StatBonus.MaxFatigue` is ever used, the AI would incorrectly reject skills the unit could afford.

## 5. `EquipmentSlots.TotalBonus()` iterates on every call

`Unit.EffectiveAttack`, `EffectiveDefense`, and `EffectiveResolve` each call `Equipment.TotalBonus()` independently. Reading all three effective stats iterates the equipment dictionary three times. Not a correctness issue, but could matter in hot loops (AI scoring).

## 6. Dead units remain as cell occupants

When a unit dies (HP reaches 0), it stays in the cell's occupant list. `IsWalkable` handles this correctly (only counts alive occupants), but `HexCell.Occupants` grows unboundedly. `ResolveTargets` iterates dead occupants and filters them, which is correct but wasteful.

## 7. `CompoundCommand.Execute` has no rollback on mid-execution failure

If an effect's `Apply` throws mid-execution, previously applied effects in the same command won't be reversed. The command has `Undo()` but the caller would need to know to call it.
