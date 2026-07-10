# AI Layer Architecture

Two planners exist:
- **`UtilityAIPlanner`** — the full resource-driven 5-layer system (this is the target design, see below and `docs/ai-layers-design-intent.md`)
- **`AIPlanner`** — the simple context-chain planner with `Default*Layer` classes (kept for tests/simple scenarios; documented in the lower half of this file)

## UtilityAIPlanner (resource-driven)

```
Director   — ScriptedDirector triggers, hard override, checked first
Commander  — once per turn (locked phase): assigns a StrategyDef to every unit
             dual score = commander expectation (CommanderDef.StrategyPrefs)
                        + unit desire (UtilityScorer over StrategyDef.FitWeights)
             expectation loses → DisobedienceEvent (role-play text hook)
Strategy   — per unit: picks/keeps a persistent GoalInstance via UtilityScorer
             (GoalDef.MinTurns + SwitchMargin hysteresis = no goal churn)
Goal       — strategy+goal modifiers → ScoringContext biases
Utility    — AIBrain scores concrete actions; per-unit action WeightSet
```

**Resources** (`ai/Resources/`, JSON, flyweight via `AIDefRegistry`):
- `goals/` — kill_unit, help_ally, hold_position (auto-selected) + move_to_position, use_skill (order-only; selection weights + active modifiers + persistence params)
- `strategies/` — tank, dps, berserker, cautious, protector, retreating (fit weights, goal prefs, scoring modifiers)
- `commanders/` — orc_warchief, goblin_boss, knight_captain (strategy prefs + aggression)
- `actions/` — default_actions, reckless_actions (per-unit AIBrain weights)

```csharp
var registry = AIDefRegistry.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));
var planner = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief"),
    director: myScriptedDirector /* optional */) { Log = decisionLog /* optional */ };
var manager = new BattleManager(battle, planner.Plan);         // threaded-safe
planner.SetUnitActionWeights(unit, registry.ActionWeights("RecklessActions")!);
var drama = planner.DisobedienceSnapshot();                    // "commander wanted A, unit chose B"

// Orders — externally assigned concrete goals (scenario/commander scripting):
planner.OrderMoveTo(unit, new HexCoord(-2, 0), battle);        // go there; attacks zeroed en route,
                                                               // completes on arrival
planner.OrderUseSkill(unit, chopSkill, targetUnit, battle);    // prefer this skill on this target;
                                                               // expires after use_skill.json expireTurns
```

Orders carry `orderScore` (1000) so normal goals can't displace them, survive strategy
reassignment (`GoalInstance.IsOrder`), and end by completion, expiry (`expireTurns`), or target death.

**Thread-safety**: commander phase runs once per turn under a lock (first `Plan` call does the work, parallel callers wait, then read frozen assignments); per-unit goal state is only touched by that unit's planning thread; defs/weights are immutable flyweights. Headless check: `dotnet run --project init/TacticalGame.Init.csproj -- --vision-ai` (threaded, orc vs goblin commanders, prints disobedience log).

**Scripted fights** (`ScriptedFights` in `ai/AI/Layers/ScriptedTriggers.cs`): `LastSurvivorsFlee` (last N always flee), `NoRetreat` (never flee — forced attack when hurt), `Vengeance` (enrage when watched ally dies), `Assassination` (hunt one target until dead). Triggers may set `Repeats => true` to fire every turn their condition holds.

Behavior tests: `tests/AIBehaviorTests.cs`.

---

# Simple chain planner (AIPlanner)

## Overview

The AI system is organized into 5 layers. Layers 1–4 form a **scoring chain** where each layer produces context consumed by the layer below it. Layer 5 (Director) is architecturally separate and can hard-override the entire chain.

```
Director (Layer 5) ── override? ──→ forced AIAction (bypass chain)
                           │ null
Commander (Layer 4) ──→ team-level doctrine      (once per team)
                           │
Strategy  (Layer 3) ──→ unit role & targets      (per unit)
                           │
Goal      (Layer 2) ──→ scoring biases           (per unit)
                           │
Utility   (Layer 1) ──→ scores actions ──→ AIAction
```

## ScoringContext

Single flat data object filled top-down. Each layer owns a section:

