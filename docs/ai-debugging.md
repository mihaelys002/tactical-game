# AI Weight Debugging

How to tune Utility AI weights and see why the AI picked what it picked. All three tiers implemented: foundation + Tier 1 in `ai/AI/Debug/`, Tier 2 overlay in `src/Prototype/AIDebugOverlay.cs`, Tier 3 replay via `DecisionHistory`. When the Utility AI + resource-file weights from `docs/ai-layers-design-intent.md` land, the trace terms extend to weight-file entries with no structural change.

## Implemented API

```csharp
// Capture decisions
var planner = new AIPlanner { Log = new DecisionLog() };   // thread-safe
planner.Plan(unit, battle);

// Tier 1 console log
foreach (var trace in planner.Log.Snapshot())
    Console.Write(DecisionFormatter.Format(trace, topN: 5));

// "Why not X?" diff
var chosen = trace.FindBreakdown(trace.Chosen!);
var wanted = trace.Candidates.First(c => /* the action you expected */);
Console.Write(DecisionFormatter.Diff(chosen, wanted));
```

Headless runner: `dotnet run --project init/TacticalGame.Init.csproj -- --debug-ai` prints top-3 candidates per unit per turn.

**Tier 2 — in the Godot prototype scene:**
- `D` toggle overlay, then click a unit to inspect it
- Candidate panel: top-8 candidates, stacked term bars (attack terms green, survival red/orange, position blue, self-cast purple), chosen action outlined yellow
- Hex heatmap: move candidates tinted red (worst) → green (best), score printed on hex
- Layer trace header: `Commander: aggr 0.5 → Strategy: Attacker, target Orc#3 → Goal: kill +50, survive 0, pos 0`
- Disobedience marker: red `!` over a unit that attacked someone other than the commander's `PriorityTarget`

**Tier 3 — replay:** every turn's traces are archived in `DecisionHistory` (survives undo). `[` / `]` browse past turns in the overlay.

Files: `ai/AI/Debug/ScoreBreakdown.cs` (ScoreTerm, ScoreBreakdown, DecisionTrace + Context), `DecisionLog.cs`, `DecisionHistory.cs`, `DecisionFormatter.cs`; `src/Prototype/AIDebugOverlay.cs`. `AIBrain.DecideAction` takes optional `DecisionTrace` — null (default) costs zero allocations. Term contributions always sum to the action's score (asserted in `tests/AIDebugTests.cs`).

## Foundation: ScoreBreakdown

Scores must be explainable data, not bare floats. When a debug flag is on, the Utility scorer emits a breakdown per candidate (flag off = zero allocations, no cost in release):

```csharp
class ScoreBreakdown
{
    string ActionLabel;              // "Slash → Orc#3"
    List<(string term, float input, float weight, float contribution)> Terms;
    float Total;
}
// Terms example:
//   ("enemyHpMissing", 0.7, 100,  +70)
//   ("aggressionBias", 0.5, ×1.5, +52)
//   ("survivalBias",   1.0, -100, -100)
```

Per unit per turn: all candidates + breakdowns, sorted by total. This one structure powers every tool below.

Threading: each planning thread writes only its own unit's log; merge after the phase barrier (same rule as disobedience events — see `docs/ai-layers-design-intent.md`).

## 1. Adjusting weights

### a. Normalize inputs to 0..1

Every scoring input (consideration) maps to 0..1 via a curve before weighting. Without this, mixed scales (`power + 100*hpFactor` vs `30*distance`) mean tuning one weight silently reweights everything else. With it, weights alone decide importance and are comparable: "kill bonus 0.5 vs survival 0.9".

### b. Behavior scenario tests = tuning harness

Encode desired behaviors as tiny headless scenarios (init runner + xunit already exist):

- "Wounded unit next to enemy → retreats, not attacks"
- "Two enemies, one at 10% HP → finishes the kill"
- "Tank strategy → moves to block chokepoint, ignores kill bait"

Tuning loop: edit weight in resource file → re-run suite → green = behavior kept, red names exactly which behavior broke. Weights live in resource files, so no recompile between iterations.

### c. "Why not X?" diff — the actual tuning tool

The tuning problem is always "unit did A, I wanted B". Tool: breakdowns of A and B side by side, per-term delta:

```
Chosen: Slash→Orc#3 (score 122)   Wanted: Retreat West (score 41)
  enemyHpMissing  +70   |  distanceGain   +60
  aggression      +52   |  survivalBias   -19  ← this term. SurvivalBias too weak vs aggression
```

Biggest delta term = the knob to turn. No guessing.

## 2. Visualizing

Three tiers, built in this order (cheap → rich):

### Tier 1 — console decision log (headless runner)

Print top-5 candidates with breakdowns per unit per turn. Terms aligned in columns so the eye catches the outlier. Usable in tests immediately; build this first.

### Tier 2 — Godot debug overlay (main tool)

Click a unit in battle:

- **Candidate panel** — sorted candidate list, horizontal stacked bar per candidate; each term a colored segment (green attack terms, red survival, blue position). Bar composition shows *why* at a glance.
- **Hex heatmap** — every candidate move hex tinted by score (dark red → bright green). Movement bugs become visible: wrong hexes glow.
- **Layer trace line** — header showing the chain: `Commander: aggressive(0.7) → Strategy: DPS → Goal: Kill Orc#3 (turn 2 of goal) → chosen action`. Localizes a bug to a layer before reading any numbers.
- **Disobedience marker** — icon over unit when unit desire beat commander expectation (role-play moment doubles as debug signal).

### Tier 3 — decision replay

Attach decision logs to turn history (undo/serialization already exist) → step back through the battle inspecting past decisions with the same overlay. Answers "why did it go wrong on turn 7" without reproducing.

## Build order

1. ScoreBreakdown + Tier 1 console log + scenario tests — small, pure C#, makes tuning possible immediately.
2. Overlay (Tier 2) after Utility AI + weight resource files exist.
3. Replay (Tier 3) last.
