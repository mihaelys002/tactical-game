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

## VisualTheme / UnitLooks

Visual decisions live outside visual nodes:
- `VisualTheme.TeamColor(teamIndex)` — the TeamId→Color dictionary; nodes ask, never own palettes.
- `UnitLookDef` — flyweight look (body color, portrait glyph; real art later). Not core Unit data.
- `UnitLooks` — the Unit→look dictionary, assigned at scenario setup, read by visuals.

## MessageBubbleLayer

Speech bubbles for the message engine (see `docs/ai-banter.md`). Minimal deps:
`MessageLine`s, a `Func<Unit, Vector2>` screen position, and VisualTheme/UnitLooks.
Portrait + name + wrapped text above the speaker, tag-colored border, timed fade.
`Show(line)`, `Clear()`, toggle with `M` in the prototype.
