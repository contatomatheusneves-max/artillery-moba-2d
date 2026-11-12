using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Artillery.G2.Core;

var checks = new Action[]
{
    TerrainCutAndRectangles,
    ChunkBoundaryAndNoOp,
    ProtectedFloorSurvivesCircularCut,
    ProtectedFloorSurvivesTunnelCut,
    AimPowerAndGravity,
    ResweepAfterEarlierImpact,
    EqualTimeImpactsAreGrouped,
    FixedClockReplay,
    VolleyAgainstSampledTerrain
};
foreach (Action check in checks)
{
    check();
    Console.WriteLine("PASS " + check.Method.Name.Split('|')[0].Replace("<<Main>$>g__", ""));
}
Console.WriteLine($"{checks.Length}/{checks.Length} core checks passed.");

static void TerrainCutAndRectangles()
{
    var grid = new TerrainGrid(34, 19, 0.25f, (_, _) => true);
    var crater = new Vector2(2.25f, 4.75f);
    Require(grid.Remove(new[] { new Cut(crater, crater, 0.65f) }) > 0, "crater removed nothing");
    Require(!grid.IsSolid(new Vector2(2.25f, 4.4f)), "crater blocked");
    Require(grid.Remove(new[] { new Cut(new Vector2(1, 2), new Vector2(8, 2), 0.55f) }) > 0,
        "tunnel removed nothing");
    Require(!grid.IsSolid(new Vector2(4, 2)), "tunnel blocked");
    Require(grid.IsSolid(new Vector2(4, 2.8f)) && grid.IsSolid(new Vector2(4, 1.2f)),
        "roof or floor removed");

    var coverage = new int[grid.Width, grid.Height];
    var rectangles = new List<CellRect>();
    for (int chunk = 0; chunk < grid.ChunksX * grid.ChunksY; chunk++)
    {
        grid.GetRectangles(chunk, rectangles);
        foreach (CellRect r in rectangles)
        for (int y = r.Y; y < r.Y + r.Height; y++)
        for (int x = r.X; x < r.X + r.Width; x++)
            coverage[x, y]++;
    }
    for (int y = 0; y < grid.Height; y++)
    for (int x = 0; x < grid.Width; x++)
        Require(coverage[x, y] == (grid.IsSolid(x, y) ? 1 : 0), $"rectangle mismatch at {x},{y}");
}

static void ChunkBoundaryAndNoOp()
{
    var grid = new TerrainGrid(32, 32, 0.25f, (_, _) => true);
    var center = new Vector2(4, 4);
    var cuts = new[] { new Cut(center, center, 0.5f), new Cut(center, center, 0.5f) };
    Require(grid.Remove(cuts) == 12, "overlapping cuts removed an incorrect number of cells");
    Require(grid.Version == 1 && Enumerable.Range(0, 4).All(i => grid.ChunkRevision(i) == 1),
        "dirty chunk revisions did not cover the corner");
    Require(grid.Remove(cuts) == 0 && grid.Version == 1, "no-op invalidated terrain");
}

static void ProtectedFloorSurvivesCircularCut()
{
    var grid = new TerrainGrid(20, 12, Experiment.Cell, (_, y) => y >= 2, protectedRows: 2);
    CheckSolidFloor(grid);
    var center = new Vector2(2.125f, 0.35f);
    Require(grid.Remove(new[] { new Cut(center, center, 0.85f) }) > 0, "crater removed no unprotected cells");
    Require(!grid.IsSolid(8, 2) && grid.IsSolid(0, 2), "crater did not remove only nearby soil above the floor");
    CheckSolidFloor(grid);

    int revision = grid.Version;
    var insideFloor = new Vector2(0.125f, 0.125f);
    Require(grid.Remove(new[] { new Cut(insideFloor, insideFloor, 0.2f) }) == 0 &&
        grid.Version == revision, "protected-only cut changed the terrain revision");
}

static void ProtectedFloorSurvivesTunnelCut()
{
    foreach (Scenario scenario in new[] { Scenario.Sandbox, Scenario.Volley })
    {
        var grid = Experiment.CreateTerrain(scenario);
        Require(grid.ProtectedRows == Experiment.ProtectedTerrainRows && grid.ProtectedRows > 0,
            "scenario did not configure its floor");
        var tunnel = new Cut(new Vector2(-1, 0.12f), new Vector2(Experiment.Width + 1, 0.6f), 0.6f);
        Require(grid.Remove(new[] { tunnel }) > 0, "tunnel removed no unprotected cells");
        for (int x = 0; x < grid.Width; x++)
            Require(!grid.IsSolid(x, grid.ProtectedRows), "tunnel stopped early above the floor at " + x);
        Require(grid.IsSolid(grid.Width / 2, grid.ProtectedRows + 4), "tunnel removed its roof");
        CheckSolidFloor(grid);
    }
}

static void CheckSolidFloor(TerrainGrid grid)
{
    for (int y = 0; y < grid.ProtectedRows; y++)
    for (int x = 0; x < grid.Width; x++)
        Require(grid.IsSolid(x, y), $"protected cell {x},{y} was empty");
}

static void AimPowerAndGravity()
{
    var shots = new Ballistics();
    Require(!shots.Launch(0, Vector2.Zero, 90, 0), "zero power launched");
    Require(Experiment.Aim(0) == Vector2.UnitX && Experiment.Aim(180) == -Vector2.UnitX &&
        Experiment.Aim(90) == Vector2.UnitY, "global endpoint aim incorrect");
    Require(Experiment.Aim(135).X < 0 && Experiment.Aim(135).Y > 0, "left quadrant incorrect");
    for (int i = 0; i < 4; i++)
        Require(shots.Launch(i, new Vector2(10, 10), 0, 100), "same-tick shot rejected");
    Require(!shots.Launch(4, new Vector2(10, 10), 0, 100), "capacity exceeded");
    shots.Step(EmptySweep, _ => throw new Exception("unexpected impact"));
    Require(shots.Shots.Count == 4 && shots.Shots.All(shot => shot.Age == 1 && shot.Position.Y < 10),
        "coincident shells interacted or gravity did not apply");
}

