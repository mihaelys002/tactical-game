using System.Collections.Generic;

namespace TacticalGame.AI.Utility
{
    // Flyweight: one WeightSet instance shared by every unit that uses the
    // same resource file. Immutable after load — safe to read from any thread.
    public class WeightSet
    {
        public string Name { get; }
        private readonly Dictionary<string, float> _weights;

        public WeightSet(string name, Dictionary<string, float> weights)
        {
            Name = name;
            _weights = weights;
        }

        public float Get(string key, float fallback = 0f)
            => _weights.TryGetValue(key, out var w) ? w : fallback;

        public bool Has(string key) => _weights.ContainsKey(key);
    }
}
