using System.Collections.Generic;
using Newtonsoft.Json;
using TacticalGame.AI.Utility;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    // Serializable snapshot of a UtilityAIPlanner's per-battle memory.
    // Same rules as core saves: fields serialization, defs stored by name
    // via converters, Unit references resolved by the surrounding graph.
    // Config (registry, director, action weights) is NOT here — it is
    // rebuilt from resource files on load.
    [JsonObject(MemberSerialization.Fields)]
    public class PlannerSave
    {
        private readonly string _commander;
        private readonly int _assignedTurn;
        private readonly List<Entry> _units;

        public string Commander => _commander;
        public int AssignedTurn => _assignedTurn;
        public IReadOnlyList<Entry> Units => _units;

        private PlannerSave() { _commander = ""; _units = new List<Entry>(); } // serialization only

        public PlannerSave(string commander, int assignedTurn, List<Entry> units)
        {
            _commander = commander;
            _assignedTurn = assignedTurn;
            _units = units;
        }

        [JsonObject(MemberSerialization.Fields)]
        public class Entry
        {
            private readonly Unit _unit;
            private readonly UnitAIState _state;

            public Unit Unit => _unit;
            public UnitAIState State => _state;

            private Entry() { _unit = null!; _state = null!; } // serialization only

            public Entry(Unit unit, UnitAIState state)
            {
                _unit = unit;
                _state = state;
            }
        }
    }

    // Def-by-name converters, mirroring core's DefConverters. AI defs live
    // in an AIDefRegistry instance (not a static registry), so the converters
    // carry the registry they resolve against.
    public class StrategyDefConverter : JsonConverter<StrategyDef>
    {
        private readonly AIDefRegistry _registry;
        public StrategyDefConverter(AIDefRegistry registry) { _registry = registry; }

        public override void WriteJson(JsonWriter writer, StrategyDef? value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.Name);
        }

        public override StrategyDef? ReadJson(JsonReader reader, System.Type objectType,
            StrategyDef? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var name = reader.Value as string;
            return name == null ? null : _registry.Strategy(name);
        }
    }

    public class GoalDefConverter : JsonConverter<GoalDef>
    {
        private readonly AIDefRegistry _registry;
        public GoalDefConverter(AIDefRegistry registry) { _registry = registry; }

        public override void WriteJson(JsonWriter writer, GoalDef? value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.Type);
        }

        public override GoalDef? ReadJson(JsonReader reader, System.Type objectType,
            GoalDef? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var type = reader.Value as string;
            return type == null ? null : _registry.Goal(type);
        }
    }
}
