using System.Collections.Generic;

namespace TacticalGame.AI.Utility
{
    public static class GoalTypes
    {
        public const string KillUnit = "KillUnit";
        public const string HelpAlly = "HelpAlly";
        public const string HoldPosition = "HoldPosition";
        public const string MoveToPosition = "MoveToPosition";
        public const string UseSkill = "UseSkill";
    }

    // Deserialized from ai/Resources/goals/*.json. Immutable by convention
    // after load (flyweight, shared across units and threads).
    public class GoalDef
    {
        public string Type { get; set; } = "";

        // Persistence: keep the goal at least this many turns...
        public int MinTurns { get; set; } = 2;
        // ...and after that, only switch if a rival goal wins by this margin.
        public float SwitchMargin { get; set; } = 10f;
        // Hard lifetime: goal expires after this many turns (0 = never).
        public int ExpireTurns { get; set; }

        // Weights for selecting this goal (consideration name → weight).
        public Dictionary<string, float> SelectionWeights { get; set; } = new();

        // ScoringContext values applied while the goal is active.
        public Dictionary<string, float> Modifiers { get; set; } = new();

        public float Modifier(string key, float fallback = 0f)
            => Modifiers.TryGetValue(key, out var v) ? v : fallback;
    }

    // Deserialized from ai/Resources/strategies/*.json.
    public class StrategyDef
    {
        public string Name { get; set; } = "";

        // How well a unit fits this strategy (consideration name → weight).
        // Scored per unit — this is the unit's "personal desire".
        public Dictionary<string, float> FitWeights { get; set; } = new();

        // Which goal types this strategy prefers (goal type → weight).
        public Dictionary<string, float> GoalPrefs { get; set; } = new();

        // ScoringContext values applied while the strategy is assigned.
        public Dictionary<string, float> Modifiers { get; set; } = new();

        public float Modifier(string key, float fallback = 0f)
            => Modifiers.TryGetValue(key, out var v) ? v : fallback;

        public float GoalPref(string goalType)
            => GoalPrefs.TryGetValue(goalType, out var v) ? v : 0f;
    }

    // Deserialized from ai/Resources/commanders/*.json.
    public class CommanderDef
    {
        public string Name { get; set; } = "";
        public float AggressionBias { get; set; }

        // Commander's expectation score per strategy name.
        public Dictionary<string, float> StrategyPrefs { get; set; } = new();

        public float StrategyPref(string strategyName)
            => StrategyPrefs.TryGetValue(strategyName, out var v) ? v : 0f;
    }
}
