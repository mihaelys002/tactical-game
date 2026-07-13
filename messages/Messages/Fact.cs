using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.Messages
{
    // Fact types produced by the built-in fact sources. The set is open:
    // custom IFactSource implementations may emit their own type strings.
    public static class FactTypes
    {
        public const string Hurt = "Hurt";                          // Actor damaged Target (Amount = applied dmg)
        public const string Killed = "Killed";                      // Target died, Actor = last damager (may be null)
        public const string AllyDied = "AllyDied";                  // Actor lost teammate Target
        public const string Moved = "Moved";                        // Actor moved (data: from, to)
        public const string SkillUsed = "SkillUsed";                // Actor used a damaging skill (data: skill)
        public const string StrategyAssigned = "StrategyAssigned";  // commander gave Actor a strategy (data: strategy)
        public const string GoalChanged = "GoalChanged";            // Actor's goal changed (data: goal, goalType)
        public const string Disobeyed = "Disobeyed";                // Actor overruled commander (data: commanderWanted, unitChose)
        public const string LastAlive = "LastAlive";                // Actor is the last of its team
        public const string Outnumbered = "Outnumbered";            // Actor's team dropped to half the enemy count
        public const string Said = "Said";                          // Actor spoke a line (data: text; tags from the rule)
    }

    // Universal observation: something that happened on the battlefield,
    // derived by fact sources from state/history — never emitted by game logic.
    public class Fact
    {
        private static readonly IReadOnlyList<string> NoTags = new List<string>();
        private static readonly IReadOnlyDictionary<string, string> NoData = new Dictionary<string, string>();

        public string Type { get; }
        public int Turn { get; }
        public Unit? Actor { get; }
        public Unit? Target { get; }
        public float Amount { get; }
        public IReadOnlyList<string> Tags { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public Fact(string type, int turn, Unit? actor = null, Unit? target = null,
            float amount = 0f, IReadOnlyDictionary<string, string>? data = null,
            IReadOnlyList<string>? tags = null)
        {
            Type = type;
            Turn = turn;
            Actor = actor;
            Target = target;
            Amount = amount;
            Data = data ?? NoData;
            Tags = tags ?? NoTags;
        }

        public bool HasTag(string tag)
        {
            foreach (var t in Tags)
                if (t == tag) return true;
            return false;
        }

        public override string ToString()
            => string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"T{Turn} {Type} {Actor?.ToString() ?? "-"} -> {Target?.ToString() ?? "-"}");
    }
}
