# Visualization

Model and visuals are fully separated. Game logic produces commands. Visualization consumes them.

## Layers

```
BattleManager.StepTurn()
        |
        v
  List<IBattleCommand>
        |
        v
  BattleOrchestrator         -- decides when to play each command
        |
        v
  CommandVisual               -- choreographs one command's visuals
   /         \
MoveVisual   AttackVisual     -- each type owns its own sequence
   |              |
   v              v
  UnitVisual    UnitVisual    -- Node2D per unit, draws itself
```

## UnitVisual

Godot Node2D. One per unit. Draws circle + HP/armor bars at local origin.
Exposes visual actions: `PlaySwing()`, `PlayHit()`, `PlayDeath()`, `AnimateMoveTo()`.
All return `Task` — currently instant, replace with tweens/animations later.
Knows nothing about game logic. Just displays what it's told.

## CommandVisual

Abstract. Each subclass choreographs one command type.

**MoveVisual**: calls `unitVisual.AnimateMoveTo(target)`.

**AttackVisual**: calls `attacker.PlaySwing()`, then `target.PlayHit()`, then `target.PlayDeath()` if dead.
Owns the temporal dependency (swing before hit). UnitVisual doesn't know about other units.

## CommandVisualFactory

Single place that maps `IBattleCommand` -> `CommandVisual`. Only type discrimination in the system.

```csharp
cmd switch
{
    MoveCommand   => new MoveVisual(...),
    AttackCommand => new AttackVisual(...),
}
```

New command types: add a CommandVisual subclass and a case here.

## BattleOrchestrator

Iterates commands, creates CommandVisual for each, awaits `Play()`.
Also has `SyncAll()` — snaps all UnitVisuals to current state (used after undo).

## GridVisualizer

Terrain only. Draws hex grid. Spawns UnitVisuals as children.
`HexToPixel()` shared with orchestrator for coordinate conversion.

## Adding animations later

1. Replace `Task.CompletedTask` in UnitVisual with actual tweens
2. CommandVisual sequences already await — animations will just take time
3. No structural changes needed
