using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TacticalGame.AI;
using TacticalGame.Grid;

Console.WriteLine($"Processors: {Environment.ProcessorCount}");
Console.WriteLine();

// Test 1: Pure compute — verify threading actually scales
Console.WriteLine("=== TEST 1: Pure compute (no shared data) ===");
{
    int workItems = 6400;
    int workPerItem = 100_000;

    var sw = Stopwatch.StartNew();
    var results = new long[workItems];
    for (int i = 0; i < workItems; i++)
    {
        long sum = 0;
        for (int j = 0; j < workPerItem; j++) sum += j * j;
        results[i] = sum;
    }
    sw.Stop();
    long singleMs = sw.ElapsedMilliseconds;

    sw.Restart();
    Parallel.For(0, workItems, i =>
    {
        long sum = 0;
        for (int j = 0; j < workPerItem; j++) sum += j * j;
        results[i] = sum;
    });
    sw.Stop();
    long parallelMs = sw.ElapsedMilliseconds;
    Console.WriteLine($"  Single: {singleMs}ms  Parallel: {parallelMs}ms  Speedup: {(double)singleMs / parallelMs:F1}x");
}

Console.WriteLine();

// Test 2: Shared read — all threads read same large array
Console.WriteLine("=== TEST 2: Shared read (all threads scan same array) ===");
{
    int workItems = 6400;
    var sharedArray = new int[3200];
    var rng = new Random(42);
    for (int i = 0; i < sharedArray.Length; i++) sharedArray[i] = rng.Next(1000);

    long SingleWork()
    {
        var r = new long[workItems];
        for (int i = 0; i < workItems; i++)
        {
            long sum = 0;
            foreach (var v in sharedArray) sum += v;
            r[i] = sum;
        }
        return r[0];
    }

    long ParallelWork()
    {
        var r = new long[workItems];
        Parallel.For(0, workItems, i =>
        {
            long sum = 0;
            foreach (var v in sharedArray) sum += v;
            r[i] = sum;
        });
        return r[0];
    }

    var sw = Stopwatch.StartNew();
    SingleWork();
    sw.Stop();
    long singleMs = sw.ElapsedMilliseconds;

    sw.Restart();
    ParallelWork();
    sw.Stop();
    long parallelMs = sw.ElapsedMilliseconds;
    Console.WriteLine($"  Single: {singleMs}ms  Parallel: {parallelMs}ms  Speedup: {(double)singleMs / parallelMs:F1}x");
}

Console.WriteLine();

// Test 3: Actual AI planning — measure real scaling
Console.WriteLine("=== TEST 3: Actual AI DecideAction ===");
{
    int totalUnits = 6400;
    int perSide = totalUnits / 2;
    int radius = (int)Math.Ceiling(Math.Sqrt(totalUnits)) + 3;

    var grid = new HexGrid();
    for (int q = -radius; q <= radius; q++)
    {
        int r1 = Math.Max(-radius, -q - radius);
        int r2 = Math.Min(radius, -q + radius);
        for (int r = r1; r <= r2; r++)
            grid.AddCell(new HexCoord(q, r), TerrainType.Plain, 0);
    }

    var battle = new BattleState(grid);
    var allUnits = new List<Unit>();
    var enemies = new List<Unit>();
    var brain = new AIBrain();
    var rng = new Random(42);

    var coords = grid.AllCells.Select(c => c.Coord).OrderBy(_ => rng.Next()).ToList();
    for (int i = 0; i < totalUnits && i < coords.Count; i++)
    {
        var u = new Unit(UnitStats.Fresh(
            rng.Next(10, 20), rng.Next(8, 16), rng.Next(30, 60),
            rng.Next(60, 100), rng.Next(20, 50), rng.Next(40, 70), rng.Next(50, 80)));
        battle.PlaceUnit(u, coords[i]);
        allUnits.Add(u);
        if (i >= perSide) enemies.Add(u);
    }

    var bb = new AIBlackboard(battle, allUnits, enemies);

    // Single
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < allUnits.Count; i++)
        AIBrain.DecideAction(allUnits[i], bb);
    sw.Stop();
    long singleMs = sw.ElapsedMilliseconds;

    // Parallel
    sw.Restart();
    Parallel.For(0, allUnits.Count, i =>
    {
        AIBrain.DecideAction(allUnits[(int)i], bb);
    });
    sw.Stop();
    long parallelMs = sw.ElapsedMilliseconds;

    Console.WriteLine($"  Single: {singleMs}ms  Parallel: {parallelMs}ms  Speedup: {(double)singleMs / parallelMs:F1}x");
}
