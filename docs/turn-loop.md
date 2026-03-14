# Turn Loop

`BattleManager.StepTurn()`:

```
recover fatigue for all alive units
pending = all alive units

while pending not empty (max 50):
    actions = PlanAll(pending)     // parallel-safe, read-only
    pending = ExecuteAll(actions)  // sequential
```

## Planning

`PlanAction` delegate, default uses `AIBrain`.
Returns `AIAction?` per unit. Planning never mutates state.

## AIAction

Abstract. Carries `Unit, Target, Score`. Subclasses:
- **MoveAction** — creates MoveCommand
- **SkillAction** — has `Skill, Weapon`, creates CompoundCommand via CombatPipeline

`CreateCommand(battle)` always returns a cmd. Validation is cmd's job.

## Execution

Sequential. For each action:
1. `action.CreateCommand(battle)` → cmd
2. `cmd.Execute(battle)` → bool
3. false → unit goes to failed list for replan

## Output

Returns `List<IBattleCommand>` — everything that happened. Stored for undo.
