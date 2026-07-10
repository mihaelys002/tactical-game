using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TacticalGame.AI;
using TacticalGame.Grid;

namespace TacticalGame.Messages
{
    // Everything a condition may look at when deciding whether a speaker
    // reacts to a fact. Built once per observation pass, Speaker/Fact swapped
    // per candidate.
    public class MessageContext
    {
        public BattleState Battle { get; }
        public BattleMemory Memory { get; }
        public CampaignContext Meta { get; }
        public Random Rng { get; }
        public IReadOnlyList<UtilityAIPlanner> Planners { get; }

        public Unit Speaker { get; internal set; } = null!;
        public Fact Fact { get; internal set; } = null!;

        public MessageContext(BattleState battle, BattleMemory memory, CampaignContext meta,
            Random rng, IReadOnlyList<UtilityAIPlanner> planners)
        {
            Battle = battle;
            Memory = memory;
            Meta = meta;
            Rng = rng;
            Planners = planners;
        }

        public UnitAIState? AIStateOf(Unit unit)
        {
            foreach (var planner in Planners)
            {
                var state = planner.PeekState(unit);
                if (state != null) return state;
            }
            return null;
        }
    }

    public delegate bool MessageCondition(JToken value, MessageContext ctx);

    // Registry of per-message conditions (the "if" block of a rule).
    // Open set: register your own before loading rules that use them.
    public static class MessageConditions
    {
        private static readonly Dictionary<string, MessageCondition> All = new()
        {
            ["speakerIs"] = SpeakerIs,
            ["chance"] = (v, ctx) => ctx.Rng.NextDouble() < v.Value<double>(),
            ["hpBelow"] = (v, ctx) => HpRatio(ctx.Speaker) < v.Value<float>(),
            ["hpAbove"] = (v, ctx) => HpRatio(ctx.Speaker) > v.Value<float>(),
            ["amountAtLeast"] = (v, ctx) => ctx.Fact.Amount >= v.Value<float>(),
            ["tag"] = (v, ctx) => ctx.Fact.HasTag(v.Value<string>() ?? ""),
            ["targetIsSelf"] = (v, ctx) => (ctx.Fact.Target == ctx.Speaker) == v.Value<bool>(),
            ["grudgeAgainst"] = GrudgeAgainst,
            ["isWinning"] = (v, ctx) => (TeamHpRatio(ctx) >= 1.25f) == v.Value<bool>(),
            ["isLosing"] = (v, ctx) => (TeamHpRatio(ctx) <= 0.8f) == v.Value<bool>(),
            ["strategyIs"] = (v, ctx) => ctx.AIStateOf(ctx.Speaker)?.Strategy?.Name == v.Value<string>(),
            ["goalIs"] = (v, ctx) => ctx.AIStateOf(ctx.Speaker)?.Goal?.Def.Type == v.Value<string>(),
            ["saidNothingFor"] = (v, ctx) =>
                ctx.Fact.Turn - ctx.Memory.LastSpokeTurn(ctx.Speaker) >= v.Value<int>(),
            ["dataEquals"] = DataEquals,
        };

        public static bool Known(string name) => All.ContainsKey(name);

        public static void Register(string name, MessageCondition condition) => All[name] = condition;

        public static bool Evaluate(string name, JToken value, MessageContext ctx)
            => All.TryGetValue(name, out var condition) && condition(value, ctx);

        // ── Built-in implementations ──────────────────────────────────

        // Who may speak, relative to the fact: actor (default when the rule
        // has no speakerIs), target, allyOfActor, enemyOfActor, any.
        private static bool SpeakerIs(JToken value, MessageContext ctx)
        {
            var speaker = ctx.Speaker;
            var actor = ctx.Fact.Actor;
            return value.Value<string>() switch
            {
                "actor" => speaker == actor,
                "target" => speaker == ctx.Fact.Target,
                "allyOfActor" => actor != null && speaker != actor
                                 && speaker.TeamIndex == actor.TeamIndex,
                "enemyOfActor" => actor != null && speaker.TeamIndex != actor.TeamIndex,
                "any" => true,
                _ => false,
            };
        }

        // Speaker holds a grudge against the fact's "actor" or "target".
        private static bool GrudgeAgainst(JToken value, MessageContext ctx)
        {
            var who = value.Value<string>() == "target" ? ctx.Fact.Target : ctx.Fact.Actor;
            return who != null && ctx.Memory.HasGrudge(ctx.Speaker, who);
        }

        // Fact data entries match: "dataEquals": { "strategy": "Berserker" }
        private static bool DataEquals(JToken value, MessageContext ctx)
        {
            if (value is not JObject obj) return false;
            foreach (var prop in obj.Properties())
            {
                if (!ctx.Fact.Data.TryGetValue(prop.Name, out var actual)) return false;
                if (actual != prop.Value.Value<string>()) return false;
            }
            return true;
        }

        private static float HpRatio(Unit unit)
            => (float)unit.Stats.CurrentHP / unit.Stats.MaxHP;

        // Speaker team's total HP vs enemy total HP.
        private static float TeamHpRatio(MessageContext ctx)
        {
            float own = 0f, enemy = 0f;
            foreach (var u in ctx.Battle.Units)
            {
                if (!u.IsAlive) continue;
                if (u.TeamIndex == ctx.Speaker.TeamIndex) own += u.Stats.CurrentHP;
                else enemy += u.Stats.CurrentHP;
            }
            return enemy <= 0f ? float.MaxValue : own / enemy;
        }
    }

    public delegate bool MetaCondition(JToken value, CampaignContext meta);

    // Registry of pool-level conditions (the "requires" block) matched
    // against campaign state once at battle start.
    public static class MetaConditions
    {
        private static readonly Dictionary<string, MetaCondition> All = new()
        {
            ["factionPresent"] = (v, meta) => meta.Factions.Contains(v.Value<string>() ?? ""),
            ["chapterAtLeast"] = (v, meta) => meta.Chapter >= v.Value<int>(),
            ["chapterBelow"] = (v, meta) => meta.Chapter < v.Value<int>(),
            ["flag"] = (v, meta) => meta.Flags.Contains(v.Value<string>() ?? ""),
            ["notFlag"] = (v, meta) => !meta.Flags.Contains(v.Value<string>() ?? ""),
            ["battlesFoughtAtLeast"] = (v, meta) => meta.BattlesFought >= v.Value<int>(),
        };

        public static bool Known(string name) => All.ContainsKey(name);

        public static void Register(string name, MetaCondition condition) => All[name] = condition;

        public static bool Evaluate(string name, JToken value, CampaignContext meta)
            => All.TryGetValue(name, out var condition) && condition(value, meta);
    }
}
