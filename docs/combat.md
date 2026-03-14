# Combat

## Pipeline

```
Skill.CreateCommand(user, weapon, targetHex, battle)
  → PrototypeCommand (mutable)
  → attacker traits modify effects
  → each target's traits modify effects
  → Finalize() → CompoundCommand (sealed)
```

`CombatPipeline.Resolve()` orchestrates this. Skills own target resolution + effect creation. Pipeline just runs trait hooks and seals the result.

## PrototypeCommand

Mutable draft. `Unit, Weapon, Skill, TargetHex, HitTargets, Effects`.
Skills build it, traits modify `Effects`, then `Finalize()` locks it into CompoundCommand.

## Skills

Abstract `SkillDef`: `Id, Name, FatigueCost, Range, HitPattern`.

- `CreateCommand` — resolves targets, builds effects, returns PrototypeCommand
- `EstimatePower(user, weapon)` — cheap score for AI, no allocs
- `HasValidUse(user, battle)` — filters invalid options (default: enemy in range)

Current skills:
- **Slash** — balanced sword hit, def/3 reduction
- **Chop** — heavy axe hit, 120% weapon scale, def/2 reduction
- **ShieldWall** — defensive stance (stub), requires adjacent enemy

## CombatCalculations

Pure static math:
- `ResolveTargets(user, pattern, targetHex, battle)` — all alive units in pattern
- `RawDamage(atk, weaponBonus, scale)` — atk + weaponBonus * scale
- `ReduceByDefense(raw, def, divisor)` — max(1, raw - def/divisor)
- `SplitDamage(amount, armor)` — armor absorbs first, rest to HP

## HitPattern

Hex offsets authored facing East. `Resolve(userPos, targetHex)` rotates them to match attack direction via CCW rotation + cube dot product.

Presets: `SingleTarget`, `Line2` (pierce), `Sweep3` (arc).

## Traits

`ITrait.ModifyEffects(PrototypeCommand, owner)` — can read/modify any effect in the cmd. Runs after skill creates effects, before finalize.
