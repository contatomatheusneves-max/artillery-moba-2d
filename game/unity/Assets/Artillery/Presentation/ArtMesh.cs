using System.Collections.Generic;
using UnityEngine;

namespace Artillery.Presentation
{
    internal sealed class ArtMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public void Box(float left, float bottom, float right, float top, Color color) =>
            Box(left, bottom, right, top, color, color);

        public void Box(float left, float bottom, float right, float top, Color lower, Color upper) =>
            Quad(new Vector2(left, bottom), new Vector2(right, bottom),
                new Vector2(right, top), new Vector2(left, top), lower, upper);

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color) =>
            Quad(a, b, c, d, color, color);

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color lower, Color upper)
        {
            int index = vertices.Count;
            Add(a, lower);
            Add(b, lower);
            Add(c, upper);
            Add(d, upper);
            Triangle(index, index + 1, index + 2);
            Triangle(index, index + 2, index + 3);
        }

        public void Polygon(Color color, params Vector2[] outline)
        {
            int index = vertices.Count;
            foreach (Vector2 point in outline)
                Add(point, color);
            for (int i = 1; i + 1 < outline.Length; i++)
                Triangle(index, index + i, index + i + 1);
        }

        public void Disc(Vector2 center, float radius, Color color, int segments) =>
            Radial(center, radius, color, color, segments);

        public void Radial(Vector2 center, float radius, Color inner, Color outer, int segments)
        {
            int index = vertices.Count;
            Add(center, inner);
            for (int i = 0; i < segments; i++)
            {
                Add(center + radius * Direction(i, segments), outer);
                Triangle(index, index + 1 + i, index + 1 + (i + 1) % segments);
            }
        }

        public void Ring(Vector2 center, float radius, float width, Color color, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = Direction(i, segments);
                Vector2 b = Direction(i + 1, segments);
                Quad(center + a * radius, center + b * radius,
                    center + b * (radius - width), center + a * (radius - width), color);
            }
        }

        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude <= 0.000001f) return;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
            Quad(a - side, b - side, b + side, a + side, color);
        }

        private static Vector2 Direction(int index, int segments)
        {
            float angle = index * (2f * Mathf.PI / segments);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
        }

        private void Add(Vector2 point, Color color)
        {
            vertices.Add(point);
            colors.Add(color);
        }

        private void Triangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
