using System.Collections.Generic;
using Artillery.Core;
using UnityEngine;

namespace Artillery
{
    public interface IArenaViewState
    {
        Scenario Scenario { get; }
        int Tick { get; }
        int Power { get; }
        int PeakShots { get; }
        int TankCount { get; }
        int ShotCount { get; }
        int TerrainVersion { get; }
        int SolidCells { get; }
        int TerrainChunks { get; }
        float CellSize { get; }
        int ChunkRevision(int chunk);
        void GetTerrainRectangles(int chunk, List<CellRect> result);
        TankPose GetTank(int index);
        ShotPose GetShot(int index);
        int ImpactCount { get; }
        ImpactRecord GetImpact(int index);
    }

    public readonly struct TankPose
    {
        public readonly Vector2 Position, Pivot;
        public readonly float Rotation;
        public readonly int Angle;
        public readonly bool Grounded;

        public TankPose(Vector2 position, Vector2 pivot, float rotation, int angle, bool grounded)
        {
            Position = position;
            Pivot = pivot;
            Rotation = rotation;
            Angle = angle;
            Grounded = grounded;
        }
    }

    public readonly struct ShotPose
    {
        public readonly int Id;
        public readonly Vector2 Position, Velocity;

        public ShotPose(int id, Vector2 position, Vector2 velocity)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
        }
    }
}
