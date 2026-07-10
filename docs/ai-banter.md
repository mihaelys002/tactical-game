# Message Engine (Banter)

Text-only NPC banter on top of the battle. A **pure observer**: game logic and
AI never know it exists — no events are emitted for it, no hooks added. It
reads what already exists (`BattleState.TurnHistory`, planner introspection,
battle state), derives **facts**, matches authored **rules** against them, and
speaks lines. Lives in its own assembly: `messages/` (`TacticalGame.Messages`).

```
All message defs (flyweight JSON, loaded once)
  ↓ pool gating       — META conditions ("requires"), once at BeginBattle
Active pools for this battle
  ↓ rule matching     — fact type + conditions ("if"), per observed turn
Candidate lines
  ↓ memory filtering  — priority, cooldowns, once-battle/campaign, budget
Spoken MessageLine  →  becomes a Said fact (others can reply next turn)
```

## Usage

```csharp
var registry = MessageRegistry.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));
var meta = new CampaignContext { Chapter = 3 };
meta.Factions.Add("Orcs");
meta.Flags.Add("bridge_massacre");

var engine = new MessageEngine(registry, meta, campaignMemory, seed: 42,
    planners: new[] { orcPlanner, goblinPlanner });   // optional AI introspection
engine.SetVoice(orcUnit, "Orc");
engine.BeginBattle(battle);

// after every StepTurn() — safe to call from a background thread:
engine.ObserveTurn(battle);
foreach (var line in engine.DrainLines()) Show(line);

// campaign save:
string json = engine.Campaign.ToJson();               // burned once-per-campaign lines
var restored = CampaignMemory.FromJson(json);
```

Headless demo: `dotnet run --project init/TacticalGame.Init.csproj -- --vision-ai`
(orcs and goblins banter live: disobedience drama, kill boasts, grudge revenge).

**Godot display** — `src/Prototype/MessageBubbleLayer.cs` (separate module,
consumes `MessageLine` only): speech bubbles above units with a procedural
portrait (team-color head-and-shoulders plate), speaker name, word-wrapped
text, tail pointing at the unit. Border color by tag (disobedience gold,
revenge crimson, taunt orange, reply blue). One bubble per speaker (new line
replaces old), lifetime scales with text length, fade-out. In the prototype
scene: bubbles appear after each turn's animations; `M` toggles the layer;
undo/restart clears it.

## Facts (what happened)

Derived by `IFactSource` implementations — spectators, never participants:

| Source | Reads | Fact types (data vars) |
|---|---|---|
| `HistoryFactSource` | `TurnHistory` commands/effects | `Hurt` (attacker, victim; Amount=applied dmg), `Killed` (victim, killer), `SkillUsed` (skill), `Moved` (from, to) |
| `AIStateFactSource` | `planner.PeekState`, `DisobedienceSnapshot` | `StrategyAssigned` (strategy), `GoalChanged` (goal, goalType), `Disobeyed` (commanderWanted, unitChose; Amount=gap) |
| `SituationFactSource` | team counts | `Outnumbered`, `LastAlive` |
| engine itself | spoken lines | `Said` (text; carries the rule's tags), `AllyDied` (ally, killer) — fan-out of `Killed` |

Custom sources: implement `IFactSource`, `engine.AddFactSource(...)` — new fact
type strings work immediately in rules. Grudges are auto-remembered: ally killed,
or hurt for ≥25% max HP.

## Authoring a message

One rule = one small JSON block inside a pool file (`messages/Resources/banter/*.json`):

```json
{
  "pool": "orc_warband",
  "voice": "Orc",
  "requires": { "factionPresent": "Orcs" },
  "rules": [
    {
      "when": "Killed",
      "if": { "speakerIs": "actor", "chance": 0.7 },
      "say": ["{victim} was weak!", "MORE! WHO NEXT?!"],
      "tags": ["taunt"],
      "priority": 5,
      "cooldown": 2,
      "once": "none"
    }
  ]
}
```

- `voice` — pool applies to units given that voice via `SetVoice`; `""` = everyone.
- `requires` — meta conditions; **pool inactive unless all hold**: `factionPresent`,
  `chapterAtLeast`, `chapterBelow`, `flag`, `notFlag`, `battlesFoughtAtLeast`.
- `when` — fact type (table above, or custom).
- `if` — all must pass: `speakerIs` (actor default | target | allyOfActor |
  enemyOfActor | any), `chance`, `hpBelow/hpAbove` (speaker HP ratio),
  `amountAtLeast`, `tag` (fact tag, for `Said` replies), `targetIsSelf`,
  `grudgeAgainst` ("actor"/"target"), `isWinning/isLosing` (team HP ratio),
  `strategyIs`/`goalIs` (AI state), `saidNothingFor`, `dataEquals` (`{"strategy": "Berserker"}`).
  Unknown names **throw at load** — no silently dead messages.
  Register your own: `MessageConditions.Register(name, fn)` / `MetaConditions.Register`.
- `say` — variants; random non-repeating until all heard. Variables: `{unit}`
  (speaker), `{actor}`, `{target}`, `{amount}` + every fact data key (`{victim}`,
  `{killer}`, `{ally}`, `{commanderWanted}`, ...).
- `once` — `none` | `battle` | `campaign` (burned forever, persists via `CampaignMemory`).

## Anti-spam arbitration

Per turn: highest priority wins; one line per speaker; one line per rule;
`MaxLinesPerTurn` budget (default 3); per-rule cooldown in turns. Silence is a
valid outcome.

## Conversations

Every spoken line becomes a `Said` fact — everyone hears everyone. Reply rules
match `when: "Said"` + `tag` + `speakerIs: "enemyOfActor"`, producing organic
taunt → comeback chains. Engine memory (`BattleMemory`) holds the fact
timeline (pruned to `TimelineWindow` turns), grudges, and speech bookkeeping;
`CampaignMemory` persists across battles.

## Threading & determinism

Producers (game loop) never wait: `ObserveTurn` reads turn-stable state under
the engine's own lock and may run on a background thread; `DrainLines()` is
thread-safe. All randomness from one seeded RNG — same seed + same battle =
same lines (see `Determinism_SameSeed_SameLines` test).

Tests: `tests/MessageEngineTests.cs` (20 scenarios: pool gating, campaign
callbacks, grudges/revenge, taunt replies, substitution, variants, cooldown,
once, budget, custom facts/conditions, disobedience via planner, last-alive,
determinism, threaded battle).
