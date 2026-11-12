using System;
using System.Numerics;

namespace Artillery.G2.Core
{
    public enum Scenario { Sandbox, Volley }

    // Tuning values for G2-01 only.
    public static class Experiment
    {
        public const string Version = "g2-01.2";
        public const int Hz = 60;
        public const float Step = 1f / Hz;
        public const int VolleyTick = 45;
        public const int MaxShots = 4;
        public const float Width = 40f;
        public const float Height = 18f;
        public const float Cell = 0.25f;
        public const int ProtectedTerrainRows = 2;
        public const float Gravity = 9.81f;
        public const float ShellRadius = 0.095f;
        public const float BlastRadius = 1.65f;
        public const float DrillRadius = 0.78f;
        public const float MuzzleLength = 0.82f;
        public const float PivotHeight = 0.39f;
        public const int CoordinateScale = 100;

        public static Vector2 Aim(int angle)
        {
            angle = Math.Clamp(angle, 0, 180);
            if (angle == 0) return Vector2.UnitX;
            if (angle == 90) return Vector2.UnitY;
            if (angle == 180) return -Vector2.UnitX;
            float radians = angle * (MathF.PI / 180f);
            return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        }

        public static float LaunchSpeed(int power) => 29f * Math.Clamp(power, 0, 100) / 100f;

        public static Vector2[] Spawn(Scenario scenario) => scenario == Scenario.Volley
            ? new[] { new Vector2(12, 4.57f), new Vector2(10, 4.57f), new Vector2(8, 4.57f), new Vector2(6, 4.57f) }
            : new[] { new Vector2(7, 5.5f), new Vector2(31, 6.7f) };

        public static TerrainGrid CreateTerrain(Scenario scenario)
        {
            if (scenario == Scenario.Volley)
                return new TerrainGrid(160, 72, Cell, (x, y) => y < 16 || (x >= 68 && x < 87 && y < 47),
                    ProtectedTerrainRows);

            var grid = new TerrainGrid(160, 72, Cell, (x, y) =>
            {
                float worldX = (x + 0.5f) * Cell;
                float surface = 4.45f + 1.4f * Peak(worldX, 29f, 8f)
                    + 4.35f * Peak(worldX, 20.5f, 6.8f) + 0.75f * Peak(worldX, 2.5f, 5f);
                return (y + 0.5f) * Cell < surface;
            }, ProtectedTerrainRows);
            grid.Remove(new[]
            {
                new Cut(new Vector2(17.3f, 3.12f), new Vector2(22.8f, 3.5f), 0.68f),
                new Cut(new Vector2(19.1f, 3.6f), new Vector2(20.8f, 4.5f), 0.97f)
            });
            return grid;
        }

        private static float Peak(float x, float center, float width)
        {
            float t = MathF.Max(0, 1f - MathF.Abs(x - center) / width);
            return t * t * (3f - 2f * t);
        }
    }

    public sealed class FixedClock
    {
        private double remaining;
        public const double Interval = 1.0 / Experiment.Hz;

        public void Advance(double seconds, Action step, int maxSteps = int.MaxValue)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            remaining += seconds;
            int steps = 0;
            while (steps < maxSteps && remaining + 1e-10 >= Interval)
            {
                remaining -= Interval;
                steps++;
                step();
            }
        }

        public void Reset() => remaining = 0;
    }

    public struct StateHash
    {
        private ulong value;
        public static StateHash Begin() => new StateHash { value = 14695981039346656037UL };
        public void Add(int number)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                    value = (value ^ (byte)(number >> shift)) * 1099511628211UL;
            }
        }

        public void Add(float number) => Add(BitConverter.SingleToInt32Bits(number));
        public void Add(Vector2 vector)
        {
            Add(vector.X);
            Add(vector.Y);
        }

        public override string ToString() => value.ToString("x16");
    }
}
