using System;
using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.Messages
{
    // A remembered offense: Against hurt me or killed someone I cared about.
    public class Grudge
    {
        public Unit Against { get; }
        public string Reason { get; }
        public int Turn { get; }

        public Grudge(Unit against, string reason, int turn)
        {
            Against = against;
            Reason = reason;
            Turn = turn;
        }
    }

    // The engine's own battle-scoped memory: what it has observed, who holds
    // grudges, who said what. Reset per battle; never shared with the AI.
    public class BattleMemory
    {
        private readonly List<Fact> _timeline = new();
        private readonly Dictionary<Unit, List<Grudge>> _grudges = new();
        private readonly Dictionary<Unit, int> _lastSpoke = new();
        private readonly Dictionary<(Unit, string), int> _ruleLastSpoke = new();
        private readonly HashSet<string> _usedBattleRules = new();
        private readonly Dictionary<string, HashSet<int>> _usedVariants = new();

        // Alive units at last observation — how deaths are detected.
        internal HashSet<Unit> KnownAlive { get; } = new();

        // Team sizes at battle start (before anyone died).
        internal Dictionary<int, int> InitialTeamCounts { get; } = new();

        // How many TurnHistory entries have been turned into facts already.
        internal int ProcessedTurns { get; set; }

        public IReadOnlyList<Fact> Timeline => _timeline;

        public void AddFacts(IEnumerable<Fact> facts) => _timeline.AddRange(facts);

        public void Prune(int currentTurn, int window)
            => _timeline.RemoveAll(f => f.Turn < currentTurn - window);

        // ── Grudges ───────────────────────────────────────────────────

        public void AddGrudge(Unit holder, Unit against, string reason, int turn)
        {
            if (holder == against) return;
            if (!_grudges.TryGetValue(holder, out var list))
                _grudges[holder] = list = new List<Grudge>();
            list.Add(new Grudge(against, reason, turn));
        }

        public bool HasGrudge(Unit holder, Unit against)
        {
            if (!_grudges.TryGetValue(holder, out var list)) return false;
            foreach (var g in list)
                if (g.Against == against) return true;
            return false;
        }

        public IReadOnlyList<Grudge> GrudgesOf(Unit holder)
            => _grudges.TryGetValue(holder, out var list) ? list : Array.Empty<Grudge>();

        // ── Speech bookkeeping ────────────────────────────────────────

        public int LastSpokeTurn(Unit unit)
            => _lastSpoke.TryGetValue(unit, out var t) ? t : int.MinValue;

        public bool CooldownOk(Unit unit, string ruleId, int turn, int cooldown)
            => !_ruleLastSpoke.TryGetValue((unit, ruleId), out var last)
               || turn - last >= cooldown;

        public bool IsRuleUsedThisBattle(string ruleId) => _usedBattleRules.Contains(ruleId);

        public void MarkSpoke(Unit unit, string ruleId, int turn, bool onceBattle)
        {
            _lastSpoke[unit] = turn;
            _ruleLastSpoke[(unit, ruleId)] = turn;
            if (onceBattle) _usedBattleRules.Add(ruleId);
        }

        // Random pick among a rule's unheard variants; resets when exhausted.
        public int NextVariant(string ruleId, int count, Random rng)
        {
            if (!_usedVariants.TryGetValue(ruleId, out var used))
                _usedVariants[ruleId] = used = new HashSet<int>();
            if (used.Count >= count) used.Clear();

            var available = new List<int>();
            for (int i = 0; i < count; i++)
                if (!used.Contains(i)) available.Add(i);

            int pick = available[rng.Next(available.Count)];
            used.Add(pick);
            return pick;
        }
    }
}
