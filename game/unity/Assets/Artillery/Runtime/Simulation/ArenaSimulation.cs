using System;
using System.Collections.Generic;
using Artillery.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using NVector = System.Numerics.Vector2;
using Object = UnityEngine.Object;

namespace Artillery
{
    public sealed class ArenaSimulation : IDisposable, IArenaViewState
    {
        private static readonly ProfilerMarker StepMarker = new ProfilerMarker("Artillery.Simulation.Step");
        private static int serial;
        private readonly Scene scene;
        private readonly GameObject root;
        private readonly PhysicsMaterial2D tread;
        private readonly ShotQueries queries;
        private readonly List<Cut> cuts = new List<Cut>(Experiment.MaxShots);
        private readonly TankBody[] tanks;

        public PhysicsScene2D Physics { get; }
        public TerrainCollider Terrain { get; }
        public Ballistics Weapon { get; } = new Ballistics();
        public IReadOnlyList<TankBody> Tanks => tanks;
        public RunTrace Trace { get; }
        public Scenario Scenario { get; }
        public int Tick { get; private set; }
        public int Power { get; private set; } = 74;
        public int Drive { get; private set; }
        public int PeakShots { get; private set; }

        public int TankCount => tanks.Length;
        public int ShotCount => Weapon.Shots.Count;
        public int TerrainVersion => Terrain.Grid.Version;
        public int SolidCells => Terrain.Grid.SolidCount;
        public int TerrainChunks => Terrain.Grid.ChunksX * Terrain.Grid.ChunksY;
        public float CellSize => Terrain.Grid.CellSize;
        public int ImpactCount => Trace.impacts.Count;

        public int ChunkRevision(int chunk) => Terrain.Grid.ChunkRevision(chunk);

        public void GetTerrainRectangles(int chunk, List<CellRect> result) =>
            Terrain.Grid.GetRectangles(chunk, result);

        public TankPose GetTank(int index)
        {
            TankBody tank = tanks[index];
            return new TankPose(tank.Body.position, tank.Pivot, tank.Body.rotation, tank.Angle,
                tank.Hull.IsTouching(Terrain.Collider));
        }

        public ShotPose GetShot(int index)
        {
            Shot shot = Weapon.Shots[index];
            return new ShotPose(shot.Id, shot.Position.Unity(), shot.Velocity.Unity());
        }

        public ImpactRecord GetImpact(int index) => Trace.impacts[index];

        public ArenaSimulation(Scenario scenario)
        {
            Scenario = scenario;
            Trace = new RunTrace
            {
                experiment = Experiment.Version,
                unity = Application.unityVersion,
                platform = Application.platform.ToString(),
                scenario = (int)scenario
            };
            scene = SceneManager.CreateScene("Artillery Physics " + ++serial,
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            Physics = scene.GetPhysicsScene2D();
            root = new GameObject("Arena simulation");
            SceneManager.MoveGameObjectToScene(root, scene);
            var ground = new GameObject("Terrain");
            ground.transform.SetParent(root.transform, false);
            Terrain = new TerrainCollider(Experiment.CreateTerrain(scenario), ground.transform);

            tread = new PhysicsMaterial2D("Experimental tread") { friction = 0.55f, bounciness = 0 };
            ArenaWalls.Create(root.transform, tread);
            NVector[] positions = Experiment.Spawn(scenario);
            tanks = new TankBody[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                tanks[i] = new TankBody(i, positions[i].Unity(), root.transform, tread);
            queries = new ShotQueries(Physics, Terrain.Collider, tanks);
            Physics2D.SyncTransforms();
        }

        public void Step(TickCommand? command = null)
        {
            using var sample = StepMarker.Auto();
            if (command.HasValue)
            {
                TickCommand current = command.Value;
                if (current.tick != Tick) throw new ArgumentException("Command tick does not match the world.");
                Trace.commands.Add(current);
                Apply(current);
            }

            foreach (TankBody tank in tanks)
                tank.ApplyForces(tank.Id == 0 ? Drive : 0, Terrain.Collider);
            if (!Physics.Simulate(Experiment.Step))
                throw new InvalidOperationException("The local 2D physics step did not run.");

            // Projectile sweeps use tank poses at the end of this tick's rigid-body step.
            Weapon.Step(queries.Sweep, ResolveImpacts);
            Trace.hashes.Add(CaptureHash());
            Tick++;
        }

        private void Apply(TickCommand command)
        {
            tanks[0].Angle = Mathf.Clamp(command.angle, 0, 180);
            Power = Mathf.Clamp(command.power, 0, 100);
            Drive = Mathf.Clamp(command.drive, -1, 1);

            if (command.drill)
            {
                var from = new NVector(command.fromX, command.fromY) / Experiment.CoordinateScale;
                var to = new NVector(command.toX, command.toY) / Experiment.CoordinateScale;
                cuts.Clear();
                cuts.Add(new Cut(from, to, Experiment.DrillRadius));
                ApplyCuts();
            }

            if (command.volley && Scenario == Scenario.Volley)
            {
                foreach (TankBody tank in tanks)
                {
                    tank.Angle = 7;
                    Weapon.Launch(tank.Id, tank.Pivot.Core(), tank.Angle, 100);
                }
                Power = 100;
            }
            else if (command.fire)
                Weapon.Launch(0, tanks[0].Pivot.Core(), tanks[0].Angle, Power);

            PeakShots = Math.Max(PeakShots, Weapon.Shots.Count);
        }

        private void ResolveImpacts(IReadOnlyList<Impact> group)
        {
            int before = Terrain.Grid.Version;
            cuts.Clear();
            foreach (Impact impact in group)
                cuts.Add(new Cut(impact.Point, impact.Point, Experiment.BlastRadius));
            ApplyCuts();

            foreach (Impact impact in group)
                Trace.impacts.Add(new ImpactRecord
                {
                    tick = Tick, shot = impact.ShotId, target = impact.Target,
                    fraction = impact.Fraction, x = impact.Point.X, y = impact.Point.Y,
                    terrainBefore = before, terrainAfter = Terrain.Grid.Version
                });
        }

        private void ApplyCuts()
        {
            if (Terrain.Grid.Remove(cuts) == 0) return;
            Terrain.Synchronize();
            foreach (TankBody tank in tanks)
                tank.Body.WakeUp();
        }

        private string CaptureHash()
        {
            var hash = StateHash.Begin();
            hash.Add(Tick);
            hash.Add((int)Scenario);
            hash.Add(Power);
            hash.Add(Drive);
            Terrain.Grid.AddTo(ref hash);
            Weapon.AddTo(ref hash);
            foreach (TankBody tank in tanks)
            {
                hash.Add(tank.Angle);
                hash.Add(tank.Body.position.Core());
                hash.Add(tank.Body.rotation);
                hash.Add(tank.Body.linearVelocity.Core());
                hash.Add(tank.Body.angularVelocity);
                hash.Add(tank.Body.IsAwake() ? 1 : 0);
            }
            return hash.ToString();
        }

        public void Dispose()
        {
            if (root == null || !root.activeSelf) return;
            root.SetActive(false);
            if (scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            Object.Destroy(tread);
        }
    }
}
