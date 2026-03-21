using System;
using System.Collections.Generic;
using System.Globalization;
using TacticalGame.Grid;

namespace TacticalGame
{
    public static class HeadlessBattle
    {
        public static void Run(int maxTurns = 200)
        {
            var setup = BattleSetup.CreatePrototype();
            var manager = new BattleManager(setup.Battle, useThreads: false);

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
