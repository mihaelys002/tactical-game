using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TacticalGame.Grid;

[assembly: InternalsVisibleTo("TacticalGame.Visual.Tests")]

namespace TacticalGame.Prototype
{
    readonly record struct CommandFootprint(HashSet<Unit> Units, HashSet<HexCoord> Cells);

    static class CommandConflictResolver
    {
        public static bool HasConflict(IBattleCommand a, IBattleCommand b)
        {
            var fa = GetFootprint(a);
            var fb = GetFootprint(b);
            return fa.Units.Overlaps(fb.Units)
                || fa.Cells.Overlaps(fb.Cells);
        }

        internal static CommandFootprint GetFootprint(IBattleCommand cmd)
        {
            var units = new HashSet<Unit>();
            var cells = new HashSet<HexCoord>();

            switch (cmd)
            {
                case MoveCommand move:
                    units.Add(move.Unit);
                    cells.Add(move.From);
                    cells.Add(move.To);
                    break;

                case CompoundCommand compound:
                    units.Add(compound.Unit);
                    cells.Add(compound.TargetHex);
                    foreach (var effect in compound.Effects)
                    {
                        units.Add(effect.Target);
                        cells.Add(effect.Target.Position);
                    }
                    break;
            }

            return new CommandFootprint(units, cells);
        }
    }

    static class CommandDependencyBuilder
    {
        public static Dictionary<int, HashSet<int>> Build(List<IBattleCommand> commands)
        {
            int n = commands.Count;
            var footprints = new CommandFootprint[n];
            for (int i = 0; i < n; i++)
                footprints[i] = CommandConflictResolver.GetFootprint(commands[i]);

            var deps = new Dictionary<int, HashSet<int>>(n);
            for (int i = 0; i < n; i++)
                deps[i] = new HashSet<int>();

            for (int i = 1; i < n; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (footprints[i].Units.Overlaps(footprints[j].Units)
                        || footprints[i].Cells.Overlaps(footprints[j].Cells))
                        deps[i].Add(j);
                }
            }

            return deps;
        }
    }

    static class CommandBatcher
    {
        public static List<List<IBattleCommand>> Batch(List<IBattleCommand> commands)
        {
            if (commands.Count == 0)
                return new List<List<IBattleCommand>>();

            var deps = CommandDependencyBuilder.Build(commands);
            var levels = new int[commands.Count];

            for (int i = 0; i < commands.Count; i++)
            {
                int maxDepLevel = -1;
                foreach (int d in deps[i])
                {
                    if (levels[d] > maxDepLevel)
                        maxDepLevel = levels[d];
                }
                levels[i] = maxDepLevel + 1;
            }

            int maxLevel = levels.Max();
            var batches = new List<List<IBattleCommand>>(maxLevel + 1);
            for (int l = 0; l <= maxLevel; l++)
                batches.Add(new List<IBattleCommand>());

            for (int i = 0; i < commands.Count; i++)
                batches[levels[i]].Add(commands[i]);

            return batches;
        }
    }
}
