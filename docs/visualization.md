# Visualization

Model and visuals fully separated. Logic produces commands, viz consumes them.

```
BattleManager.StepTurn() → List<IBattleCommand>
  → BattleOrchestrator → CommandVisual → UnitVisual
```

## UnitVisual

Node2D per unit. Circle + HP/armor bars + equipment icons.
Exposes: `PlaySwing()`, `PlayHit()`, `PlayDeath()`, `AnimateMoveTo()` — all return Task.
Currently instant, replace with tweens later. No structural changes needed.

## CommandVisual

Abstract. Each subclass choreographs one cmd type.
- **MoveVisual** — animates unit to target
- **CompoundVisual** — swing, hit, death sequence

`CommandVisualFactory` maps `IBattleCommand` → `CommandVisual`.

## BattleOrchestrator

Iterates cmds, creates visuals, awaits `Play()`.
`SyncAll()` snaps visuals to current state (used after undo).

## GridVisualizer

Draws hex grid terrain. `HexToPixel()` for coord conversion.
