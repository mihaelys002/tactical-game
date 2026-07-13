using Newtonsoft.Json;
using TacticalGame.AI.Utility;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    // A concrete, persistent objective: Kill Unit X / Help Ally Y / Hold (q,r) /
    // Move to (q,r) / Use skill S at target Y.
    [JsonObject(MemberSerialization.Fields)]
    public class GoalInstance
    {
        public GoalDef Def { get; }
        public Unit? TargetUnit { get; }
        public HexCoord? TargetHex { get; }
        public SkillDef? Skill { get; }
        public int StartTurn { get; }
        public float Score { get; internal set; }

        // Orders (OrderMoveTo / OrderUseSkill) survive strategy reassignment.
        public bool IsOrder { get; internal set; }

        private GoalInstance() { Def = null!; } // serialization only

        public GoalInstance(GoalDef def, int startTurn, Unit? targetUnit = null,
            HexCoord? targetHex = null, SkillDef? skill = null)
        {
            Def = def;
            StartTurn = startTurn;
            TargetUnit = targetUnit;
            TargetHex = targetHex;
            Skill = skill;
        }

        public bool IsValid => TargetUnit == null || TargetUnit.IsAlive;

        public int Age(int currentTurn) => currentTurn - StartTurn;

        public bool IsExpired(int currentTurn)
            => Def.ExpireTurns > 0 && Age(currentTurn) >= Def.ExpireTurns;

        // MoveToPosition completes on arrival.
        public bool IsCompleted(Unit unit)
            => Def.Type == GoalTypes.MoveToPosition
               && TargetHex is HexCoord hex && unit.Position == hex;

        public string Label
        {
            get
            {
                string skill = Skill != null ? $" [{Skill.Name}]" : "";
                return TargetUnit != null
                    ? $"{Def.Type}{skill} {TargetUnit}"
                    : TargetHex != null ? $"{Def.Type}{skill} {TargetHex}" : $"{Def.Type}{skill}";
            }
        }
    }

    // Per-unit AI memory. Written during the commander phase (under the
    // planner's turn lock) and by the unit's own planning thread — never
    // shared between planning threads.
    [JsonObject(MemberSerialization.Fields)]
    public class UnitAIState
    {
        public StrategyDef? Strategy { get; internal set; }
        public GoalInstance? Goal { get; internal set; }

        // Captured when a hold-position strategy is assigned.
        public HexCoord? HoldPosition { get; internal set; }
    }

    // "Commander wants A, unit wants B, B won" — the role-playing moment.
    public class DisobedienceEvent
    {
        public Unit Unit { get; }
        public int Turn { get; }
        public string CommanderWanted { get; }
        public string UnitChose { get; }
        public float DesireGap { get; }

        public DisobedienceEvent(Unit unit, int turn, string commanderWanted, string unitChose, float desireGap)
        {
            Unit = unit;
            Turn = turn;
            CommanderWanted = commanderWanted;
            UnitChose = unitChose;
            DesireGap = desireGap;
        }

        public override string ToString()
            => string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"T{Turn} {Unit}: commander wanted {CommanderWanted}, unit chose {UnitChose} (gap {DesireGap:0.0})");
    }
}
