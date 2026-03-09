using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TacticalGame.AI;
using TacticalGame.Grid;

namespace TacticalGame.Prototype
{
    /// <summary>
    /// Planning produces an action for each unit.
    /// Execution does not care how actions were produced — only what they are.
    /// Replace this delegate to swap AI without touching the execution loop.
    /// </summary>
    public delegate AIAction? PlanAction(Unit unit, BattleState battle, List<Unit> allies, List<Unit> enemies);

    public class BattleManager
    {
        private readonly BattleState _battle;
        private readonly List<List<Unit>> _teams = new();
        private readonly Dictionary<Unit, int> _unitTeam = new();
        private readonly PlanAction _planner;
        private readonly List<List<IBattleCommand>> _turnHistory = new();
        private int _turnNumber;
        private bool _useThreads;

        public BattleState Battle => _battle;
        public IReadOnlyList<List<Unit>> Teams => _teams;
        public int TeamCount => _teams.Count;
        public int TurnNumber => _turnNumber;
        public bool UseThreads { get => _useThreads; set => _useThreads = value; }

        public event Action<string>? OnLog;

        public BattleManager(PlanAction? planner = null, bool useThreads = true)
        {
            _planner = planner ?? DefaultPlanner();
            _useThreads = useThreads;
            _battle = new BattleState(CreateGrid());
            SpawnUnits();
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
            _turnNumber++;

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
        //
        //  Calls _planner for each unit. Threaded or not — same result.
        //  Planning only READS battle state. Never mutates.
        //

        private List<(Unit unit, AIAction? action)> PlanAll(List<Unit> units)
        {
            var enemyCache = BuildEnemyCache();

            if (_useThreads)
            {
                var results = new (Unit unit, AIAction? action)[units.Count];
                Parallel.For(0, units.Count, i =>
                {
                    var unit = units[i];
                    int team = _unitTeam[unit];
                    results[i] = (unit, _planner(unit, _battle, _teams[team], enemyCache[team]));
                });
                return new List<(Unit, AIAction?)>(results);
            }
            else
            {
                var results = new List<(Unit, AIAction?)>(units.Count);
                foreach (var unit in units)
                {
                    int team = _unitTeam[unit];
                    results.Add((unit, _planner(unit, _battle, _teams[team], enemyCache[team])));
                }
                return results;
            }
        }

        private Dictionary<int, List<Unit>> BuildEnemyCache()
        {
            var cache = new Dictionary<int, List<Unit>>(_teams.Count);
            for (int t = 0; t < _teams.Count; t++)
            {
                var enemies = new List<Unit>();
                for (int other = 0; other < _teams.Count; other++)
                {
                    if (other == t) continue;
                    foreach (var u in _teams[other])
                        if (u.IsAlive) enemies.Add(u);
                }
                cache[t] = enemies;
            }
            return cache;
        }

        // ─── Execution ───────────────────────────────────────────────────
        //
        //  Applies actions one by one. Immediate effects.
        //  If an action fails (target dead, cell occupied), unit goes to failed.
        //  This method does NOT know about AI. Only AIActionType + targets.
        //

        private List<Unit> ExecuteAll(
            List<(Unit unit, AIAction? action)> planned,
            List<IBattleCommand> commands)
        {
            var failed = new List<Unit>();
            var claimed = new HashSet<HexCoord>();

            foreach (var (unit, action) in planned)
            {
                if (action == null) continue;
                if (!unit.IsAlive) continue;

                switch (action.Type)
                {
                    case AIActionType.Move:
                    {
                        var cell = _battle.Grid.GetCell(action.Target);
                        bool blocked = cell == null
                            || !cell.IsWalkable
                            || claimed.Contains(action.Target);

                        if (blocked)
                        {
                            failed.Add(unit);
                            break;
                        }

                        claimed.Add(action.Target);
                        var cmd = new MoveCommand(unit, unit.Position, action.Target);
                        cmd.Execute(_battle);
                        commands.Add(cmd);
                        break;
                    }

                    case AIActionType.Attack:
                    {
                        bool invalid = action.TargetUnit == null
                            || !action.TargetUnit.IsAlive;

                        if (invalid)
                        {
                            failed.Add(unit);
                            break;
                        }

                        var cmd = new AttackCommand(unit, action.TargetUnit!, unit.Stats.Attack);
                        cmd.Execute(_battle);
                        commands.Add(cmd);
                        break;
                    }
                }
            }

            return failed;
        }

        // ─── Queries ──────────────────────────────────────────────────────

        public int GetTeamIndex(Unit unit)
        {
            return _unitTeam.TryGetValue(unit, out int idx) ? idx : -1;
        }

        public bool IsBattleOver()
        {
            int aliveTeams = 0;
            foreach (var team in _teams)
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
            foreach (var team in _teams)
                foreach (var u in team)
                    if (u.IsAlive) alive.Add(u);
            return alive;
        }

        // ─── Default AI ──────────────────────────────────────────────────
        //
        //  Simple utility AI. Replace via constructor parameter.
        //

        private static PlanAction DefaultPlanner()
        {
            var brain = new AIBrain();
            return (unit, battle, allies, enemies) =>
            {
                var bb = new AIBlackboard(battle, allies, enemies);
                return brain.DecideAction(unit, bb);
            };
        }

        // ─── Setup (prototype only) ──────────────────────────────────────

        private HexGrid CreateGrid()
        {
            var grid = new HexGrid();
            int radius = 7;

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Math.Max(-radius, -q - radius);
                int r2 = Math.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    var terrain = TerrainType.Plain;
                    int hash = q * 7 + r * 13;
                    if (hash % 5 == 0) terrain = TerrainType.Forest;
                    if (hash % 7 == 0) terrain = TerrainType.Hill;

                    grid.AddCell(new HexCoord(q, r), terrain, terrain == TerrainType.Hill ? 1 : 0);
                }
            }

            return grid;
        }

        private void SpawnUnits()
        {
            AddTeam(0, new HexCoord(-6, 0), new HexCoord(-6, 1), new HexCoord(-6, 2),
                       new HexCoord(-6, 3), new HexCoord(-6, 4), new HexCoord(-6, 5));
            AddTeam(1, new HexCoord(6, 0), new HexCoord(6, -1), new HexCoord(6, -2),
                       new HexCoord(6, -3), new HexCoord(6, -4), new HexCoord(6, -5));
            AddTeam(2, new HexCoord(0, -6), new HexCoord(1, -6), new HexCoord(2, -6),
                       new HexCoord(3, -6), new HexCoord(4, -6), new HexCoord(5, -6));
            AddTeam(3, new HexCoord(0, 6), new HexCoord(-1, 6), new HexCoord(-2, 6),
                       new HexCoord(-3, 6), new HexCoord(-4, 6), new HexCoord(-5, 6));
        }

        private void AddTeam(int teamIndex, params HexCoord[] positions)
        {
            var team = new List<Unit>();
            _teams.Add(team);
            var rng = new Random(teamIndex * 1000);
            foreach (var pos in positions)
            {
                var stats = UnitStats.Fresh(
                    rng.Next(11, 19), rng.Next(8, 16), rng.Next(35, 60),
                    rng.Next(65, 95), rng.Next(25, 50), rng.Next(45, 70), rng.Next(55, 75));
                var unit = new Unit(stats);
                _battle.PlaceUnit(unit, pos);
                team.Add(unit);
                _unitTeam[unit] = teamIndex;
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
}
