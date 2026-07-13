using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI.Debug
{
    // Tier 3: per-turn archive of decision traces. Survives undo, so past
    // turns stay inspectable. Call from the game loop thread after planning.
    public class DecisionHistory
    {
        private readonly Dictionary<int, List<DecisionTrace>> _turns = new();

        public int? LatestTurn { get; private set; }

        public void Store(int turn, List<DecisionTrace> traces)
        {
            _turns[turn] = traces;
            if (LatestTurn == null || turn > LatestTurn)
                LatestTurn = turn;
        }

        public IReadOnlyList<DecisionTrace>? GetTurn(int turn)
        {
            return _turns.TryGetValue(turn, out var traces) ? traces : null;
        }

        public DecisionTrace? Find(int turn, Unit unit)
        {
            if (!_turns.TryGetValue(turn, out var traces)) return null;
            foreach (var t in traces)
                if (t.Unit == unit) return t;
            return null;
        }

        public void Clear()
        {
            _turns.Clear();
            LatestTurn = null;
        }
    }
}
