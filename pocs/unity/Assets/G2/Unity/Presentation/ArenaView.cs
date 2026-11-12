using System;
using System.Collections.Generic;
using Artillery.G2.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Artillery.G2
{
    internal sealed class ArenaView : IDisposable
    {
        private static readonly Color Sky = Hex(0x152838);
        private static readonly Color Amber = Hex(0xffbf68);
        private static readonly Color Teal = Hex(0x77e2cc);
        private readonly GameObject root;
        private readonly Material material;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Mesh[] terrain;
        private readonly int[] revisions;
        private readonly List<CellRect> rectangles = new List<CellRect>();
        private readonly MeshData data = new MeshData();
        private readonly Transform[] hulls, turrets, shells, impacts;
        private readonly Transform drillCursor, drillAnchor;
        private readonly PocWorld world;
        private readonly Camera camera;

        public ArenaView(PocWorld world, Transform parent, Camera camera)
        {
            this.world = world;
            this.camera = camera;
            root = new GameObject("G2-01 visuals");
            root.transform.SetParent(parent, false);
            var shader = Resources.Load<Shader>("G2Flat");
            if (shader == null) throw new InvalidOperationException("The G2Flat shader is missing.");
            material = new Material(shader);
            camera.backgroundColor = Sky;
            BuildBackdrop();
            var grid = world.Terrain.Grid;
            terrain = new Mesh[grid.ChunksX * grid.ChunksY];
            revisions = new int[terrain.Length];
            Array.Fill(revisions, -1);
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = Create("Terrain mesh " + i, 0, out _);

            hulls = new Transform[world.Tanks.Count];
            turrets = new Transform[hulls.Length];
            for (int i = 0; i < hulls.Length; i++)
            {
                Color accent = i == 0 ? Teal : Hex(0x9eaecb);
                data.Box(-0.47f, -0.32f, 0.47f, 0.05f, Hex(0x1a2530));
                for (int wheel = -1; wheel <= 1; wheel++)
                    data.Disc(new Vector2(wheel * 0.26f, -0.15f), 0.10f, Hex(0x657584), 12);
                data.Quad(new Vector2(-0.45f, -0.06f), new Vector2(0.45f, -0.06f),
                    new Vector2(0.30f, 0.26f), new Vector2(-0.28f, 0.26f), accent);
                Write("Tank hull", -0.8f, out hulls[i]);

                data.Box(0, -0.065f, Experiment.MuzzleLength, 0.065f, accent);
                data.Box(0.69f, -0.087f, Experiment.MuzzleLength, 0.087f, Hex(0xeff7f1));
                data.Disc(Vector2.zero, 0.135f, Hex(0x293b46), 18);
                Write("Global cannon", -0.9f, out turrets[i]);
            }

            shells = new Transform[Experiment.MaxShots];
            impacts = new Transform[shells.Length];
            for (int i = 0; i < shells.Length; i++)
            {
                data.Disc(Vector2.zero, 0.14f, Amber, 16);
                data.Disc(Vector2.zero, 0.075f, Hex(0xfff0cf), 12);
                Write("Shell", -1.3f, out shells[i]);
                data.Ring(1f, 0.07f, Amber, 36);
                Write("Impact", -1.2f, out impacts[i]);
            }
            data.Ring(Experiment.DrillRadius, 0.042f, Teal, 40);
            Write("Drill cursor", -1.4f, out drillCursor);
            data.Ring(Experiment.DrillRadius, 0.065f, Amber, 40);
            Write("Drill anchor", -1.5f, out drillAnchor);
        }

        private void BuildBackdrop()
        {
            data.Box(-25, -10, 65, 35, Sky, Hex(0x254861));
            Write("Sky", 8, out _);

            data.Disc(new Vector2(33, 14.1f), 2.1f, Hex(0x52768a), 64);
            data.Disc(new Vector2(33, 14.1f), 1.65f, Hex(0xd9ba8a), 64);
            Write("Sun", 7, out _);

            var ridge = new[] { -10f, -4f, 3f, 10f, 16f, 23f, 29f, 36f, 44f, 52f };
            var elevations = new[] { 5f, 7.6f, 9.7f, 6.6f, 11f, 7.2f, 10.4f, 7.6f, 11f, 7f };
            for (int i = 1; i < ridge.Length; i++)
                data.Quad(new Vector2(ridge[i - 1], -5), new Vector2(ridge[i], -5),
                    new Vector2(ridge[i], elevations[i]), new Vector2(ridge[i - 1], elevations[i - 1]),
                    Hex(0x203949), Hex(0x365668));
            Write("Distant ridge", 6, out _);

            for (int i = 0; i < 6; i++)
            {
                float x = 2.5f + i * 6.1f;
                float y = 11.8f + ((i * 7) % 5) * 0.85f;
                data.Box(x, y, x + 1.6f + i * 0.17f, y + 0.045f, Hex(0x567080));
            }
            Write("Cloud streaks", 5, out _);
        }

        public void Refresh(Vector2? pointer, Vector2? anchor)
        {
            camera.orthographicSize = Mathf.Max(9.6f, 21f / Mathf.Max(0.1f, camera.aspect));
            var grid = world.Terrain.Grid;
            for (int i = 0; i < terrain.Length; i++)
            {
                int revision = grid.ChunkRevision(i);
                if (revisions[i] == revision) continue;
                grid.GetRectangles(i, rectangles);
                foreach (CellRect r in rectangles)
                {
                    float bottom = r.Y * grid.CellSize;
                    float top = (r.Y + r.Height) * grid.CellSize;
                    data.Box(r.X * grid.CellSize, bottom, (r.X + r.Width) * grid.CellSize, top,
                        Earth(bottom), Earth(top));
                }
                data.Apply(terrain[i]);
                revisions[i] = revision;
            }

            for (int i = 0; i < hulls.Length; i++)
            {
                TankBody tank = world.Tanks[i];
                Place(hulls[i], tank.Body.position);
                hulls[i].localRotation = Quaternion.Euler(0, 0, tank.Body.rotation);
                Place(turrets[i], tank.Pivot);
                turrets[i].localRotation = Quaternion.Euler(0, 0, tank.Angle);
            }
            var active = world.Weapon.Shots;
            for (int i = 0; i < shells.Length; i++)
                Show(shells[i], i < active.Count ? active[i].Position.Unity() : (Vector2?)null);

            var history = world.Trace.impacts;
            for (int i = 0; i < impacts.Length; i++)
            {
                int index = history.Count - i - 1;
                int age = index >= 0 ? world.Tick - history[index].tick : int.MaxValue;
                if (age <= 22)
                {
                    var impact = history[index];
                    Show(impacts[i], new Vector2(impact.x, impact.y));
                    impacts[i].localScale = Vector3.one * Mathf.Lerp(0.16f, Experiment.BlastRadius, age / 22f);
                }
                else
                    Show(impacts[i], null);
            }

            Show(drillCursor, pointer);
            Show(drillAnchor, anchor);
        }

        private static Color Earth(float y) => Color.Lerp(Hex(0x2c3644), Hex(0x8ba39a), Mathf.Clamp01(y / 11f));

        private static void Show(Transform item, Vector2? position)
        {
            item.gameObject.SetActive(position.HasValue);
            if (position.HasValue) Place(item, position.Value);
        }

        private static void Place(Transform item, Vector2 position) =>
            item.localPosition = new Vector3(position.x, position.y, item.localPosition.z);

        private Mesh Create(string label, float depth, out Transform item)
        {
            var obj = new GameObject(label);
            item = obj.transform;
            item.SetParent(root.transform, false);
            item.localPosition = new Vector3(0, 0, depth);
            var mesh = new Mesh { name = label };
            meshes.Add(mesh);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            return mesh;
        }

        private void Write(string label, float depth, out Transform item) => data.Apply(Create(label, depth, out item));

        public void Dispose()
        {
            if (root != null) root.SetActive(false);
            foreach (Mesh mesh in meshes) Object.Destroy(mesh);
            Object.Destroy(material);
            Object.Destroy(root);
        }

        internal static Color Hex(int rgb) =>
            new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }

    internal sealed class MeshData
    {
        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> indices = new List<int>();

        public void Box(float left, float bottom, float right, float top, Color color) =>
            Box(left, bottom, right, top, color, color);

        public void Box(float left, float bottom, float right, float top, Color lower, Color upper) =>
            Quad(new Vector2(left, bottom), new Vector2(right, bottom),
                new Vector2(right, top), new Vector2(left, top), lower, upper);

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color) =>
            Quad(a, b, c, d, color, color);

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color lower, Color upper)
        {
            int start = positions.Count;
            Add(a, lower);
            Add(b, lower);
            Add(c, upper);
            Add(d, upper);
            Triangle(start, start + 1, start + 2);
            Triangle(start, start + 2, start + 3);
        }

        public void Disc(Vector2 center, float radius, Color color, int sides)
        {
            int start = positions.Count;
            Add(center, color);
            for (int i = 0; i < sides; i++)
            {
                Add(center + Direction(i, sides) * radius, color);
                Triangle(start, start + 1 + i, start + 1 + (i + 1) % sides);
            }
        }

        public void Ring(float radius, float thickness, Color color, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                Vector2 a = Direction(i, sides);
                Vector2 b = Direction(i + 1, sides);
                Quad(a * radius, b * radius, b * (radius - thickness), a * (radius - thickness), color);
            }
        }

        private static Vector2 Direction(int i, int sides)
        {
            float angle = 2f * Mathf.PI * i / sides;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void Add(Vector2 p, Color color)
        {
            positions.Add(p);
            colors.Add(color);
        }

        private void Triangle(int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        public void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            positions.Clear();
            colors.Clear();
            indices.Clear();
        }
    }
}
