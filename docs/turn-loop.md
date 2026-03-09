# Turn Loop

Core loop lives in `BattleManager.StepTurn()`.

```
pending = all alive units

while pending not empty:
    planned = PlanAll(pending)      // read-only, parallelizable
    pending = ExecuteAll(planned)   // sequential, immediate effects
```

## Planning

`PlanAction` delegate — swappable. Default uses `AIBrain`.
Planning only reads state. Never mutates. Safe for `Parallel.For`.

Enemy cache built once per loop iteration, not per unit.

## Execution

Sequential. Each action validated then executed immediately.

- **Move**: blocked if cell occupied, unwalkable, or already claimed this loop. Failed units replan.
- **Attack**: invalid if target dead. Failed units replan.

Valid actions become `IBattleCommand`, executed in-place, appended to command list.

## Replanning

Failed units go back to `pending`. Loop repeats.
Safety limit (50) prevents infinite loops.

## Output

`StepTurn()` returns `List<IBattleCommand>` — the ordered sequence of everything that happened. Stored in `_turnHistory` for undo.
