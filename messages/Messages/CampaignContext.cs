using System.Collections.Generic;
using Newtonsoft.Json;

namespace TacticalGame.Messages
{
    // Meta-gameplay snapshot handed to the engine at construction. Filled by
    // whoever owns campaign state; the engine never queries the game for it.
    public class CampaignContext
    {
        public HashSet<string> Factions { get; } = new();
        public HashSet<string> Flags { get; } = new();
        public int Chapter { get; set; } = 1;
        public int BattlesFought { get; set; }
    }

    // Cross-battle memory: which once-per-campaign lines are burned.
    // Serialize with the campaign save via ToJson/FromJson.
    public class CampaignMemory
    {
        private readonly HashSet<string> _usedRules = new();

        public bool IsUsed(string ruleId) => _usedRules.Contains(ruleId);
        public void MarkUsed(string ruleId) => _usedRules.Add(ruleId);

        public string ToJson() => JsonConvert.SerializeObject(_usedRules);

        public static CampaignMemory FromJson(string json)
        {
            var memory = new CampaignMemory();
            var used = JsonConvert.DeserializeObject<HashSet<string>>(json);
            if (used != null)
                foreach (var id in used) memory._usedRules.Add(id);
            return memory;
        }
    }
}
