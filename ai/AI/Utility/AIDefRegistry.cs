using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TacticalGame.AI.Utility
{
    // Flyweight registry for AI resource files. Load once at startup, then
    // read-only — every unit/team shares the same def instances, and reads
    // are lock-free thread-safe.
    public class AIDefRegistry
    {
        private readonly Dictionary<string, GoalDef> _goals = new();
        private readonly Dictionary<string, StrategyDef> _strategies = new();
        private readonly Dictionary<string, CommanderDef> _commanders = new();
        private readonly Dictionary<string, WeightSet> _actionWeights = new();

        public IReadOnlyCollection<GoalDef> Goals => _goals.Values;
        public IReadOnlyCollection<StrategyDef> Strategies => _strategies.Values;
        public IReadOnlyCollection<CommanderDef> Commanders => _commanders.Values;
        public IReadOnlyCollection<WeightSet> ActionWeightSets => _actionWeights.Values;

        public GoalDef Goal(string type) => _goals[type];
        public StrategyDef Strategy(string name) => _strategies[name];
        public CommanderDef Commander(string name) => _commanders[name];
        public WeightSet? ActionWeights(string name)
            => _actionWeights.TryGetValue(name, out var w) ? w : null;

        public void Register(GoalDef def) => _goals[def.Type] = def;
        public void Register(StrategyDef def) => _strategies[def.Name] = def;
        public void Register(CommanderDef def) => _commanders[def.Name] = def;
        public void Register(WeightSet weights) => _actionWeights[weights.Name] = weights;

        // Loads Resources/{goals,strategies,commanders}/*.json from a root dir.
        public static AIDefRegistry LoadFromDirectory(string root)
        {
            var registry = new AIDefRegistry();

            foreach (var file in JsonFiles(Path.Combine(root, "goals")))
                registry.Register(Deserialize<GoalDef>(file));
            foreach (var file in JsonFiles(Path.Combine(root, "strategies")))
                registry.Register(Deserialize<StrategyDef>(file));
            foreach (var file in JsonFiles(Path.Combine(root, "commanders")))
                registry.Register(Deserialize<CommanderDef>(file));
            foreach (var file in JsonFiles(Path.Combine(root, "actions")))
            {
                var raw = Deserialize<ActionWeightsFile>(file);
                registry.Register(new WeightSet(raw.Name, raw.Weights));
            }

            return registry;
        }

        private sealed class ActionWeightsFile
        {
            public string Name { get; set; } = "";
            public Dictionary<string, float> Weights { get; set; } = new();
        }

        private static IEnumerable<string> JsonFiles(string dir)
            => Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "*.json")
                : System.Array.Empty<string>();

        private static T Deserialize<T>(string file)
        {
            var def = JsonConvert.DeserializeObject<T>(File.ReadAllText(file));
            return def ?? throw new InvalidDataException($"Failed to parse AI def: {file}");
        }
    }
}
