# AI Layers — Design Intent (verbatim, 2026-07-07)

Author's original words, copied verbatim. Do not edit. This is the target design for the 5-layer AI; `docs/ai-layers.md` describes the current (interim) implementation.

---

So we have it working LIke this:
Utility is the most simpistic It should work separetely of everything else. It uses formulas with weights to score each action. The scoring formulas are universal for each unit. The weights are defined as resource files and are unique per unit.(with lightweight pattern for the same score file)
│ Goal (DefaultGoalLayer) => this is something that unit has separetely from Utility. It adds modifiers to Scoring for Utility layer, which allow us to pick a specific target as a priority or an action as  a priority. It makes  NPC 'want' something more than it would ususally want it. Goals are supposed to be more or less persistant. IF strategy for unit decides that it should go after NPC#23. Unit will stick to this goal for some time. Strategy cannot switch goals too often. Goals are concrete Kill Unit X, Run to position (x,y) use skill S. There are resource files for Goals Like Kill unit, Move to position X help ally. Strategy Layer picks apropriate Goal and assigns

Strategy => It 'can' select/override Goals for each unit + it can provide modifiers for Utility scoring of certain actions(Actions assosiated with retreating is scored higher). So Strategy is something that is derived from Unit 'Role".The Goal Layer does not care about Unit Role. The Strategy Layer assigns a Goal based on Strategy + Unit stats And equipment. Selecting A Goal is done via separate AI(Utility AI) that picks apropriate goal for current strategy. Strategies Are also defined as Resource files. Strategies are general behavior: Tank, DPS, Protector, backstabber. Cautious, retreating.

Commander => selects and Assigns Strategies for Each NPC. Using Utility AI as always. Each commander has it's own resource file to store weights. Orc commanders will prefer aggressive strategies for it's units. Goblin Commanders will prefer Cowardly strategies for the units. This is done on top of universal scoring of strategies that take into account NPC stats/equipment and situation on battle field. This way even if Commander 'wants' to assign Aggressive action a Unit can ignore it completely if it is badly hurt or cowardly by nature. We need to track this As each strategy will get 2 scores commander expectation and NPC personal desire. The resulting Strategy is going to be selected by summ of 2 scores, but it will allow us to catch the most important moment ( Commander wants A, but unit disobeys and wants B) I want this to display role-playing text so I need a clear way of figuring this moment or being close to IT

And finally Director AI => this is for scripted behaviors, It can override any layer and any action directely and it is mostly for story fights.( like 3 last enemies will flee 100% or noone will ever flee or Unit X will becaome extremely agggressive if Unit Y dies) This is the most vague layer and the most simple as well.

What I am seeing is the need for Utility Ai and ability to store weights for it.  I want yuu to cppy this prompt into a file verbatum

---

## Hard Requirements (agreed 2026-07-07)

**Multithreaded AI calculations are a must.** Planned phase structure:

```
Phase 0 (sequential): Director triggers, BeginTurn
Phase 1 (parallel per team): Commander → strategy assignments, dual scores
        ── barrier ──
Phase 2 (parallel per unit): Strategy → Goal → Utility → AIAction
        ── barrier ──
Phase 3 (sequential): execute commands
```

Rules: scoring is pure reads (BattleState + immutable weight resources) → parallel. Anything mutating shared state (execution, trigger firing, event log) → sequential. Per-unit AI state (goal persistence) lives on the unit, never in shared planner fields. Disobedience events collected per-thread, merged after barrier.
