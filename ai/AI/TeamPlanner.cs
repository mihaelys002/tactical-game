using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI
{
    public class TeamPlanner
    {
        private readonly Dictionary<int, AIPlanner> _teamPlanners = new();
        private readonly AIPlanner _default;

        public TeamPlanner(AIPlanner defaultPlanner)
        {
            _default = defaultPlanner;
        }

        public void SetTeamPlanner(int teamIndex, AIPlanner planner)
        {
            _teamPlanners[teamIndex] = planner;
        }

        public void BeginTurn()
        {
            _default.BeginTurn();
            foreach (var planner in _teamPlanners.Values)
                planner.BeginTurn();
        }

        public AIAction? Plan(Unit unit, BattleState battle)
        {
            var planner = _teamPlanners.GetValueOrDefault(unit.TeamIndex, _default);
            return planner.Plan(unit, battle);
        }
    }
}
