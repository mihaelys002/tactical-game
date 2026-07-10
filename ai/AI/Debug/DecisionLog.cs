using System.Collections.Generic;

namespace TacticalGame.AI.Debug
{
    // Thread-safe collection of decision traces. Planning threads each
    // build their own DecisionTrace, then Add() it here (single lock).
    public class DecisionLog
    {
        private readonly object _lock = new();
        private readonly List<DecisionTrace> _traces = new();

        public void Add(DecisionTrace trace)
        {
            lock (_lock)
                _traces.Add(trace);
        }

        public List<DecisionTrace> Snapshot()
        {
            lock (_lock)
                return new List<DecisionTrace>(_traces);
        }

        public void Clear()
        {
            lock (_lock)
                _traces.Clear();
        }
    }
}
