using System.Collections.Generic;
using Artillery.Core;
using UnityEngine;
using NVector = System.Numerics.Vector2;

namespace Artillery
{
    internal sealed class ShotQueries
    {
        private readonly PhysicsScene2D scene;
        private readonly Dictionary<Collider2D, int> targets = new Dictionary<Collider2D, int>();
        private readonly List<RaycastHit2D> casts = new List<RaycastHit2D>(8);
        private readonly List<Collider2D> overlaps = new List<Collider2D>(8);
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };

        public ShotQueries(PhysicsScene2D scene, Collider2D ground, IReadOnlyList<TankBody> tanks)
        {
            this.scene = scene;
            targets.Add(ground, -1);
            foreach (TankBody tank in tanks)
                targets.Add(tank.Hull, tank.Id);
        }

        public bool Sweep(NVector from, NVector to, float radius, int owner, out SweepHit result)
        {
            Vector2 start = from.Unity();
            scene.OverlapCircle(start, radius, filter, overlaps);
            int bestTarget = int.MaxValue;
            Collider2D best = null;
            foreach (Collider2D collider in overlaps)
            {
                if (!targets.TryGetValue(collider, out int target) || target == owner || target >= bestTarget) continue;
                bestTarget = target;
                best = collider;
            }
            if (best != null)
            {
                result = new SweepHit(0, best.ClosestPoint(start).Core(), bestTarget);
                return true;
            }

            Vector2 delta = to.Unity() - start;
            float distance = delta.magnitude;
            float nearest = float.PositiveInfinity;
            Vector2 point = default;
            if (distance > 0)
            {
                scene.CircleCast(start, radius, delta / distance, distance, filter, casts);
                foreach (RaycastHit2D hit in casts)
                {
                    if (!targets.TryGetValue(hit.collider, out int target) || target == owner) continue;
                    if (hit.fraction > nearest || (hit.fraction == nearest && target >= bestTarget)) continue;
                    nearest = hit.fraction;
                    bestTarget = target;
                    point = hit.point;
                }
            }

            result = new SweepHit(nearest, point.Core(), bestTarget);
            return bestTarget != int.MaxValue;
        }
    }

    internal static class Vectors
    {
        internal static Vector2 Unity(this NVector vector) => new Vector2(vector.X, vector.Y);
        internal static NVector Core(this Vector2 vector) => new NVector(vector.x, vector.y);
    }
}
