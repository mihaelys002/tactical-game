# State

## BattleState

Mutation gateway. All state changes go thru instance methods.

Owns: grid, units, teams.

Mutations:
- `PlaceUnit`, `MoveUnit`, `RemoveUnit`
- `Equip`, `Unequip`
- `ChangeHP(unit, delta)` → actual delta
- `ChangeArmor`, `ChangeFatigue`, `ChangeMorale` — same pattern

Team tracking:
- `RegisterTeam(List<Unit>)` → team index
- `GetTeamIndex(unit)`, `GetAllies(unit)`, `GetEnemies(unit)`

## HexCell

Occupants list (living + corpses). 1 alive unit per cell max.
`IsWalkable` = no alive occupant. Only public check for occupancy.

## Unit

`IsAlive` = `CurrentHP > 0`. Dead units stay in state, never removed. Enables undo.
`Effective*` props = base stats + equipment `TotalBonus()`.
`Traits` = `List<ITrait>` for pipeline modification.
