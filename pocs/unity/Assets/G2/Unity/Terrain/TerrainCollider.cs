using System;
using System.Collections.Generic;
using Artillery.G2.Core;
using UnityEngine;

namespace Artillery.G2
{
    public sealed class TerrainCollider
    {
        private readonly Transform root;
        private readonly PolygonCollider2D[] sources;
        private readonly int[] revisions;
        private readonly List<CellRect> rectangles = new List<CellRect>();
        private readonly Vector2[] path = new Vector2[4];

        public TerrainGrid Grid { get; }
        public CompositeCollider2D Collider { get; }

        public TerrainCollider(TerrainGrid grid, Transform root)
        {
            this.root = root;
            Grid = grid;
            var body = root.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            Collider = root.gameObject.AddComponent<CompositeCollider2D>();
            Collider.geometryType = CompositeCollider2D.GeometryType.Polygons;
            Collider.generationType = CompositeCollider2D.GenerationType.Manual;
            sources = new PolygonCollider2D[grid.ChunksX * grid.ChunksY];
            revisions = new int[sources.Length];
            Array.Fill(revisions, -1);
            Synchronize();
        }

        public void Synchronize()
        {
            bool changed = false;
            for (int chunk = 0; chunk < sources.Length; chunk++)
            {
                int revision = Grid.ChunkRevision(chunk);
                if (revisions[chunk] == revision) continue;
                revisions[chunk] = revision;
                Grid.GetRectangles(chunk, rectangles);
                var source = sources[chunk];
                if (rectangles.Count == 0)
                {
                    if (source != null) source.enabled = false;
                    changed = true;
                    continue;
                }

                if (source == null)
                {
                    var child = new GameObject("Collision chunk " + chunk);
                    child.transform.SetParent(root, false);
                    source = child.AddComponent<PolygonCollider2D>();
                    source.compositeOperation = Collider2D.CompositeOperation.Merge;
                    sources[chunk] = source;
                }
                source.pathCount = rectangles.Count;
                for (int i = 0; i < rectangles.Count; i++)
                {
                    CellRect rect = rectangles[i];
                    float left = rect.X * Grid.CellSize;
                    float right = (rect.X + rect.Width) * Grid.CellSize;
                    float bottom = rect.Y * Grid.CellSize;
                    float top = (rect.Y + rect.Height) * Grid.CellSize;
                    path[0] = new Vector2(left, bottom);
                    path[1] = new Vector2(right, bottom);
                    path[2] = new Vector2(right, top);
                    path[3] = new Vector2(left, top);
                    source.SetPath(i, path);
                }
                source.enabled = true;
                changed = true;
            }
            if (!changed) return;
            Collider.GenerateGeometry();
            Physics2D.SyncTransforms();
        }
    }
}
