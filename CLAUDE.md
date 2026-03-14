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
├── TacticalGame.csproj          # Godot project (Godot.NET.Sdk 4.6.1)
├── core/
│   ├── TacticalGame.Core.csproj # Pure C# class library (.NET 9.0)
│   ├── Grid/                    # Core model (pure C#, no Godot)
│   └── AI/                      # Decision-making (reads state, never mutates)
├── src/
│   └── Prototype/               # Godot visualization + orchestration
└── docs/
```

## Docs Index
| File | Contents |
|---|---|
| `docs/turn-loop.md` | Plan → execute → replan loop, AIAction |
| `docs/commands.md` | IBattleCommand, CompoundCommand, BattleEffect |
| `docs/combat.md` | Pipeline, skills, traits, HitPattern, CombatCalcs |
| `docs/state.md` | BattleState, HexCell, Unit, teams, mutations |
| `docs/visualization.md` | UnitVisual, CommandVisual, orchestrator |
