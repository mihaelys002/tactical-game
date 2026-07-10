using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TacticalGame.Messages
{
    // One authorable message: react to a fact type, gated by flat conditions,
    // pick one of the say-variants.
    //
    //   { "when": "Killed",
    //     "if": { "chance": 0.6 },
    //     "say": ["{victim} was weak!", "MORE!"],
    //     "tags": ["taunt"], "priority": 5, "cooldown": 3, "once": "battle" }
    public class MessageRule
    {
        public string Id { get; internal set; } = "";
        public string When { get; set; } = "";
        public Dictionary<string, JToken> If { get; set; } = new();
        public List<string> Say { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public int Priority { get; set; }
        public int Cooldown { get; set; } = 2;
        public string Once { get; set; } = "none"; // none | battle | campaign
    }

    // A themed set of rules, active only when its meta requirements hold.
    // Voice scopes the pool to units assigned that voice; empty = everyone.
    public class MessagePool
    {
        public string Pool { get; set; } = "";
        public string Voice { get; set; } = "";
        public Dictionary<string, JToken> Requires { get; set; } = new();
        public List<MessageRule> Rules { get; set; } = new();

        public bool IsActive(CampaignContext meta)
        {
            foreach (var (name, value) in Requires)
                if (!MetaConditions.Evaluate(name, value, meta))
                    return false;
            return true;
        }
    }

    // Flyweight registry for message pools. Load once, read-only afterwards.
    // Unknown condition names fail loudly at load — a typo in a JSON must
    // never become a silently-dead message.
    public class MessageRegistry
    {
        private readonly List<MessagePool> _pools = new();

        public IReadOnlyList<MessagePool> Pools => _pools;

        public void Register(MessagePool pool)
        {
            Validate(pool);
            for (int i = 0; i < pool.Rules.Count; i++)
                pool.Rules[i].Id = pool.Pool + ":" + pool.Rules[i].When + ":" +
                    i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _pools.Add(pool);
        }

        public List<MessagePool> ActivePools(CampaignContext meta)
        {
            var active = new List<MessagePool>();
            foreach (var pool in _pools)
                if (pool.IsActive(meta)) active.Add(pool);
            return active;
        }

        // Loads Resources/banter/*.json from a root dir.
        public static MessageRegistry LoadFromDirectory(string root)
        {
            var registry = new MessageRegistry();
            var dir = Path.Combine(root, "banter");
            if (!Directory.Exists(dir)) return registry;

            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                var pool = JsonConvert.DeserializeObject<MessagePool>(File.ReadAllText(file))
                    ?? throw new InvalidDataException($"Failed to parse message pool: {file}");
                registry.Register(pool);
            }
            return registry;
        }

        private static void Validate(MessagePool pool)
        {
            if (string.IsNullOrEmpty(pool.Pool))
                throw new InvalidDataException("Message pool has no name");

            foreach (var (name, _) in pool.Requires)
                if (!MetaConditions.Known(name))
                    throw new InvalidDataException(
                        $"Pool '{pool.Pool}': unknown requires condition '{name}'");

            foreach (var rule in pool.Rules)
            {
                if (string.IsNullOrEmpty(rule.When))
                    throw new InvalidDataException($"Pool '{pool.Pool}': rule missing 'when'");
                if (rule.Say.Count == 0)
                    throw new InvalidDataException(
                        $"Pool '{pool.Pool}': rule '{rule.When}' has nothing to say");
                foreach (var (name, _) in rule.If)
                    if (!MessageConditions.Known(name))
                        throw new InvalidDataException(
                            $"Pool '{pool.Pool}': unknown condition '{name}' in '{rule.When}' rule");
            }
        }
    }
}
