using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI.Layers
{
    public class ScriptedDirector : IDirectorLayer
    {
        private readonly List<DirectorTrigger> _triggers = new();

        public void AddTrigger(DirectorTrigger trigger) => _triggers.Add(trigger);

        public AIAction? Override(Unit unit, AIBlackboard blackboard)
        {
            foreach (var trigger in _triggers)
            {
                if (trigger.Unit != unit) continue;
                if (trigger.HasFired && !trigger.Repeats) continue;
                if (!trigger.ShouldFire(blackboard)) continue;

                var action = trigger.CreateAction(blackboard);
                trigger.MarkFired();
                return action;
            }

            return null;
        }
    }

    public abstract class DirectorTrigger
    {
        public Unit Unit { get; }
        public bool HasFired { get; private set; }

        // Repeating triggers fire every turn their condition holds
        // (e.g. "keeps fleeing", "keeps attacking the marked target").
        public virtual bool Repeats => false;

        protected DirectorTrigger(Unit unit)
        {
            Unit = unit;
        }

        public abstract bool ShouldFire(AIBlackboard blackboard);
        public abstract AIAction CreateAction(AIBlackboard blackboard);

        public void MarkFired() => HasFired = true;
    }
}
