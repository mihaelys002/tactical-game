using System.Collections.Concurrent;
using System.Collections.Generic;
using TacticalGame.AI.Debug;
using TacticalGame.AI.Layers;
using TacticalGame.AI.Utility;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    // Full 5-layer planner driven by resource files (see docs/ai-layers-design-intent.md).
    //
    //   Director   — scripted override, checked first
    //   Commander  — once per turn (locked phase): assigns a StrategyDef to every
    //                unit via dual score = commander expectation + unit desire
    //   Strategy   — per unit: picks/keeps a persistent GoalInstance via UtilityScorer
    //   Goal       — translates strategy+goal into ScoringContext biases
    //   Utility    — AIBrain scores concrete actions with per-unit action weights
    //
    // Thread-safety: the commander phase runs once under _turnLock (first Plan
    // call of the turn does the work, parallel callers wait, then read frozen
    // state). Per-unit goal state is only touched by that unit's planning thread.
    public class UtilityAIPlanner
    {
        private readonly AIDefRegistry _registry;
        private readonly CommanderDef _commander;
        private readonly IDirectorLayer? _director;
        private readonly WeightSet? _defaultActionWeights;

        private readonly Dictionary<StrategyDef, WeightSet> _fitWeights = new();
        private readonly GoalDef? _killGoal;
        private readonly GoalDef? _helpGoal;
        private readonly GoalDef? _holdGoal;
        private readonly GoalDef? _moveGoal;
        private readonly GoalDef? _useSkillGoal;

        private readonly ConcurrentDictionary<Unit, UnitAIState> _states = new();
        private readonly ConcurrentDictionary<Unit, WeightSet> _unitActionWeights = new();

        private readonly object _turnLock = new();
        private int _assignedTurn = -1;
        private readonly List<DisobedienceEvent> _disobedience = new();

        public DecisionLog? Log { get; set; }

        public UtilityAIPlanner(
            AIDefRegistry registry,
            CommanderDef commander,
            IDirectorLayer? director = null,
            WeightSet? actionWeights = null)
        {
            _registry = registry;
            _commander = commander;
            _director = director;
            _defaultActionWeights = actionWeights;

            foreach (var strategy in registry.Strategies)
                _fitWeights[strategy] = new WeightSet(strategy.Name, strategy.FitWeights);

            foreach (var goal in registry.Goals)
            {
                if (goal.Type == GoalTypes.KillUnit) _killGoal = goal;
                else if (goal.Type == GoalTypes.HelpAlly) _helpGoal = goal;
                else if (goal.Type == GoalTypes.HoldPosition) _holdGoal = goal;
                else if (goal.Type == GoalTypes.MoveToPosition) _moveGoal = goal;
                else if (goal.Type == GoalTypes.UseSkill) _useSkillGoal = goal;
            }
        }

        // ── Orders: externally assigned concrete goals ───────────────────
        // Orders get the def's "orderScore" so regular candidates can't
        // displace them; they end by completion (arrival), expiry, or death.

        public void OrderMoveTo(Unit unit, HexCoord position, BattleState battle)
        {
            if (_moveGoal == null) throw new System.InvalidOperationException(
                "MoveToPosition goal def not loaded");
            StateOf(unit).Goal = new GoalInstance(_moveGoal, battle.TurnNumber, targetHex: position)
            { Score = _moveGoal.Modifier("orderScore", 1000f), IsOrder = true };
        }

        public void OrderUseSkill(Unit unit, SkillDef skill, Unit? target, BattleState battle)
        {
            if (_useSkillGoal == null) throw new System.InvalidOperationException(
                "UseSkill goal def not loaded");
            StateOf(unit).Goal = new GoalInstance(_useSkillGoal, battle.TurnNumber,
                targetUnit: target, skill: skill)
            { Score = _useSkillGoal.Modifier("orderScore", 1000f), IsOrder = true };
        }

        public void SetUnitActionWeights(Unit unit, WeightSet weights)
            => _unitActionWeights[unit] = weights;

        public UnitAIState StateOf(Unit unit)
            => _states.GetOrAdd(unit, _ => new UnitAIState());

        // Read-only introspection for observers (debug overlays, message
        // engine). Never creates state — planning code uses StateOf.
        public UnitAIState? PeekState(Unit unit)
            => _states.TryGetValue(unit, out var state) ? state : null;

        public List<DisobedienceEvent> DisobedienceSnapshot()
        {
            lock (_turnLock)
                return new List<DisobedienceEvent>(_disobedience);
        }

        // ── Save/Load (see GameSave in orchestration) ─────────────────────
        // Battle memory only: strategies, goals, commander phase stamp.
        // Entries follow battle.Units order so identical sessions produce
        // identical save files.

        public string CommanderName => _commander.Name;

        public PlannerSave ExportState(BattleState battle)
        {
            lock (_turnLock)
            {
                var entries = new List<PlannerSave.Entry>();
                foreach (var unit in battle.Units)
                    if (_states.TryGetValue(unit, out var state))
                        entries.Add(new PlannerSave.Entry(unit, state));
                return new PlannerSave(_commander.Name, _assignedTurn, entries);
            }
        }

        public void ImportState(PlannerSave save)
        {
            lock (_turnLock)
            {
                _states.Clear();
                foreach (var entry in save.Units)
                    _states[entry.Unit] = entry.State;
                _assignedTurn = save.AssignedTurn;
            }
        }

        // ── Entry point (PlanAction-compatible) ──────────────────────────

        public AIAction? Plan(Unit unit, BattleState battle)
        {
            var bb = new AIBlackboard(battle, unit);

            if (_director != null)
            {
                var forced = _director.Override(unit, bb);
                if (forced != null) return forced;
            }

            EnsureCommanderPhase(battle);

            var state = StateOf(unit);
            SelectGoal(unit, state, bb, battle.TurnNumber);

            var context = BuildContext(unit, state);

            if (Log == null)
                return AIBrain.DecideAction(unit, bb, context);

            var trace = new DecisionTrace(unit, battle.TurnNumber) { Context = context };
            var result = AIBrain.DecideAction(unit, bb, context, trace);
            Log.Add(trace);
            return result;
        }

        // ── Commander phase (Layer 4): dual-score strategy assignment ────

        private void EnsureCommanderPhase(BattleState battle)
        {
            lock (_turnLock)
            {
                if (_assignedTurn == battle.TurnNumber) return;

                foreach (var unit in battle.Units)
                {
                    if (!unit.IsAlive) continue;
                    AssignStrategy(unit, battle);
                }

                _assignedTurn = battle.TurnNumber;
            }
        }

        private void AssignStrategy(Unit unit, BattleState battle)
        {
            var enemies = battle.GetEnemies(unit);
            var considerations = FitConsiderations(unit, enemies);

            StrategyDef? final = null, commanderPick = null;
            float bestTotal = float.MinValue, bestExpectation = float.MinValue;
            float finalDesire = 0f, commanderPickDesire = 0f;

            foreach (var strategy in _registry.Strategies)
            {
                float desire = UtilityScorer.Score(considerations, _fitWeights[strategy]);
                float expectation = _commander.StrategyPref(strategy.Name);
                float total = expectation + desire;

                if (total > bestTotal) { bestTotal = total; final = strategy; finalDesire = desire; }
                if (expectation > bestExpectation) { bestExpectation = expectation; commanderPick = strategy; commanderPickDesire = desire; }
            }

            if (final == null) return;

            var state = StateOf(unit);
            bool strategyChanged = state.Strategy != final;
            state.Strategy = final;
            state.HoldPosition = final.Modifier("holdCurrentPosition") > 0 ? unit.Position : null;

            // Strategy changed → old goal no longer follows the plan.
            // Explicit orders survive — the unit was told, not inclined.
            if (strategyChanged && state.Goal?.IsOrder != true)
                state.Goal = null;

            // The role-playing moment: commander's preferred strategy lost to
            // the unit's own desire. Gap measures how strongly the unit resists.
            if (commanderPick != null && final != commanderPick)
            {
                _disobedience.Add(new DisobedienceEvent(
                    unit, battle.TurnNumber,
                    commanderPick.Name, final.Name,
                    finalDesire - commanderPickDesire));
            }
        }

        private static List<(string, float)> FitConsiderations(Unit unit, List<Unit> enemies)
        {
            float hpRatio = (float)unit.Stats.CurrentHP / unit.Stats.MaxHP;
            float armorRatio = unit.Stats.MaxArmor > 0
                ? (float)unit.Stats.CurrentArmor / unit.Stats.MaxArmor : 0f;
            float attackPower = System.Math.Min(unit.Stats.Attack, 100) / 100f;

            int minDist = int.MaxValue;
            foreach (var e in enemies)
                minDist = System.Math.Min(minDist, unit.Position.DistanceTo(e.Position));
            float proximity = enemies.Count == 0 ? 0f : 1f - System.Math.Min(minDist, 8) / 8f;

            return new List<(string, float)>
            {
                ("hpRatio", hpRatio),
                ("hpMissing", 1f - hpRatio),
                ("armorRatio", armorRatio),
                ("attackPower", attackPower),
                ("enemyProximity", proximity),
            };
        }

        // ── Strategy phase (Layer 3): pick/keep a persistent goal ────────

        private void SelectGoal(Unit unit, UnitAIState state, AIBlackboard bb, int turn)
        {
            var strategy = state.Strategy;
            if (strategy == null) return;

            var current = state.Goal;

            // Finished orders (arrived / expired) drop back to normal selection.
            if (current != null && (current.IsCompleted(unit) || current.IsExpired(turn)))
            {
                state.Goal = null;
                current = null;
            }

            // Persistence: valid goal younger than MinTurns is never questioned.
            if (current != null && current.IsValid && current.Age(turn) < current.Def.MinTurns)
                return;

            var best = BestGoalCandidate(unit, strategy, bb, turn);

            if (current == null || !current.IsValid)
            {
                state.Goal = best;
                return;
            }

            // Hysteresis: switch only if the challenger clearly wins.
            if (best != null && best.Score > current.Score + current.Def.SwitchMargin)
                state.Goal = best;
        }

        private GoalInstance? BestGoalCandidate(Unit unit, StrategyDef strategy, AIBlackboard bb, int turn)
        {
            GoalInstance? best = null;

            void Consider(GoalInstance candidate)
            {
                if (best == null || candidate.Score > best.Score) best = candidate;
            }

            if (_killGoal != null)
            {
                var weights = new WeightSet(_killGoal.Type, _killGoal.SelectionWeights);
                foreach (var enemy in bb.Enemies)
                {
                    float hpMissing = 1f - (float)enemy.Stats.CurrentHP / enemy.Stats.MaxHP;
                    float proximity = 1f - System.Math.Min(unit.Position.DistanceTo(enemy.Position), 10) / 10f;

                    float score = UtilityScorer.Score(new List<(string, float)>
                    {
                        ("enemyHpMissing", hpMissing),
                        ("proximity", proximity),
                    }, weights) + strategy.GoalPref(GoalTypes.KillUnit);

                    Consider(new GoalInstance(_killGoal, turn, targetUnit: enemy) { Score = score });
                }
            }

            if (_helpGoal != null)
            {
                var weights = new WeightSet(_helpGoal.Type, _helpGoal.SelectionWeights);
                foreach (var ally in bb.Friends)
                {
                    if (ally == unit || !ally.IsAlive) continue;

                    float allyHpMissing = 1f - (float)ally.Stats.CurrentHP / ally.Stats.MaxHP;
                    if (allyHpMissing < 0.25f) continue; // ally not in trouble

                    var threat = ClosestTo(ally, bb.Enemies);
                    if (threat == null) continue;

                    float allyProximity = 1f - System.Math.Min(unit.Position.DistanceTo(ally.Position), 10) / 10f;

                    float score = UtilityScorer.Score(new List<(string, float)>
                    {
                        ("allyHpMissing", allyHpMissing),
                        ("allyProximity", allyProximity),
                    }, weights) + strategy.GoalPref(GoalTypes.HelpAlly);

                    // Helping = killing the enemy threatening the ally.
                    Consider(new GoalInstance(_helpGoal, turn, targetUnit: threat) { Score = score });
                }
            }

            if (_holdGoal != null)
            {
                var weights = new WeightSet(_holdGoal.Type, _holdGoal.SelectionWeights);
                var hold = new List<(string, float)> { ("constant", 1f) };
                float score = UtilityScorer.Score(hold, weights) + strategy.GoalPref(GoalTypes.HoldPosition);

                Consider(new GoalInstance(_holdGoal, turn,
                    targetHex: StateOf(unit).HoldPosition ?? unit.Position)
                { Score = score });
            }

            return best;
        }

        private static Unit? ClosestTo(Unit reference, IReadOnlyList<Unit> units)
        {
            Unit? closest = null;
            int closestDist = int.MaxValue;
            foreach (var u in units)
            {
                int d = reference.Position.DistanceTo(u.Position);
                if (d < closestDist) { closestDist = d; closest = u; }
            }
            return closest;
        }

        // ── Goal phase (Layer 2): strategy+goal → scoring biases ─────────

        private ScoringContext BuildContext(Unit unit, UnitAIState state)
        {
            var context = new ScoringContext();
            var strategy = state.Strategy;
            var goal = state.Goal;

            if (strategy != null)
            {
                context.StrategyName = strategy.Name;
                context.AggressionBias = _commander.AggressionBias + strategy.Modifier("aggressionBias");

                float survival = strategy.Modifier("survivalBias");
                float hurtThreshold = strategy.Modifier("hurtThreshold");
                float hpRatio = (float)unit.Stats.CurrentHP / unit.Stats.MaxHP;
                if (hurtThreshold > 0 && hpRatio < hurtThreshold)
                    survival += strategy.Modifier("survivalBiasWhenHurt");
                context.SurvivalBias = survival;

                context.PositionBonus = strategy.Modifier("positionBonus");
                if (state.HoldPosition is HexCoord hold)
                    context.HoldPosition = hold;

                context.ShouldRetreat = survival > 0;
                context.Role = survival > 0 ? UnitRole.Retreating
                    : context.PositionBonus > 0 ? UnitRole.Defender : UnitRole.Attacker;
            }

            if (goal != null && goal.IsValid)
            {
                context.GoalLabel = goal.Label;

                if (goal.TargetUnit != null)
                {
                    context.AssignedTarget = goal.TargetUnit;
                    context.KillTargetBonus = goal.Def.Modifier("killTargetBonus");
                }

                if (goal.TargetHex is HexCoord goalHex)
                {
                    context.HoldPosition = goalHex;
                    context.PositionBonus += goal.Def.Modifier("positionBonus");
                }

                if (goal.Skill != null)
                {
                    context.PreferredSkill = goal.Skill;
                    context.PreferredSkillBonus = goal.Def.Modifier("preferredSkillBonus", 60f);
                }

                // A move order overrides everything else: retreat pull is off,
                // and attacks are zeroed (aggression -1 → ×0) — the unit is
                // going where it was told, not stopping to fight.
                if (goal.Def.Type == GoalTypes.MoveToPosition)
                {
                    context.SurvivalBias = 0f;
                    context.ShouldRetreat = false;
                    context.AggressionBias = -1f;
                    context.AssignedTarget = null;
                    context.KillTargetBonus = 0f;
                }
            }

            context.ActionWeights = _unitActionWeights.TryGetValue(unit, out var uw)
                ? uw : _defaultActionWeights;

            return context;
        }
    }
}
