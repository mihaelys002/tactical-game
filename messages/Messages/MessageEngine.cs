using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TacticalGame.AI;
using TacticalGame.Grid;

namespace TacticalGame.Messages
{
    // A spoken line, ready for display (floating text, battle log, console).
    public class MessageLine
    {
        public Unit Speaker { get; }
        public string Text { get; }
        public int Turn { get; }
        public string RuleId { get; }
        public IReadOnlyList<string> Tags { get; }

        public MessageLine(Unit speaker, string text, int turn, string ruleId, IReadOnlyList<string> tags)
        {
            Speaker = speaker;
            Text = text;
            Turn = turn;
            RuleId = ruleId;
            Tags = tags;
        }

        public override string ToString()
            => string.Create(CultureInfo.InvariantCulture, $"T{Turn} {Speaker}: \"{Text}\"");
    }

    // Text-only banter engine. A pure observer: after each executed turn it
    // reads what already exists (TurnHistory, planner introspection, battle
    // state), derives facts, matches authored rules against them, and speaks.
    // Game logic and AI never know it exists.
    //
    //   var engine = new MessageEngine(registry, meta, campaignMemory, seed: 42,
    //       planners: new[] { orcPlanner, goblinPlanner });
    //   engine.SetVoice(orcUnit, "Orc");
    //   engine.BeginBattle(battle);
    //   ... after every StepTurn():          // safe from a background thread
    //   engine.ObserveTurn(battle);
    //   foreach (var line in engine.DrainLines()) Show(line);
    public class MessageEngine
    {
        private readonly MessageRegistry _registry;
        private readonly CampaignContext _meta;
        private readonly CampaignMemory _campaign;
        private readonly Random _rng;
        private readonly IReadOnlyList<UtilityAIPlanner> _planners;

        private readonly object _lock = new();
        private readonly Dictionary<Unit, string> _voices = new();
        private readonly Queue<MessageLine> _lines = new();
        private readonly List<Fact> _pendingSaid = new();
        private readonly List<IFactSource> _extraSources = new();

        private List<IFactSource> _sources = new();
        private List<MessagePool> _activePools = new();
        private BattleMemory _memory = new();

        public int MaxLinesPerTurn { get; set; } = 3;
        public int TimelineWindow { get; set; } = 6;

        public BattleMemory Memory => _memory;
        public CampaignMemory Campaign => _campaign;

        public MessageEngine(MessageRegistry registry, CampaignContext? meta = null,
            CampaignMemory? campaignMemory = null, int seed = 0,
            IReadOnlyList<UtilityAIPlanner>? planners = null)
        {
            _registry = registry;
            _meta = meta ?? new CampaignContext();
            _campaign = campaignMemory ?? new CampaignMemory();
            _rng = new Random(seed);
            _planners = planners ?? Array.Empty<UtilityAIPlanner>();
        }

        public void SetVoice(Unit unit, string voice)
        {
            lock (_lock) _voices[unit] = voice;
        }

        // Custom sources survive BeginBattle — added on top of the built-ins.
        public void AddFactSource(IFactSource source)
        {
            lock (_lock) _extraSources.Add(source);
        }

        // Resets battle memory, selects the active pools for this battle's
        // meta situation, and snapshots who is alive.
        public void BeginBattle(BattleState battle)
        {
            lock (_lock)
            {
                _memory = new BattleMemory();
                _memory.ProcessedTurns = battle.TurnHistory.Count;
                foreach (var u in battle.Units)
                {
                    if (!u.IsAlive) continue;
                    _memory.KnownAlive.Add(u);
                    if (u.TeamIndex >= 0)
                        _memory.InitialTeamCounts[u.TeamIndex] =
                            _memory.InitialTeamCounts.TryGetValue(u.TeamIndex, out var n) ? n + 1 : 1;
                }

                _activePools = _registry.ActivePools(_meta);

                _sources = new List<IFactSource> { new HistoryFactSource(), new SituationFactSource() };
                if (_planners.Count > 0) _sources.Add(new AIStateFactSource(_planners));
                _sources.AddRange(_extraSources);

                _pendingSaid.Clear();
                _lines.Clear();
            }
        }

        // Call after each executed turn (state must be stable while it runs).
        public void ObserveTurn(BattleState battle)
        {
            lock (_lock)
            {
                int turn = battle.TurnNumber;

                var fresh = new List<Fact>(_pendingSaid);
                _pendingSaid.Clear();
                foreach (var source in _sources)
                    fresh.AddRange(source.Extract(battle, _memory));

                DeriveSecondary(fresh, battle);
                _memory.AddFacts(fresh);

                var ctx = new MessageContext(battle, _memory, _meta, _rng, _planners);
                var winners = Arbitrate(Match(fresh, battle, ctx), turn);
                foreach (var (speaker, rule, fact) in winners)
                    Speak(speaker, rule, fact, turn);

                _memory.Prune(turn, TimelineWindow);
            }
        }

        public List<MessageLine> DrainLines()
        {
            lock (_lock)
            {
                var drained = new List<MessageLine>(_lines);
                _lines.Clear();
                return drained;
            }
        }

        // ── Derived facts: fan-outs and grudges ───────────────────────

