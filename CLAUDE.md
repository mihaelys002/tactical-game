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
├── TacticalGame.csproj          # Godot project (Godot.NET.Sdk 4.6.1), compiles src/
├── core/                        # TacticalGame.Core — pure C# model (.NET 9.0, no Godot)
│   ├── Grid/                    # HexCoord, HexCell, HexGrid, TerrainType
│   ├── Units/                   # Unit, UnitStats, ITrait, StatBonus
│   ├── Items/                   # Equipment, EquipmentDef, EquipmentSlot
│   ├── Combat/                  # CombatPipeline, CombatCalculations, HitPattern, BattleEffect
│   ├── Commands/                # BattleCommands, MoveCommand, PrototypeCommand, CompoundCommand
│   ├── Skills/                  # SkillDef + concrete skills
│   ├── State/                   # BattleState (mutation gateway)
│   └── Serialization/           # BattleSave, DefRegistry, DefConverters
│                                # Namespaces match folders: TacticalGame.<Folder>; GlobalUsings.cs per project imports all core namespaces
├── ai/                          # TacticalGame.AI — decision-making (reads state, never mutates)
│   └── AI/Layers/               # Commander/Strategy/Goal/Director layers
├── messages/                    # TacticalGame.Messages — banter engine (pure observer of core+ai)
├── orchestration/               # TacticalGame.Orchestration — BattleManager turn loop
├── init/                        # TacticalGame.Init — scenario setup, headless console runner
├── src/Prototype/               # Godot visualization + input
├── tests/                       # xunit — Core + AI + Orchestration
├── tests-visual/                # xunit — visualization (command batching)
└── docs/
```
Dependency chain: `core ← ai ← orchestration ← init ← src (Godot)`; `messages` refs core+ai (nothing refs it back except init/tests). Tests reference core+ai+orchestration+messages.

## Docs Index
| File | Contents |
|---|---|
| `docs/turn-loop.md` | Plan → execute → replan loop, AIAction |
| `docs/ai-layers.md` | 5-layer AI: UtilityAIPlanner (resource-driven, dual-score, scripted fights) + simple AIPlanner chain |
| `docs/ai-layers-design-intent.md` | Target AI design (verbatim), resource-file weights, dual-score, multithreading must |
| `docs/ai-debugging.md` | ScoreBreakdown, weight tuning workflow, debug overlay tiers |
| `docs/ai-banter.md` | MessageEngine: facts, pools, rules JSON, campaign memory, conversations |
| `docs/commands.md` | IBattleCommand, CompoundCommand, BattleEffect |
| `docs/combat.md` | Pipeline, skills, traits, HitPattern, CombatCalcs |
| `docs/state.md` | BattleState, HexCell, Unit, teams, mutations |
| `docs/serialization.md` | BattleSave, Newtonsoft rules, no attributes needed |
| `docs/visualization.md` | UnitVisual, CommandVisual, orchestrator |
