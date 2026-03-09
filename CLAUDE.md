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
└── docs/
    ├── architecture/   # Scene tree design, system boundaries, data flow
    ├── gameplay/       # Rules, mechanics, balance parameters
    ├── systems/        # Per-system technical notes (combat, units, grid, etc.)
    ├── ai/             # AI architecture and enemy behavior
    └── art/            # Asset pipeline, naming conventions, style guide
```

## Docs Index
| File | Contents |
|---|---|
| `docs/architecture/scene-structure.md` | Scene tree, autoloads |
| `docs/architecture/data-flow.md` | How data moves between systems |
| `docs/gameplay/combat-rules.md` | Combat mechanics and rules |
| `docs/gameplay/unit-stats.md` | Unit stats and progression |
| `docs/systems/hex-grid.md` | Hex grid implementation |
| `docs/systems/combat-system.md` | Combat system design |
| `docs/systems/turn-manager.md` | Turn order and AP management |
| `docs/ai/ai-overview.md` | AI architecture |
| `docs/ai/enemy-behaviors.md` | Enemy types and behaviors |
| `docs/art/asset-pipeline.md` | Art pipeline and conventions |
