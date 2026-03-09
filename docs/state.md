# Battle State

## BattleState

Single gateway for all mutations. Owns grid, unit list, loot list.

Key methods: `PlaceUnit`, `MoveUnit`, `RemoveUnit`, `ApplyDamage`.

`ApplyDamage` splits damage between armor and HP. No clamping, no death removal.
Commands call these methods (or do their own math for undo support).

## HexCell

Holds list of occupants (not single occupant). Multiple units can share a cell (living + corpses).

`IsWalkable` — true if no alive unit on the cell. Consumers check this, never inspect occupant list directly.

## Unit

`IsAlive` = `CurrentHP > 0`. Dead units stay in state with negative HP.
Never removed from BattleState on death. Enables clean undo.

## Teams

`BattleManager` owns team lists and `Dictionary<Unit, int>` for O(1) team lookup.
Enemy cache built per planning loop iteration: all alive units from other teams.
