using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI.Layers
{
    // Forced-action builders shared by scripted triggers.
    internal static class ScriptedActions
    {
        internal static AIAction? MoveAwayFromEnemies(Unit unit, AIBlackboard bb)
        {
            var enemy = Closest(unit, bb.Enemies);
            if (enemy == null) return null;

            HexCoord? best = null;
            int bestDist = unit.Position.DistanceTo(enemy.Position);
            foreach (var neighbor in bb.Battle.Grid.GetNeighbors(unit.Position))
            {
                if (!neighbor.IsWalkable) continue;
                int d = neighbor.Coord.DistanceTo(enemy.Position);
                if (d > bestDist) { bestDist = d; best = neighbor.Coord; }
            }

            return best is HexCoord target ? AIAction.Move(unit, target, float.MaxValue) : null;
        }

        internal static AIAction? AttackOrChase(Unit unit, Unit target, AIBlackboard bb)
        {
            int distance = unit.Position.DistanceTo(target.Position);

            foreach (var equipment in unit.Equipment.Values)
            {
                foreach (var skill in equipment.Def.GrantedSkills)
                {
                    if (skill.Range == 0 || distance > skill.Range) continue;
                    if (unit.Stats.CurrentFatigue + skill.FatigueCost > unit.Stats.MaxFatigue) continue;
                    return AIAction.UseSkill(unit, target.Position, skill, equipment.Def, float.MaxValue);
                }
            }

            // Not in range (or too tired to swing) — close in.
            HexCoord? best = null;
            int bestDist = distance;
            foreach (var neighbor in bb.Battle.Grid.GetNeighbors(unit.Position))
            {
                if (!neighbor.IsWalkable) continue;
                int d = neighbor.Coord.DistanceTo(target.Position);
                if (d < bestDist) { bestDist = d; best = neighbor.Coord; }
            }

            return best is HexCoord step ? AIAction.Move(unit, step, float.MaxValue) : null;
        }

        internal static Unit? Closest(Unit unit, IReadOnlyList<Unit> units)
        {
            Unit? closest = null;
            int closestDist = int.MaxValue;
            foreach (var u in units)
            {
                int d = unit.Position.DistanceTo(u.Position);
                if (d < closestDist) { closestDist = d; closest = u; }
            }
            return closest;
        }
    }

    // "The last N defenders always flee." Repeats every turn it holds.
    public class FleeWhenOutnumberedTrigger : DirectorTrigger
    {
        private readonly int _aliveThreshold;

        public override bool Repeats => true;

        public FleeWhenOutnumberedTrigger(Unit unit, int aliveThreshold) : base(unit)
        {
            _aliveThreshold = aliveThreshold;
        }

        public override bool ShouldFire(AIBlackboard blackboard)
        {
            int alive = Unit.IsAlive ? 1 : 0;
            foreach (var ally in blackboard.Friends)
                if (ally.IsAlive) alive++;
            return alive <= _aliveThreshold && ScriptedActions.MoveAwayFromEnemies(Unit, blackboard) != null;
        }

        public override AIAction CreateAction(AIBlackboard blackboard)
            => ScriptedActions.MoveAwayFromEnemies(Unit, blackboard)!;
    }

    // "Unit X snaps when unit Y dies" — one forced attack on the nearest enemy.
    public class EnrageOnAllyDeathTrigger : DirectorTrigger
    {
        private readonly Unit _watched;

        public EnrageOnAllyDeathTrigger(Unit unit, Unit watched) : base(unit)
        {
            _watched = watched;
        }

        public override bool ShouldFire(AIBlackboard blackboard)
        {
            if (_watched.IsAlive) return false;
            var enemy = ScriptedActions.Closest(Unit, blackboard.Enemies);
            return enemy != null && ScriptedActions.AttackOrChase(Unit, enemy, blackboard) != null;
        }

        public override AIAction CreateAction(AIBlackboard blackboard)
        {
            var enemy = ScriptedActions.Closest(Unit, blackboard.Enemies)!;
            return ScriptedActions.AttackOrChase(Unit, enemy, blackboard)!;
        }
    }

    // "Kill X, ignore everything else." Repeats until the target is dead.
    public class FocusTargetTrigger : DirectorTrigger
    {
        private readonly Unit _target;

        public override bool Repeats => true;

        public FocusTargetTrigger(Unit unit, Unit target) : base(unit)
        {
            _target = target;
        }

        public override bool ShouldFire(AIBlackboard blackboard)
            => _target.IsAlive && ScriptedActions.AttackOrChase(Unit, _target, blackboard) != null;

        public override AIAction CreateAction(AIBlackboard blackboard)
            => ScriptedActions.AttackOrChase(Unit, _target, blackboard)!;
    }

    // "No one ever flees" — when the unit is hurt enough that normal AI would
    // retreat, force an attack on the nearest enemy instead. Repeats.
    public class StandGroundTrigger : DirectorTrigger
    {
        private readonly float _hpThreshold;

        public override bool Repeats => true;

        public StandGroundTrigger(Unit unit, float hpThreshold = 0.25f) : base(unit)
        {
            _hpThreshold = hpThreshold;
        }

        public override bool ShouldFire(AIBlackboard blackboard)
        {
            float hpRatio = (float)Unit.Stats.CurrentHP / Unit.Stats.MaxHP;
            if (hpRatio >= _hpThreshold) return false;
            var enemy = ScriptedActions.Closest(Unit, blackboard.Enemies);
            return enemy != null && ScriptedActions.AttackOrChase(Unit, enemy, blackboard) != null;
        }

        public override AIAction CreateAction(AIBlackboard blackboard)
        {
            var enemy = ScriptedActions.Closest(Unit, blackboard.Enemies)!;
            return ScriptedActions.AttackOrChase(Unit, enemy, blackboard)!;
        }
    }

    // Prebuilt scripted fights from the design doc.
    public static class ScriptedFights
    {
        // "The last 3 enemies will flee, 100%."
        public static ScriptedDirector LastSurvivorsFlee(IEnumerable<Unit> team, int threshold = 3)
        {
            var director = new ScriptedDirector();
            foreach (var unit in team)
                director.AddTrigger(new FleeWhenOutnumberedTrigger(unit, threshold));
            return director;
        }

        // "No one will ever flee."
        public static ScriptedDirector NoRetreat(IEnumerable<Unit> team)
        {
            var director = new ScriptedDirector();
            foreach (var unit in team)
                director.AddTrigger(new StandGroundTrigger(unit));
            return director;
        }

        // "Unit X becomes extremely aggressive if unit Y dies."
        public static ScriptedDirector Vengeance(Unit avenger, Unit watched)
        {
            var director = new ScriptedDirector();
            director.AddTrigger(new EnrageOnAllyDeathTrigger(avenger, watched));
            return director;
        }

        // "The assassin hunts one target until it dies."
        public static ScriptedDirector Assassination(Unit assassin, Unit target)
        {
            var director = new ScriptedDirector();
            director.AddTrigger(new FocusTargetTrigger(assassin, target));
            return director;
        }
    }
}
