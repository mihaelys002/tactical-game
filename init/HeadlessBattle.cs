using System;
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
            for (int t = 0; t < manager.TeamCount; t++)
            {
                Console.Write("Team " + t + ": ");
                foreach (var u in manager.Teams[t])
                {
                    var weapon = u.Equipment.Get(EquipmentSlot.RightHand);
                    Console.Write(u + "(" + (weapon?.Def.Name ?? "none") + ") ");
                }
                Console.WriteLine();
            }
        }

        private static void PrintSurvivors(BattleManager manager)
        {
            for (int t = 0; t < manager.TeamCount; t++)
            {
                int alive = 0;
                foreach (var u in manager.Teams[t])
                    if (u.IsAlive) alive++;

                if (alive == 0) continue;

                Console.Write(string.Create(CultureInfo.InvariantCulture,
                    $"Team {t} ({alive} alive): "));
                foreach (var u in manager.Teams[t])
                {
                    if (!u.IsAlive) continue;
                    Console.Write(string.Create(CultureInfo.InvariantCulture,
                        $"{u}({u.Stats.CurrentHP}/{u.Stats.MaxHP}hp) "));
                }
                Console.WriteLine();
            }
        }
    }
}
