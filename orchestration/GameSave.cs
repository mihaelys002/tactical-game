using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TacticalGame.AI;
using TacticalGame.AI.Utility;
using TacticalGame.Grid;

namespace TacticalGame
{
    // Whole-game save: BattleState plus every AI planner's battle memory,
    // in ONE json document. One document matters: PreserveReferences only
    // unifies $refs within a single graph, so planner goals pointing at
    // units resolve to the same Unit instances as the loaded battle.
    //
    // Serialization rules come from BattleSave.CreateSettings — no second
    // strategy, just two extra def-by-name converters for AI defs.
    public static class GameSave
    {
        [JsonObject(MemberSerialization.Fields)]
        private sealed class Envelope
        {
            private readonly BattleState _battle; // first: units serialize here, planners $ref them
            private readonly List<PlannerSave> _planners;

            public BattleState Battle => _battle;
            public IReadOnlyList<PlannerSave> Planners => _planners;

            private Envelope() { _battle = null!; _planners = new List<PlannerSave>(); } // serialization only

            public Envelope(BattleState battle, List<PlannerSave> planners)
            {
                _battle = battle;
                _planners = planners;
            }
        }

        private static JsonSerializerSettings Settings(AIDefRegistry registry)
            => BattleSave.CreateSettings(
                new StrategyDefConverter(registry),
                new GoalDefConverter(registry));

        public static void Save(BattleState battle, IReadOnlyList<UtilityAIPlanner> planners,
            AIDefRegistry registry, Stream stream)
        {
            var saves = new List<PlannerSave>();
            foreach (var planner in planners)
                saves.Add(planner.ExportState(battle));

            var json = JsonConvert.SerializeObject(new Envelope(battle, saves), Settings(registry));
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(json);
        }

        // Planners are rebuilt from the registry (commander by saved name)
        // and get their battle memory imported. Config that lives outside
        // the save — directors, action weight assignments — is the caller's
        // to re-attach, same as the registry itself.
        public static (BattleState battle, List<UtilityAIPlanner> planners) Load(
            Stream stream, AIDefRegistry registry)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            var envelope = JsonConvert.DeserializeObject<Envelope>(reader.ReadToEnd(), Settings(registry))!;

            var planners = new List<UtilityAIPlanner>();
            foreach (var save in envelope.Planners)
            {
                var planner = new UtilityAIPlanner(registry, registry.Commander(save.Commander));
                planner.ImportState(save);
                planners.Add(planner);
            }

            return (envelope.Battle, planners);
        }
    }
}
