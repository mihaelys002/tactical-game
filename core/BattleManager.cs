using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TacticalGame.AI;
using TacticalGame.Grid;

namespace TacticalGame
{
    public delegate AIAction? PlanAction(Unit unit, BattleState battle);

    public class BattleManager
    {
        private readonly BattleState _battle;
        private readonly PlanAction _planner;
        private readonly List<List<IBattleCommand>> _turnHistory = new();
        private int _turnNumber;
        private bool _useThreads;

        public BattleState Battle => _battle;
        public IReadOnlyList<List<Unit>> Teams => _battle.Teams;
        public int TeamCount => _battle.TeamCount;
        public int TurnNumber => _turnNumber;
        public bool UseThreads { get => _useThreads; set => _useThreads = value; }

        public event Action<string>? OnLog;

        public BattleManager(BattleState battle,
            PlanAction? planner = null, bool useThreads = true)
        {
            _planner = planner ?? DefaultPlanner();
            _useThreads = useThreads;
            _battle = battle;
        }

        // ─── Turn Resolution ──────────────────────────────────────────────
        //
        //  1. Plan   — every pending unit picks an action (parallel-safe)
        //  2. Execute — apply actions sequentially, collect failures
        //  3. Loop   — failed units replan until everyone acted
        //

        private const int SafetyLimit = 50;

        private const int FatigueRecoveryPerTurn = 15;

        public List<IBattleCommand> StepTurn()
        {
            _turnNumber++;

            // Recover fatigue at start of each turn
            foreach (var unit in GatherAliveUnits())
                _battle.ChangeFatigue(unit, -FatigueRecoveryPerTurn);

            var commands = new List<IBattleCommand>();
            var pending = GatherAliveUnits();
            int safety = 0;

            while (pending.Count > 0)
            {
                if (++safety > SafetyLimit) break;

                var planned = PlanAll(pending);
                pending = ExecuteAll(planned, commands);
            }

            _turnHistory.Add(commands);
            return commands;
        }

        public bool UndoLastTurn()
        {
            if (_turnHistory.Count == 0) return false;

            var commands = _turnHistory[^1];
            _turnHistory.RemoveAt(_turnHistory.Count - 1);

            for (int i = commands.Count - 1; i >= 0; i--)
                commands[i].Undo(_battle);

            _turnNumber--;
            return true;
        }

        public bool CanUndo => _turnHistory.Count > 0;

        // ─── Planning ─────────────────────────────────────────────────────

        private List<AIAction?> PlanAll(List<Unit> units)
        {
            if (_useThreads)
            {
                var results = new AIAction?[units.Count];
                Parallel.For(0, units.Count, i =>
                {
                    results[i] = _planner(units[i], _battle);
                });
                return new List<AIAction?>(results);
            }
            else
            {
                var results = new List<AIAction?>(units.Count);
                foreach (var unit in units)
                    results.Add(_planner(unit, _battle));
                return results;
            }
        }

        // ─── Execution ───────────────────────────────────────────────────

        private List<Unit> ExecuteAll(
            List<AIAction?> planned,
            List<IBattleCommand> commands)
        {
            var failed = new List<Unit>();

            foreach (var action in planned)
            {
                if (action == null) continue;
                if (!action.Unit.IsAlive) continue;

                var cmd = action.CreateCommand(_battle);
                if (!cmd.Execute(_battle))
                {
                    failed.Add(action.Unit);
                    continue;
                }

                commands.Add(cmd);
                Log("  " + cmd);
            }

            return failed;
        }

        // ─── Queries ──────────────────────────────────────────────────────

        public int GetTeamIndex(Unit unit) => _battle.GetTeamIndex(unit);

        public bool IsBattleOver()
        {
            int aliveTeams = 0;
            foreach (var team in _battle.Teams)
            {
                foreach (var u in team)
                {
                    if (u.IsAlive) { aliveTeams++; break; }
                }
            }
            return aliveTeams <= 1;
        }

        private List<Unit> GatherAliveUnits()
        {
            var alive = new List<Unit>();
            foreach (var team in _battle.Teams)
                foreach (var u in team)
                    if (u.IsAlive) alive.Add(u);
            return alive;
        }

        // ─── Default AI ──────────────────────────────────────────────────

        private static PlanAction DefaultPlanner()
        {
            return (unit, battle) =>
            {
                var bb = new AIBlackboard(battle, unit);
                return AIBrain.DecideAction(unit, bb);
            };
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
}
