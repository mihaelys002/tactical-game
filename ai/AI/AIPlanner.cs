using TacticalGame.AI.Debug;
using TacticalGame.AI.Layers;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public class AIPlanner
    {
        // When set, every utility decision is recorded here (debug only).
        public DecisionLog? Log { get; set; }

        private readonly ICommanderLayer? _commander;
        private readonly IStrategyLayer _strategy;
        private readonly IGoalLayer _goal;
        private readonly IDirectorLayer? _director;

        private readonly object _cacheLock = new();
        private ScoringContext? _cachedCommanderContext;
        private int _cachedTeamIndex = -1;

        public AIPlanner(
            ICommanderLayer? commander = null,
            IStrategyLayer? strategy = null,
            IGoalLayer? goal = null,
            IDirectorLayer? director = null)
        {
            _commander = commander;
            _strategy = strategy ?? new DefaultStrategyLayer();
            _goal = goal ?? new DefaultGoalLayer();
            _director = director;
        }

        public void BeginTurn()
        {
            lock (_cacheLock)
            {
                _cachedCommanderContext = null;
                _cachedTeamIndex = -1;
            }
        }

        public AIAction? Plan(Unit unit, BattleState battle)
        {
            var bb = new AIBlackboard(battle, unit);

            if (_director != null)
            {
                var forced = _director.Override(unit, bb);
                if (forced != null) return forced;
            }

            var context = new ScoringContext();

            if (_commander != null)
            {
                // Locked: planning runs on parallel threads (BattleManager.PlanAll).
                lock (_cacheLock)
                {
                    if (_cachedCommanderContext == null || _cachedTeamIndex != unit.TeamIndex)
                    {
                        _commander.Evaluate(bb, context);
                        _cachedCommanderContext = context;
                        _cachedTeamIndex = unit.TeamIndex;
                    }
                    else
                    {
                        context.AggressionBias = _cachedCommanderContext.AggressionBias;
                        context.PriorityTarget = _cachedCommanderContext.PriorityTarget;
                        context.ProtectTarget = _cachedCommanderContext.ProtectTarget;
                        context.ForceRetreatUnit = _cachedCommanderContext.ForceRetreatUnit;
                    }
                }
            }

            _strategy.Evaluate(unit, bb, context);
            _goal.Evaluate(unit, bb, context);

            if (Log == null)
                return AIBrain.DecideAction(unit, bb, context);

            var trace = new DecisionTrace(unit, battle.TurnNumber) { Context = context };
            var result = AIBrain.DecideAction(unit, bb, context, trace);
            Log.Add(trace);
            return result;
        }
    }
}