| Field | Layer | Type | Purpose |
|-------|-------|------|---------|
| `AggressionBias` | Commander | float (-1..+1) | Scales attack scores |
| `PriorityTarget` | Commander | Unit? | Team-level focus target |
| `ProtectTarget` | Commander | Unit? | Unit to protect |
| `ForceRetreatUnit` | Commander | Unit? | Force specific unit to retreat |
| `Role` | Strategy | UnitRole | Unit's tactical role |
| `HoldPosition` | Strategy | HexCoord? | Position to hold |
| `AssignedTarget` | Strategy | Unit? | Personal kill target |
| `ShouldRetreat` | Strategy | bool | Unit should flee |
| `KillTargetBonus` | Goal | float | Score bonus for assigned target |
| `SurvivalBias` | Goal | float | Bias toward fleeing (>0) |
| `PositionBonus` | Goal | float | Weight for holding position |

## Layer Interfaces

```csharp
// Layer 4 — one instance per team
public interface ICommanderLayer
{
    void Evaluate(AIBlackboard blackboard, ScoringContext context);
}

// Layer 3 — per unit
public interface IStrategyLayer
{
    void Evaluate(Unit unit, AIBlackboard blackboard, ScoringContext context);
}

// Layer 2 — per unit
public interface IGoalLayer
{
    void Evaluate(Unit unit, AIBlackboard blackboard, ScoringContext context);
}

// Layer 5 — separate, hard override
public interface IDirectorLayer
{
    AIAction? Override(Unit unit, AIBlackboard blackboard);
}
```

Layer 1 (Utility) is `AIBrain.DecideAction(unit, blackboard, context)`.

## AIPlanner

Orchestrates the layer chain. Caches Commander context per team per turn.

```csharp
var planner = new AIPlanner(
    commander: new DefaultCommanderLayer { AggressionBias = 0.5f },
    strategy: new DefaultStrategyLayer(),
    goal: new DefaultGoalLayer(),
    director: myScriptedDirector       // optional
);

// Use with BattleManager
var manager = new BattleManager(battle, planner.Plan);
```

Call `planner.BeginTurn()` before each turn to clear Commander cache.

## TeamPlanner

Routes units to different AIPlanner instances by team index.

```csharp
var teamPlanner = new TeamPlanner(defaultPlanner);
teamPlanner.SetTeamPlanner(0, playerPlanner);
teamPlanner.SetTeamPlanner(1, enemyPlanner);

var manager = new BattleManager(battle, teamPlanner.Plan);
```

## Director (ScriptedDirector)

Bypasses the scoring chain entirely. Triggers fire once.

```csharp
var director = new ScriptedDirector();
director.AddTrigger(new FleeOnDeathTrigger(unitC, unitB));
// When unitB dies, unitC gets a forced flee action
```

Extend `DirectorTrigger` to create custom triggers:
- `ShouldFire(blackboard)` — condition check
- `CreateAction(blackboard)` — the forced action
- `MarkFired()` — called automatically, trigger won't fire again

## How Context Biases Affect Scoring

**Targeted skill scores:**
```
base = estimatePower + 100 * (1 - enemyHpRatio)
score = base * (1 + AggressionBias)
      + KillTargetBonus    (if enemy == AssignedTarget)
      - SurvivalBias       (if > 0)
```

**Movement scores:**
```
Normal:  (distanceReduction) * 30
Fleeing: (distanceIncrease) * 30 + SurvivalBias    (when SurvivalBias > 0)
Hold:    + (distReductionToHold) * PositionBonus    (when HoldPosition set)
```

## File Structure

```
ai/AI/
├── AIAction.cs, AIBlackboard.cs, AIBrain.cs
├── AIPlanner.cs          — layer orchestrator
├── ScoringContext.cs     — context data + UnitRole enum
├── TeamPlanner.cs        — per-team routing
└── Layers/
    ├── ICommanderLayer.cs, IStrategyLayer.cs, IGoalLayer.cs, IDirectorLayer.cs
    ├── DefaultCommanderLayer.cs, DefaultStrategyLayer.cs, DefaultGoalLayer.cs
    └── ScriptedDirector.cs + DirectorTrigger
```

## Swapping Layers

Each layer is an interface — implement your own and pass it to AIPlanner:

```csharp
public class CautiousStrategy : IStrategyLayer
{
    public void Evaluate(Unit unit, AIBlackboard blackboard, ScoringContext context)
    {
        // Always hold position, never push forward
        context.Role = UnitRole.Defender;
        context.HoldPosition = unit.Position;
        context.PositionBonus = 40f;
    }
}
```
