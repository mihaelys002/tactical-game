using System.Collections.Generic;
using TacticalGame.AI;
using TacticalGame.Grid;

namespace TacticalGame.Messages
{
    // A spectator's eye: derives facts by observing state the game already
    // exposes for other reasons. Sources are stateful per battle — the engine
    // creates fresh instances in BeginBattle.
    public interface IFactSource
    {
        List<Fact> Extract(BattleState battle, BattleMemory memory);
    }

    // Reads executed commands from BattleState.TurnHistory — the same record
    // the visualization replays. Exact damage/kill attribution, no diffing.
    public class HistoryFactSource : IFactSource
    {
        public List<Fact> Extract(BattleState battle, BattleMemory memory)
        {
            var facts = new List<Fact>();
            var lastDamager = new Dictionary<Unit, Unit>();
            var history = battle.TurnHistory;

            for (int t = memory.ProcessedTurns; t < history.Count; t++)
            {
                int turn = t + 1;
                foreach (var cmd in history[t])
                {
                    if (cmd is MoveCommand move)
                    {
                        facts.Add(new Fact(FactTypes.Moved, turn, actor: move.Unit,
                            data: new Dictionary<string, string>
                            { ["from"] = move.From.ToString(), ["to"] = move.To.ToString() }));
                    }
                    else if (cmd is CompoundCommand compound)
                    {
                        bool hasDamage = false;
                        foreach (var effect in compound.Effects)
                        {
                            if (effect is not DamageEffect dmg) continue;
                            int applied = dmg.AppliedHpDamage + dmg.AppliedArmorDamage;
                            if (applied <= 0) continue;

                            hasDamage = true;
                            lastDamager[dmg.Target] = dmg.Source;
                            facts.Add(new Fact(FactTypes.Hurt, turn,
                                actor: dmg.Source, target: dmg.Target, amount: applied,
                                data: new Dictionary<string, string>
                                {
                                    ["attacker"] = dmg.Source.ToString(),
                                    ["victim"] = dmg.Target.ToString(),
                                }));
                        }

                        // Recovery and self-buffs are not conversation-worthy.
                        if (hasDamage)
                            facts.Add(new Fact(FactTypes.SkillUsed, turn, actor: compound.Unit,
                                data: new Dictionary<string, string> { ["skill"] = compound.Skill.Name }));
                    }
                }
            }
            memory.ProcessedTurns = history.Count;

            // Deaths: anyone we knew alive who isn't anymore.
            var died = new List<Unit>();
            foreach (var u in memory.KnownAlive)
                if (!u.IsAlive) died.Add(u);

            foreach (var victim in died)
            {
                memory.KnownAlive.Remove(victim);
                lastDamager.TryGetValue(victim, out var killer);
                facts.Add(new Fact(FactTypes.Killed, battle.TurnNumber,
                    actor: killer, target: victim,
                    data: new Dictionary<string, string>
                    {
                        ["victim"] = victim.ToString(),
                        ["killer"] = killer?.ToString() ?? "someone",
                    }));
            }

            return facts;
        }
    }

    // Reads AI intent through the planner's public introspection surface
    // (PeekState, DisobedienceSnapshot) — the planner never knows it's read.
    public class AIStateFactSource : IFactSource
    {
        private readonly IReadOnlyList<UtilityAIPlanner> _planners;
        private readonly Dictionary<Unit, string> _lastStrategy = new();
        private readonly Dictionary<Unit, string> _lastGoal = new();
        private readonly Dictionary<UtilityAIPlanner, int> _seenDisobedience = new();

        public AIStateFactSource(IReadOnlyList<UtilityAIPlanner> planners)
        {
            _planners = planners;
        }

        public List<Fact> Extract(BattleState battle, BattleMemory memory)
        {
            var facts = new List<Fact>();
            int turn = battle.TurnNumber;

            foreach (var unit in battle.Units)
            {
                if (!unit.IsAlive) continue;
                var state = PeekState(unit);
                if (state == null) continue;

                var strategy = state.Strategy?.Name;
                if (strategy != null && (_lastStrategy.TryGetValue(unit, out var prevS) ? prevS : null) != strategy)
                {
                    _lastStrategy[unit] = strategy;
                    facts.Add(new Fact(FactTypes.StrategyAssigned, turn, actor: unit,
                        data: new Dictionary<string, string> { ["strategy"] = strategy }));
                }

                var goal = state.Goal?.Label;
                if (goal != null && (_lastGoal.TryGetValue(unit, out var prevG) ? prevG : null) != goal)
                {
                    _lastGoal[unit] = goal;
                    facts.Add(new Fact(FactTypes.GoalChanged, turn, actor: unit,
                        target: state.Goal!.TargetUnit,
                        data: new Dictionary<string, string>
                        {
                            ["goal"] = goal,
                            ["goalType"] = state.Goal.Def.Type,
                        }));
                }
            }

            foreach (var planner in _planners)
            {
                var events = planner.DisobedienceSnapshot();
                int seen = _seenDisobedience.TryGetValue(planner, out var s) ? s : 0;
                for (int i = seen; i < events.Count; i++)
                {
                    var evt = events[i];
                    facts.Add(new Fact(FactTypes.Disobeyed, turn, actor: evt.Unit,
                        amount: evt.DesireGap,
                        data: new Dictionary<string, string>
                        {
                            ["commanderWanted"] = evt.CommanderWanted,
                            ["unitChose"] = evt.UnitChose,
                        }));
                }
                _seenDisobedience[planner] = events.Count;
            }

            return facts;
        }

        private UnitAIState? PeekState(Unit unit)
        {
            foreach (var planner in _planners)
            {
                var state = planner.PeekState(unit);
                if (state != null) return state;
            }
            return null;
        }
    }

    // Team-level situation transitions: outnumbered, last one standing.
    public class SituationFactSource : IFactSource
    {
        private readonly HashSet<int> _outnumberedEmitted = new();
        private readonly HashSet<int> _lastAliveEmitted = new();

        public List<Fact> Extract(BattleState battle, BattleMemory memory)
        {
            var facts = new List<Fact>();
            int turn = battle.TurnNumber;

            var alive = new Dictionary<int, List<Unit>>();
            int totalAlive = 0;
            foreach (var u in battle.Units)
            {
                if (!u.IsAlive || u.TeamIndex < 0) continue;
                if (!alive.TryGetValue(u.TeamIndex, out var list))
                    alive[u.TeamIndex] = list = new List<Unit>();
                list.Add(u);
                totalAlive++;
            }

            foreach (var (team, units) in alive)
            {
                int enemies = totalAlive - units.Count;

                if (units.Count * 2 <= enemies && _outnumberedEmitted.Add(team))
                    foreach (var u in units)
                        facts.Add(new Fact(FactTypes.Outnumbered, turn, actor: u));

                if (units.Count == 1 && memory.InitialTeamCounts.TryGetValue(team, out var initial)
                    && initial > 1 && _lastAliveEmitted.Add(team))
                    facts.Add(new Fact(FactTypes.LastAlive, turn, actor: units[0]));
            }

            return facts;
        }
    }
}
