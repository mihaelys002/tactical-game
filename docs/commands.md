# Commands

All state mutations go through `IBattleCommand`. Every command has `Execute` and `Undo`.

## Interface

```csharp
public interface IBattleCommand
{
    Unit Unit { get; }
    void Execute(BattleState battle);
    void Undo(BattleState battle);
}
```

## MoveCommand

Stores `Unit`, `From`, `To`. Execute moves forward. Undo moves back.

## AttackCommand

Stores attacker, target, total damage. On Execute, computes armor/HP split and applies.
Undo adds back exact amounts. No clamping — HP goes negative on death, positive on undo.

Key decisions:
- Dead units stay in state. Never removed.
- HP not clamped to 0. Enables pure arithmetic undo.
- `Unit.IsAlive` = `CurrentHP > 0`. That's the only death check.

## Undo

`BattleManager.UndoLastTurn()` pops last turn's command list, undoes all in reverse order.
Reverse order guarantees state consistency (a unit moved to cell X is moved back before another unit moves into X).

## Why commands own damage calc

`AttackCommand.Execute` does the armor/HP split itself (not `BattleState.ApplyDamage`).
It records `ArmorDamage` and `HpDamage` so Undo can reverse exactly what happened.

## Future

Commands will be weapon-driven. Axe has its own command types, spear has different ones.