        private void DeriveSecondary(List<Fact> fresh, BattleState battle)
        {
            var primary = fresh.ToArray();
            foreach (var fact in primary)
            {
                if (fact.Type == FactTypes.Killed && fact.Target != null)
                {
                    foreach (var mate in battle.Units)
                    {
                        if (!mate.IsAlive || mate == fact.Target
                            || mate.TeamIndex != fact.Target.TeamIndex) continue;

                        fresh.Add(new Fact(FactTypes.AllyDied, fact.Turn,
                            actor: mate, target: fact.Target,
                            data: new Dictionary<string, string>
                            {
                                ["ally"] = fact.Target.ToString(),
                                ["killer"] = fact.Actor?.ToString() ?? "someone",
                            }));

                        if (fact.Actor != null)
                            _memory.AddGrudge(mate, fact.Actor,
                                "killed " + fact.Target, fact.Turn);
                    }
                }
                else if (fact.Type == FactTypes.Hurt && fact.Actor != null && fact.Target != null
                         && fact.Amount >= 0.25f * fact.Target.Stats.MaxHP)
                {
                    _memory.AddGrudge(fact.Target, fact.Actor, "wounded me", fact.Turn);
                }
            }
        }

        // ── Matching: fact × speaker × rule ───────────────────────────

        private List<(Unit, MessageRule, Fact)> Match(List<Fact> fresh, BattleState battle, MessageContext ctx)
        {
            var candidates = new List<(Unit, MessageRule, Fact)>();

            foreach (var fact in fresh)
            {
                foreach (var speaker in battle.Units)
                {
                    if (!speaker.IsAlive) continue;
                    ctx.Speaker = speaker;
                    ctx.Fact = fact;

                    foreach (var pool in _activePools)
                    {
                        if (pool.Voice.Length > 0 && pool.Voice != VoiceOf(speaker)) continue;

                        foreach (var rule in pool.Rules)
                        {
                            if (rule.When != fact.Type) continue;
                            if (!Passes(rule, ctx)) continue;
                            candidates.Add((speaker, rule, fact));
                        }
                    }
                }
            }
            return candidates;
        }

        private bool Passes(MessageRule rule, MessageContext ctx)
        {
            if (rule.Once == "campaign" && _campaign.IsUsed(rule.Id)) return false;
            if (rule.Once == "battle" && _memory.IsRuleUsedThisBattle(rule.Id)) return false;
            if (!_memory.CooldownOk(ctx.Speaker, rule.Id, ctx.Fact.Turn, rule.Cooldown)) return false;

            // Without an explicit speakerIs, only the fact's actor reacts.
            if (!rule.If.ContainsKey("speakerIs") && ctx.Speaker != ctx.Fact.Actor) return false;

            foreach (var (name, value) in rule.If)
                if (!MessageConditions.Evaluate(name, value, ctx)) return false;
            return true;
        }

        // Priority wins; one line per speaker and per rule per turn; budget caps chatter.
        private List<(Unit, MessageRule, Fact)> Arbitrate(
            List<(Unit Speaker, MessageRule Rule, Fact Fact)> candidates, int turn)
        {
            candidates.Sort((a, b) => b.Rule.Priority.CompareTo(a.Rule.Priority));

            var winners = new List<(Unit, MessageRule, Fact)>();
            var speakers = new HashSet<Unit>();
            var rules = new HashSet<string>();

            foreach (var c in candidates)
            {
                if (winners.Count >= MaxLinesPerTurn) break;
                if (speakers.Contains(c.Speaker) || rules.Contains(c.Rule.Id)) continue;
                speakers.Add(c.Speaker);
                rules.Add(c.Rule.Id);
                winners.Add(c);
            }
            return winners;
        }

        // ── Speaking ──────────────────────────────────────────────────

        private void Speak(Unit speaker, MessageRule rule, Fact fact, int turn)
        {
            int variant = _memory.NextVariant(rule.Id, rule.Say.Count, _rng);
            string text = Substitute(rule.Say[variant], speaker, fact);

            _memory.MarkSpoke(speaker, rule.Id, turn, onceBattle: rule.Once == "battle");
            if (rule.Once == "campaign") _campaign.MarkUsed(rule.Id);

            _lines.Enqueue(new MessageLine(speaker, text, turn, rule.Id, rule.Tags));

            // What was said becomes a fact — others can answer next turn.
            _pendingSaid.Add(new Fact(FactTypes.Said, turn, actor: speaker,
                target: fact.Actor == speaker ? fact.Target : fact.Actor,
                data: new Dictionary<string, string> { ["text"] = text },
                tags: rule.Tags));
        }

        private static string Substitute(string template, Unit speaker, Fact fact)
        {
            var sb = new StringBuilder(template);
            sb.Replace("{unit}", speaker.ToString());
            if (fact.Actor != null) sb.Replace("{actor}", fact.Actor.ToString());
            if (fact.Target != null) sb.Replace("{target}", fact.Target.ToString());
            sb.Replace("{amount}", ((int)fact.Amount).ToString(CultureInfo.InvariantCulture));
            foreach (var (key, value) in fact.Data)
                sb.Replace("{" + key + "}", value);
            return sb.ToString();
        }

        private string VoiceOf(Unit unit)
            => _voices.TryGetValue(unit, out var voice) ? voice : "";
    }
}
