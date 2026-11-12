using System;
using System.Collections.Generic;
using System.Numerics;

namespace Artillery.G2.Core
{
    public readonly struct SweepHit
    {
        public readonly float Fraction;
        public readonly Vector2 Point;
        // -1 is terrain. Nonnegative values identify tanks.
        public readonly int Target;
        public SweepHit(float fraction, Vector2 point, int target)
        {
            Fraction = fraction;
            Point = point;
            Target = target;
        }
    }

    public readonly struct Impact
    {
        public readonly int ShotId, Target;
        public readonly float Fraction;
        public readonly Vector2 Point;

        public Impact(int shotId, SweepHit hit, float fraction)
        {
            ShotId = shotId;
            Target = hit.Target;
            Point = hit.Point;
            Fraction = fraction;
        }
    }

    public sealed class Shot
    {
        public int Id { get; }
        public int Owner { get; }
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public int Age { get; internal set; }
        internal Vector2 End { get; set; }

        internal Shot(int id, int owner, Vector2 position, Vector2 velocity)
        {
            Id = id;
            Owner = owner;
            Position = position;
            Velocity = velocity;
        }
    }

    public delegate bool Sweep(Vector2 from, Vector2 to, float radius, int owner, out SweepHit hit);

    public sealed class Ballistics
    {
        private const float TimeTolerance = 0.00001f;
        private readonly List<Shot> shots = new List<Shot>(Experiment.MaxShots);
        private readonly List<Impact> candidates = new List<Impact>(Experiment.MaxShots);
        private readonly List<Impact> group = new List<Impact>(Experiment.MaxShots);
        private int nextId;
        public IReadOnlyList<Shot> Shots => shots;

        public bool Launch(int owner, Vector2 pivot, int angle, int power)
        {
            if (power <= 0 || shots.Count >= Experiment.MaxShots) return false;
            Vector2 direction = Experiment.Aim(angle);
            shots.Add(new Shot(nextId++, owner, pivot + direction * Experiment.MuzzleLength,
                direction * Experiment.LaunchSpeed(power)));
            return true;
        }

        public void Step(Sweep sweep, Action<IReadOnlyList<Impact>> resolve)
        {
            foreach (Shot shot in shots)
            {
                shot.Velocity += new Vector2(0, -Experiment.Gravity * Experiment.Step);
                shot.End = shot.Position + shot.Velocity * Experiment.Step;
            }

            float cursor = 0;
            while (shots.Count > 0)
            {
                candidates.Clear();
                float first = float.PositiveInfinity;
                foreach (Shot shot in shots)
                {
                    Vector2 from = Vector2.Lerp(shot.Position, shot.End, cursor);
                    if (!sweep(from, shot.End, Experiment.ShellRadius, shot.Owner, out SweepHit hit)) continue;
                    float fraction = cursor + (1f - cursor) * Math.Clamp(hit.Fraction, 0f, 1f);
                    candidates.Add(new Impact(shot.Id, hit, fraction));
                    first = MathF.Min(first, fraction);
                }
                if (candidates.Count == 0) break;

                group.Clear();
                foreach (Impact candidate in candidates)
                    if (candidate.Fraction <= first + TimeTolerance)
                        group.Add(candidate);

                foreach (Impact impact in group)
                    shots.RemoveAt(shots.FindIndex(shot => shot.Id == impact.ShotId));

                // Re-sweep survivors after the callback updates collision geometry.
                resolve(group);
                cursor = first;
            }

            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot shot = shots[i];
                shot.Position = shot.End;
                shot.Age++;
                if (shot.Age >= Experiment.Hz * 12 || shot.Position.X < -4 ||
                    shot.Position.X > Experiment.Width + 4 || shot.Position.Y < -4)
                    shots.RemoveAt(i);
            }
        }

        public void AddTo(ref StateHash hash)
        {
            hash.Add(nextId);
            hash.Add(shots.Count);
            foreach (Shot shot in shots)
            {
                hash.Add(shot.Id);
                hash.Add(shot.Owner);
                hash.Add(shot.Position);
                hash.Add(shot.Velocity);
                hash.Add(shot.Age);
            }
        }
    }
}
