using System;
using System.Linq;
using Artillery.G2.Core;
using NUnit.Framework;
using UnityEngine;

namespace Artillery.G2.Tests
{
    public sealed class WorldTests
    {
        [Test]
        public void CraterAndCoveredTunnelChangeNativeCollision()
        {
            using var world = new PocWorld(Scenario.Volley);
            Vector2 top = new Vector2(18, 13);
            Vector2 tunnel = new Vector2(19, 7.5f);
            var initial = world.Physics.Raycast(top, Vector2.down, 4);
            Assert.That(initial.collider, Is.SameAs(world.Terrain.Collider));
            Assert.That(world.Physics.OverlapPoint(tunnel), Is.SameAs(world.Terrain.Collider));
            int solid = world.Terrain.Grid.SolidCount;

            world.Step(Drill(0, 18, 11.7f, 18, 11.7f));
            var crater = world.Physics.Raycast(top, Vector2.down, 4);
            Assert.That(crater.collider, Is.SameAs(world.Terrain.Collider));
            Assert.That(crater.point.y, Is.LessThan(initial.point.y - 0.45f));

            world.Step(Drill(1, 15, 7.5f, 23, 7.5f));
            Assert.That(world.Physics.OverlapPoint(tunnel), Is.Null);
            Assert.That(world.Physics.OverlapPoint(new Vector2(19, 9)), Is.SameAs(world.Terrain.Collider));
            Assert.That(world.Physics.OverlapPoint(new Vector2(19, 6.1f)), Is.SameAs(world.Terrain.Collider));
            Assert.That(world.Physics.Raycast(new Vector2(15, 7.5f), Vector2.right, 8).collider, Is.Null);
            Assert.That(world.Terrain.Grid.SolidCount, Is.LessThan(solid));
        }

        [Test]
        public void TankStaysOnTerrainAndDropsWhenSupportIsRemoved()
        {
            using var world = new PocWorld(Scenario.Volley);
            TankBody tank = world.Tanks[0];
            Advance(world, 70);
            float x = tank.Body.position.x;
            float y = tank.Body.position.y;
            Assert.That(tank.Hull.IsTouching(world.Terrain.Collider), Is.True);
            Assert.That(y, Is.InRange(4.23f, 4.45f));

            world.Step(new TickCommand { tick = world.Tick, angle = 38, power = 74, drive = 1 });
            Advance(world, 20);
            Assert.That(tank.Body.position.x, Is.GreaterThan(x + 0.15f));
            Assert.That(tank.Body.position.y, Is.GreaterThan(4.2f));

            x = tank.Body.position.x;
            world.Step(Drill(world.Tick, x - 1.1f, 4f, x + 1.1f, 4f));
            Advance(world, 50);
            Assert.That(tank.Body.position.y, Is.InRange(3.3f, y - 0.35f));
            Assert.That(tank.Hull.IsTouching(world.Terrain.Collider), Is.True);
        }

        [Test]
        public void FourShellsCoexistAndLaterShellsSeeChangedWall()
        {
            using var world = new PocWorld(Scenario.Volley);
            Advance(world, Experiment.VolleyTick);
            world.Step(Volley(world.Tick));
            Assert.That(world.Weapon.Shots.Count, Is.EqualTo(4));
            Assert.That(world.PeakShots, Is.EqualTo(4));
            Assert.That(world.Weapon.Shots.All(shot => shot.Age == 1), Is.True);

            Advance(world, 90);
            ImpactRecord[] hits = world.Trace.impacts.Where(hit => hit.target == -1 && hit.x >= 16.9f).ToArray();
            Assert.That(hits.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(hits[0].terrainAfter, Is.GreaterThan(hits[0].terrainBefore));
            Assert.That(hits.Skip(1).Any(hit => hit.x > hits[0].x + 0.5f &&
                hit.terrainBefore >= hits[0].terrainAfter), Is.True);
        }

        [Test]
        public void ShotHitsAnotherTankAndZeroPowerDoesNotLaunch()
        {
            using var world = new PocWorld(Scenario.Volley);
            Advance(world, 70);
            world.Step(new TickCommand { tick = world.Tick, angle = 180, power = 0, fire = true });
            Assert.That(world.Weapon.Shots, Is.Empty);
            world.Step(new TickCommand { tick = world.Tick, angle = 180, power = 60, fire = true });
            Advance(world, 18);
            Assert.That(world.Trace.impacts.Count, Is.EqualTo(1));
            Assert.That(world.Trace.impacts[0].target, Is.EqualTo(1));
            Assert.That(world.Tanks[1].Body.gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void ReplayMatchesAcrossFreshWorldsAndFrameSchedules()
        {
            RunTrace first = ReplayAt(30);
            RunTrace second = ReplayAt(144);
            Assert.That(first.hashes.Count, Is.EqualTo(180));
            Assert.That(first.impacts, Is.Not.Empty);
            CollectionAssert.AreEqual(first.commands, second.commands);
            CollectionAssert.AreEqual(first.impacts, second.impacts);
            CollectionAssert.AreEqual(first.hashes, second.hashes);
        }

        private static RunTrace ReplayAt(int renderRate)
        {
            using var world = new PocWorld(Scenario.Volley);
            var clock = new FixedClock();
            void Step()
            {
                switch (world.Tick)
                {
                    case Experiment.VolleyTick: world.Step(Volley(world.Tick)); break;
                    case 94: world.Step(Drill(world.Tick, 17, 8, 23, 8)); break;
                    default: world.Step(); break;
                }
            }
            for (int frame = 0; frame < renderRate * 3; frame++)
                clock.Advance(1.0 / renderRate, Step);
            return world.Trace;
        }

        private static TickCommand Volley(int tick) =>
            new TickCommand { tick = tick, angle = 7, power = 100, volley = true };

        private static TickCommand Drill(int tick, float x, float y, float endX, float endY) =>
            new TickCommand
            {
                tick = tick, angle = 38, power = 74, drill = true,
                fromX = Coordinate(x), fromY = Coordinate(y),
                toX = Coordinate(endX), toY = Coordinate(endY)
            };

        private static int Coordinate(float value) => (int)Math.Round(value * Experiment.CoordinateScale);

        private static void Advance(PocWorld world, int count)
        {
            for (int i = 0; i < count; i++) world.Step();
        }
    }
}
