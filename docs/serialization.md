# Serialization

## Overview

`BattleSave` serializes the full `BattleManager` (state + undo history) using Newtonsoft.Json.

## Three ways to serialize readonly/private members

Prefer `[JsonObject(MemberSerialization.Fields)]` on the class over `[JsonProperty]` on individual fields.

### 1. `[JsonObject(MemberSerialization.Fields)]` on the class (preferred)

Serializes **all** fields (including private and readonly) automatically. No per-member attributes needed.

```csharp
[JsonObject(MemberSerialization.Fields)]
public class BattleState
{
    private readonly HexGrid _grid;          // serialized
    private readonly List<Unit> _units;      // serialized
    private readonly List<List<Unit>> _teams; // serialized
}
```

### 2. `[JsonProperty]` on individual members

Use when only specific private members need serialization, or on classes where `Fields` mode isn't appropriate.

```csharp
public class BattleManager
{
    [JsonProperty]
    private int _turnNumber;       // private field → serialized
}
```

### 3. Constructor parameter matching (no attributes)

Newtonsoft matches JSON property names to constructor parameter names (case-insensitive).
After construction, it sets any remaining `public set` properties directly.

```csharp
public class DamageEffect : BattleEffect
{
    public int Amount { get; set; }                  // public set → auto-restored
    public int AppliedHpDamage { get; private set; } // private set → needs constructor param

    public DamageEffect(Unit source, Unit target, int amount,
        int appliedHpDamage = 0)                     // ← matched by name from JSON
    {
        Amount = amount;
        AppliedHpDamage = appliedHpDamage;
    }
}
```

## What works without any of the above

- **Public-set properties** — handled automatically
- **Computed properties** (e.g. `S => -Q - R`) — not stored, recomputed on access

## What does NOT serialize

- **Delegates** (e.g. `PlanAction`) — must be re-supplied after load
- **Event handlers** (`OnLog`) — subscribers must re-attach after load

## Settings

```csharp
PreserveReferencesHandling = PreserveReferencesHandling.All  // shared Unit refs stay shared
TypeNameHandling = TypeNameHandling.Auto                     // polymorphic (BattleEffect subclasses)
ConstructorHandling = AllowNonPublicDefaultConstructor
ObjectCreationHandling = ObjectCreationHandling.Replace      // always create new collections, never append to existing
```
