using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.AI.Debug
{
    public readonly struct ScoreTerm
    {
        public string Name { get; }
        public float Input { get; }
        public float Weight { get; }
        public float Contribution { get; }

        public ScoreTerm(string name, float input, float weight, float contribution)
        {
            Name = name;
            Input = input;
            Weight = weight;
            Contribution = contribution;
        }
    }

    public class ScoreBreakdown
    {
        public AIAction Action { get; }
        public string ActionLabel { get; }
        public IReadOnlyList<ScoreTerm> Terms { get; }
        public float Total => Action.Score;

        public ScoreBreakdown(AIAction action, string actionLabel, List<ScoreTerm> terms)
        {
            Action = action;
            ActionLabel = actionLabel;
            Terms = terms;
        }
    }

    // Collects all candidate breakdowns for one unit's plan call.
    // One trace per unit per plan — never shared between threads.
    public class DecisionTrace
    {
        private readonly List<ScoreBreakdown> _candidates = new();

        public Unit Unit { get; }
        public int TurnNumber { get; }
        public IReadOnlyList<ScoreBreakdown> Candidates => _candidates;
        public AIAction? Chosen { get; private set; }

        // Layer chain snapshot (what Commander/Strategy/Goal handed down).
        public ScoringContext? Context { get; set; }

        public DecisionTrace(Unit unit, int turnNumber)
        {
            Unit = unit;
            TurnNumber = turnNumber;
        }

        public void Add(AIAction action, string label, List<ScoreTerm> terms)
        {
            _candidates.Add(new ScoreBreakdown(action, label, terms));
        }

        public void MarkChosen(AIAction? action)
        {
            Chosen = action;
        }

        public ScoreBreakdown? FindBreakdown(AIAction action)
        {
            foreach (var c in _candidates)
                if (c.Action == action) return c;
            return null;
        }
    }
}
