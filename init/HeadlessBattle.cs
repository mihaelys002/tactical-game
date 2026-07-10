using System;
using System.Collections.Generic;
using System.Globalization;
using TacticalGame.AI;
using TacticalGame.AI.Debug;
using TacticalGame.Grid;
using TacticalGame.Messages;

namespace TacticalGame
{
    public static class HeadlessBattle
    {
        public static void Run(int maxTurns = 200, bool debugAI = false, bool visionAI = false)
        {
            var setup = BattleSetup.CreatePrototype();

            DecisionLog? log = null;
            List<UtilityAIPlanner>? visionPlanners = null;
            MessageEngine? narrator = null;
            BattleManager manager;
            if (visionAI)
            {
                // Full 5-layer resource-driven AI: even teams get the orc
                // warchief, odd teams the goblin boss.
                var resourceRoot = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources");
                var registry = TacticalGame.AI.Utility.AIDefRegistry.LoadFromDirectory(resourceRoot);

                log = debugAI ? new DecisionLog() : null;
                var orc = new UtilityAIPlanner(registry, registry.Commander("OrcWarchief")) { Log = log };
                var goblin = new UtilityAIPlanner(registry, registry.Commander("GoblinBoss")) { Log = log };
                visionPlanners = new List<UtilityAIPlanner> { orc, goblin };

                manager = new BattleManager(setup.Battle,
                    (unit, battle) => visionPlanners[unit.TeamIndex % 2].Plan(unit, battle),
                    useThreads: true);

                // Banter: a pure observer on top — AI never knows it exists.
                var meta = new CampaignContext();
                meta.Factions.Add("Orcs");
                meta.Factions.Add("Goblins");
                narrator = new MessageEngine(MessageRegistry.LoadFromDirectory(resourceRoot),
                    meta, seed: 42, planners: visionPlanners);
                foreach (var unit in setup.Battle.Units)
                    narrator.SetVoice(unit, unit.TeamIndex % 2 == 0 ? "Orc" : "Goblin");
                narrator.BeginBattle(setup.Battle);
            }
            else if (debugAI)
            {
                log = new DecisionLog();
                var planner = new AIPlanner { Log = log };
                manager = new BattleManager(setup.Battle, planner.Plan, useThreads: false);
            }
            else
            {
                manager = new BattleManager(setup.Battle, useThreads: false);
            }

            manager.OnLog += Console.WriteLine;

            Console.WriteLine("=== Headless Battle: 4 teams x 6 units ===");
            PrintTeams(manager);

            while (!manager.IsBattleOver() && manager.TurnNumber < maxTurns)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"\n--- Turn {manager.TurnNumber + 1} ---"));

                var commands = manager.StepTurn();

                if (commands.Count == 0)
                    Console.WriteLine("  (no actions)");

                if (log != null)
                {
                    foreach (var trace in log.Snapshot())
                        Console.Write(DecisionFormatter.Format(trace, topN: 3));
                    log.Clear();
                }

                if (narrator != null)
                {
                    narrator.ObserveTurn(manager.Battle);
                    foreach (var line in narrator.DrainLines())
                        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                            $"  {line.Speaker}: \"{line.Text}\""));
                }
            }

            if (visionPlanners != null)
            {
                Console.WriteLine("\n=== Disobedience events (commander overruled) ===");
                foreach (var planner in visionPlanners)
                    foreach (var evt in planner.DisobedienceSnapshot())
                        Console.WriteLine("  " + evt);
            }

            Console.WriteLine("\n=== BATTLE OVER ===");
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Ended on turn {manager.TurnNumber}"));
            PrintSurvivors(manager);
        }

        private static void PrintTeams(BattleManager manager)
        {
            var teams = GroupByTeam(manager);
            foreach (var (teamIndex, units) in teams)
            {
                Console.Write("Team " + teamIndex + ": ");
                foreach (var u in units)
                {
                    u.Equipment.TryGetValue(EquipmentSlot.RightHand, out var weapon);
                    Console.Write(u + "(" + (weapon?.Def.Name ?? "none") + ") ");
                }
                Console.WriteLine();
            }
        }

        private static void PrintSurvivors(BattleManager manager)
        {
            var teams = GroupByTeam(manager);
            foreach (var (teamIndex, units) in teams)
            {
                int alive = 0;
                foreach (var u in units)
                    if (u.IsAlive) alive++;

                if (alive == 0) continue;

                Console.Write(string.Create(CultureInfo.InvariantCulture,
                    $"Team {teamIndex} ({alive} alive): "));
                foreach (var u in units)
                {
                    if (!u.IsAlive) continue;
                    Console.Write(string.Create(CultureInfo.InvariantCulture,
                        $"{u}({u.Stats.CurrentHP}/{u.Stats.MaxHP}hp) "));
                }
                Console.WriteLine();
            }
        }

        private static SortedDictionary<int, List<Unit>> GroupByTeam(BattleManager manager)
        {
            var teams = new SortedDictionary<int, List<Unit>>();
            foreach (var u in manager.Battle.Units)
            {
                if (!teams.TryGetValue(u.TeamIndex, out var list))
                {
                    list = new List<Unit>();
                    teams[u.TeamIndex] = list;
                }
                list.Add(u);
            }
            return teams;
        }
    }
}
