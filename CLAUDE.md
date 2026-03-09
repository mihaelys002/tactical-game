# TacticalGame — Claude Context

## Project Overview
A turn-based tactical RPG inspired by **Battle Brothers**. Players command a mercenary company in isometric hex-grid combat.

- **Engine**: Godot 4.6 (C# / .NET)
- **Rendering**: Forward Plus, D3D12
- **Physics**: Jolt Physics
- **Grid**: Hex-based, axial coordinates
- **Perspective**: Isometric 3D

## Repository Structure
```
tactical-game/
├── CLAUDE.md
├── project.godot
├── src/
│   ├── Grid/        # Core model (pure C#, no Godot)
│   ├── AI/          # Decision-making (reads state, never mutates)
│   └── Prototype/   # Godot visualization + orchestration
└── docs/
```

## Docs Index
| File | Contents |
|---|---|
| `docs/turn-loop.md` | Plan → execute → replan loop |
| `docs/commands.md` | IBattleCommand, undo, damage calc |
| `docs/visualization.md` | UnitVisual, CommandVisual, orchestrator |
| `docs/state.md` | BattleState, HexCell, dead units, teams |
