using System;
using System.Collections.Generic;
using System.Numerics;

namespace Artillery.Core
{
    public readonly struct Cut
    {
        public readonly Vector2 From, To;
        public readonly float Radius;

        public Cut(Vector2 from, Vector2 to, float radius)
        {
            From = from;
            To = to;
            Radius = radius;
        }
    }

    public readonly struct CellRect
    {
        public readonly int X, Y, Width, Height;
        public CellRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public sealed class TerrainGrid
    {
        public const int ChunkSize = 16;
        private readonly bool[] solid;
        private readonly ChunkGeometry[] chunks;
        private int hashedVersion = -1;
        private ulong occupancyDigest;

        private sealed class ChunkGeometry
        {
            public readonly List<CellRect> Rectangles = new List<CellRect>();
            public int Revision;
            public int CachedRevision = -1;
            public ulong Digest;
        }

        public int Width { get; }
        public int Height { get; }
        public int ProtectedRows { get; }
        public float CellSize { get; }
        public int ChunksX { get; }
        public int ChunksY { get; }
        public int Version { get; private set; }
        public int SolidCount { get; private set; }

        public TerrainGrid(int width, int height, float cellSize, Func<int, int, bool> fill, int protectedRows = 0)
        {
            if (width <= 0 || height <= 0 || cellSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (protectedRows < 0 || protectedRows > height)
                throw new ArgumentOutOfRangeException(nameof(protectedRows));
            Width = width;
            Height = height;
            ProtectedRows = protectedRows;
            CellSize = cellSize;
            ChunksX = (width + ChunkSize - 1) / ChunkSize;
            ChunksY = (height + ChunkSize - 1) / ChunkSize;
            solid = new bool[width * height];
            chunks = new ChunkGeometry[ChunksX * ChunksY];
            for (int chunk = 0; chunk < chunks.Length; chunk++)
                chunks[chunk] = new ChunkGeometry();

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (y >= ProtectedRows && !fill(x, y)) continue;
                solid[x + y * width] = true;
                SolidCount++;
            }
        }

        public int ChunkRevision(int chunk) => chunks[chunk].Revision;

        public bool IsSolid(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height && solid[x + y * Width];

        public bool IsSolid(Vector2 point) =>
            IsSolid((int)MathF.Floor(point.X / CellSize), (int)MathF.Floor(point.Y / CellSize));

        public int Remove(IReadOnlyList<Cut> cuts)
        {
            int before = SolidCount;
            foreach (Cut cut in cuts)
                RemoveCells(cut, Version + 1);
            if (before != SolidCount)
                Version++;
            return before - SolidCount;
        }

        private void RemoveCells(Cut cut, int revision)
        {
            if (cut.Radius <= 0) return;
            int minX = Math.Max(0, (int)MathF.Floor((MathF.Min(cut.From.X, cut.To.X) - cut.Radius) / CellSize));
            int maxX = Math.Min(Width - 1, (int)MathF.Floor((MathF.Max(cut.From.X, cut.To.X) + cut.Radius) / CellSize));
            int minY = Math.Max(ProtectedRows, (int)MathF.Floor((MathF.Min(cut.From.Y, cut.To.Y) - cut.Radius) / CellSize));
            int maxY = Math.Min(Height - 1, (int)MathF.Floor((MathF.Max(cut.From.Y, cut.To.Y) + cut.Radius) / CellSize));
            Vector2 axis = cut.To - cut.From;
            float lengthSquared = axis.LengthSquared();
            float radiusSquared = cut.Radius * cut.Radius;

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int index = x + y * Width;
                if (!solid[index]) continue;
                var center = new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);
                float along = lengthSquared > 0
                    ? Math.Clamp(Vector2.Dot(center - cut.From, axis) / lengthSquared, 0f, 1f)
                    : 0;
                if (Vector2.DistanceSquared(center, cut.From + along * axis) > radiusSquared) continue;
                solid[index] = false;
                chunks[x / ChunkSize + y / ChunkSize * ChunksX].Revision = revision;
                SolidCount--;
            }
        }

        public void GetRectangles(int chunk, List<CellRect> result)
        {
            ChunkGeometry geometry = GetGeometry(chunk);
            result.Clear();
            result.AddRange(geometry.Rectangles);
        }

        private ChunkGeometry GetGeometry(int chunk)
        {
            ChunkGeometry geometry = chunks[chunk];
            if (geometry.CachedRevision == geometry.Revision)
                return geometry;

            RebuildRectangles(chunk, geometry.Rectangles);
            // Canonical rectangles cover every solid cell once, in a stable order.
            var hash = StateHash.Begin();
            hash.Add(geometry.Rectangles.Count);
            foreach (CellRect rect in geometry.Rectangles)
            {
                hash.Add(rect.X);
                hash.Add(rect.Y);
                hash.Add(rect.Width);
                hash.Add(rect.Height);
            }
            geometry.Digest = hash.Digest;
            geometry.CachedRevision = geometry.Revision;
            return geometry;
        }

        private void RebuildRectangles(int chunk, List<CellRect> result)
        {
            result.Clear();
            int startX = chunk % ChunksX * ChunkSize;
            int startY = chunk / ChunksX * ChunkSize;
            int endX = Math.Min(startX + ChunkSize, Width);
            int endY = Math.Min(startY + ChunkSize, Height);
            // A 16x16 chunk fits in 16 row masks. Merge identical-width runs vertically.
            Span<int> used = stackalloc int[ChunkSize];
            used.Clear();
            for (int y = startY; y < endY; y++)
            for (int x = startX; x < endX; x++)
            {
                if (!IsSolid(x, y) || (used[y - startY] & (1 << (x - startX))) != 0) continue;
                int right = x;
                while (right < endX && IsSolid(right, y) && (used[y - startY] & (1 << (right - startX))) == 0)
                    right++;
                int mask = ((1 << (right - x)) - 1) << (x - startX);
                int top = y + 1;
                while (top < endY && (used[top - startY] & mask) == 0 && RowIsSolid(x, right, top))
                    top++;
                for (int row = y; row < top; row++)
                    used[row - startY] |= mask;
                result.Add(new CellRect(x, y, right - x, top - y));
            }
        }

        private bool RowIsSolid(int fromX, int toX, int y)
        {
            for (int x = fromX; x < toX; x++)
                if (!IsSolid(x, y)) return false;
            return true;
        }

        public void AddTo(ref StateHash hash)
        {
            if (hashedVersion != Version)
            {
                var occupancy = StateHash.Begin();
                for (int chunk = 0; chunk < chunks.Length; chunk++)
                    occupancy.Add(GetGeometry(chunk).Digest);
                occupancyDigest = occupancy.Digest;
                hashedVersion = Version;
            }

            hash.Add(Width);
            hash.Add(Height);
            hash.Add(CellSize);
            hash.Add(ProtectedRows);
            hash.Add(Version);
            hash.Add(occupancyDigest);
        }
    }
}