static void ResweepAfterEarlierImpact()
{
    var shots = new Ballistics();
    shots.Launch(0, new Vector2(1.12f - Experiment.MuzzleLength, 5), 0, 100);
    shots.Launch(1, new Vector2(0.99f - Experiment.MuzzleLength, 5), 0, 100);
    float wall = 1.25f;
    var groups = new List<Impact>();
    shots.Step(SweepWall, group =>
    {
        groups.AddRange(group);
        wall = 1.40f;
    });
    Require(groups.Count == 2 && groups[0].ShotId == 0 && groups[1].ShotId == 1 &&
        groups[0].Fraction < groups[1].Fraction && groups[1].Point.X > groups[0].Point.X + 0.1f,
        "second shot reused stale geometry in the same tick");

    bool SweepWall(Vector2 from, Vector2 to, float radius, int owner, out SweepHit hit)
    {
        float surface = wall - radius;
        float t = (surface - from.X) / (to.X - from.X);
        hit = new SweepHit(t, new Vector2(wall, 5), -1);
        return t >= 0 && t <= 1;
    }
}

static void EqualTimeImpactsAreGrouped()
{
    var shots = new Ballistics();
    shots.Launch(0, new Vector2(3, 5), 0, 100);
    shots.Launch(1, new Vector2(3, 5), 0, 100);
    int groups = 0;
    shots.Step(Hit, group =>
    {
        groups++;
        Require(group.Count == 2 && group[0].Fraction == group[1].Fraction, "ties were split");
    });
    Require(groups == 1 && shots.Shots.Count == 0, "tied shots were not resolved together");

    static bool Hit(Vector2 from, Vector2 to, float radius, int owner, out SweepHit hit)
    {
        hit = new SweepHit(0.5f, Vector2.Lerp(from, to, 0.5f), -1);
        return true;
    }
}

static void FixedClockReplay()
{
    string[] first = Trace(30);
    Require(first.Length == 180 && first.SequenceEqual(Trace(144)), "frame schedule changed the replay");

    var clock = new FixedClock();
    int ticks = 0;
    clock.Advance(0.5, () => ticks++, 8);
    Require(ticks == 8, "budget exceeded");
    clock.Advance(0, () => ticks++);
    Require(ticks == 30, "budget discarded elapsed time");

    static string[] Trace(int rate)
    {
        var grid = Experiment.CreateTerrain(Scenario.Sandbox);
        var shots = new Ballistics();
        var clock = new FixedClock();
        var hashes = new List<string>();
        for (int frame = 0; frame < rate * 3; frame++)
        {
            clock.Advance(1.0 / rate, () =>
            {
                int tick = hashes.Count;
                if (tick == 4) shots.Launch(0, new Vector2(9, 6), 78, 80);
                if (tick == 63)
                    grid.Remove(new[] { new Cut(new Vector2(8, 2), new Vector2(18, 2.4f), 0.7f) });
                shots.Step(EmptySweep, _ => throw new Exception("unexpected impact"));
                var hash = StateHash.Begin();
                hash.Add(tick);
                grid.AddTo(ref hash);
                shots.AddTo(ref hash);
                hashes.Add(hash.ToString());
            });
        }
        return hashes.ToArray();
    }
}

static void VolleyAgainstSampledTerrain()
{
    var grid = Experiment.CreateTerrain(Scenario.Volley);
    var shots = new Ballistics();
    Vector2[] spawns = Experiment.Spawn(Scenario.Volley);
    for (int i = 0; i < spawns.Length; i++)
        shots.Launch(i, new Vector2(spawns[i].X, 4.33f + Experiment.PivotHeight), 7, 100);
    var impacts = new List<Impact>();
    for (int i = 0; i < 120; i++)
        shots.Step(SampledGridSweep, group =>
        {
            grid.Remove(group.Select(hit => new Cut(hit.Point, hit.Point, Experiment.BlastRadius)).ToArray());
            impacts.AddRange(group);
        });
    Require(impacts.Count >= 2 && impacts[0].Point.X >= 16.9f &&
        impacts.Skip(1).Any(hit => hit.Point.X > impacts[0].Point.X + 0.5f),
        "wall fixture did not produce a later, deeper collision with the grid sampler");
    Console.WriteLine("  Sampled terrain impact X: " + string.Join(", ", impacts.Select(hit => hit.Point.X.ToString("F2"))));

    // Fixture check against a sampled occupancy query. Native Unity collision is tested separately.
    bool SampledGridSweep(Vector2 from, Vector2 to, float radius, int owner, out SweepHit hit)
    {
        for (int i = 0; i <= 40; i++)
        {
            float fraction = i / 40f;
            Vector2 center = Vector2.Lerp(from, to, fraction);
            for (int side = 0; side < 8; side++)
            {
                float a = side * MathF.PI / 4;
                Vector2 sample = center + radius * new Vector2(MathF.Cos(a), MathF.Sin(a));
                if (!grid.IsSolid(sample)) continue;
                hit = new SweepHit(fraction, sample, -1);
                return true;
            }
        }
        hit = default;
        return false;
    }
}

static bool EmptySweep(Vector2 from, Vector2 to, float radius, int owner, out SweepHit hit)
{
    hit = default;
    return false;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
