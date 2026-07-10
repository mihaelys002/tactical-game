using TacticalGame.AI.Utility;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public enum UnitRole
    {
        None,
        Attacker,
        Defender,
        Flanker,
        Retreating
    }

    public class ScoringContext
    {
        // Commander (Layer 4) writes
        public float AggressionBias { get; set; }
        public Unit? PriorityTarget { get; set; }
        public Unit? ProtectTarget { get; set; }
        public Unit? ForceRetreatUnit { get; set; }

        // Strategy (Layer 3) reads above, writes
        public UnitRole Role { get; set; }
        public HexCoord? HoldPosition { get; set; }
        public Unit? AssignedTarget { get; set; }
        public bool ShouldRetreat { get; set; }

        // Goal (Layer 2) reads above, writes
        public float KillTargetBonus { get; set; }
        public float SurvivalBias { get; set; }
        public float PositionBonus { get; set; }

        // "Use skill S (at target Y)" order: bonus applies to this skill,
        // restricted to AssignedTarget when one is set.
        public SkillDef? PreferredSkill { get; set; }
        public float PreferredSkillBonus { get; set; }

        // Utility (Layer 1): per-unit action weights from a resource file
        // (flyweight). Null → AIBrain's built-in defaults.
        public WeightSet? ActionWeights { get; set; }

        // Debug labels for traces/overlay (set by UtilityAIPlanner).
        public string? StrategyName { get; set; }
        public string? GoalLabel { get; set; }
    }
}
