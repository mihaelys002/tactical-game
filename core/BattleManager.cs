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
        private bool _useThreads;

        public BattleState Battle => _battle;
        public int TurnNumber => _battle.TurnNumber;
        public bool UseThreads { get => _useThreads; set => _useThreads = value; }

        private Action<string>? _onLog;
        public event Action<string>? OnLog
        {
            add => _onLog += value;
            remove => _onLog -= value;
        }

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

        public List<IBattleCommand> StepTurn()
        {
            _battle.AdvanceTurn();

            var commands = new List<IBattleCommand>();
            var pending = GatherAliveUnits();

            // Recovery at start of turn (skip turn 1)
            if (_battle.TurnNumber > 1)
                foreach (var unit in pending)
                {
                    var recovery = CombatPipeline.ResolveRecovery(unit);
                    recovery.Execute(_battle);
                    commands.Add(recovery);
                }

            int safety = 0;

            while (pending.Count > 0)
            {
                if (++safety > SafetyLimit) break;

                var planned = PlanAll(pending);
                pending = ExecuteAll(planned, commands);
            }

            _battle.RecordTurn(commands);
            return commands;
        }

        public bool UndoLastTurn()
        {
            var commands = _battle.PopLastTurn();
            if (commands == null) return false;

            for (int i = commands.Count - 1; i >= 0; i--)
                commands[i].Undo(_battle);

            _battle.RewindTurn();
            return true;
        }

        public bool CanUndo => _battle.TurnHistory.Count > 0;

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

        public bool IsBattleOver()
        {
            var aliveTeams = new HashSet<int>();
            foreach (var u in _battle.Units)
                if (u.IsAlive && u.TeamIndex >= 0)
                    aliveTeams.Add(u.TeamIndex);
            return aliveTeams.Count <= 1;
        }

        private List<Unit> GatherAliveUnits()
        {
            var alive = new List<Unit>();
            foreach (var u in _battle.Units)
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
            _onLog?.Invoke(message);
        }
    }
}
