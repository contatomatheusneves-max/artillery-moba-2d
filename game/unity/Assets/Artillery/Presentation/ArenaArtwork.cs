using System;
using Artillery.Core;
using UnityEngine;

namespace Artillery.Presentation
{
    internal static class ArenaArtwork
    {
        internal static readonly Color Sky = Hex(0x1b3a4a);
        internal static readonly Color Teal = Hex(0x72efd3);
        internal static readonly Color Amber = Hex(0xffbd74);
        internal static readonly Color Gold = Hex(0xe7b28b);
        internal static readonly Color Pale = Hex(0xe6f3ef);

        public static void Backdrop(ArtMesh art, Action<string, float> finish)
        {
            art.Box(-100, -60, 140, 60, Hex(0x10202d), Hex(0x305565));
            finish("Sky gradient", 7);
            art.Box(-100, -2, 140, 18, Hex(0x53626b), Sky);
            finish("Horizon color", 6.95f);

            Vector2 sun = new Vector2(31.6f, 13.7f);
            art.Radial(sun, 3.7f, Fade(Gold, 0.41f), Fade(Gold, 0), 80);
            finish("Solar haze", 6.9f);

            art.Disc(sun, 1.85f, Hex(0xc9957e), 80);
            art.Disc(sun, 1.79f, Hex(0xf2c69f), 80);
            for (int stripe = 0; stripe < 5; stripe++)
            {
                float y = -1.35f + stripe * 0.18f;
                float half = Mathf.Sqrt(1.79f * 1.79f - y * y) - 0.04f;
                art.Box(sun.x - half, sun.y + y, sun.x + half, sun.y + y + 0.02f, Hex(0xcfa688));
            }
            finish("Sun disc", 6.7f);

            var farX = new[] { -15f, -3, 2, 7, 10, 13, 16, 21, 26, 32, 34, 40, 50 };
            Ridge(art, farX, new[] { 7f, 9.7f, 8.1f, 10.9f, 9.1f, 13f, 8.6f, 10.3f, 7.3f, 8.9f, 7.7f, 10.5f, 8f },
                Hex(0x283b49), Hex(0x596973), Hex(0x778080));
            finish("Distant ridge", 6.4f);

            var nearX = new[] { -20f, -1, 4, 8, 12, 17, 23, 29, 33, 38, 43, 57 };
            Ridge(art, nearX, new[] { 5f, 7.5f, 6.2f, 8f, 5.7f, 6.5f, 4.2f, 5.6f, 5.05f, 7.8f, 6.5f, 6f },
                Hex(0x1f3745), Hex(0x3b5760), Hex(0x4b666b));
            finish("Near ridge", 5.8f);

            for (int i = 0; i < 7; i++)
            {
                float x = -0.5f + i * 6.3f;
                float y = 11.4f + (i * 7 % 5) * 0.93f;
                art.Line(new Vector2(x, y), new Vector2(x + 1.5f + i * 0.13f, y), 0.025f, Hex(0x849491));
                art.Line(new Vector2(x + 0.6f, y - 0.14f), new Vector2(x + 2.2f, y - 0.14f), 0.022f, Hex(0x617d80));
            }
            finish("Cloud wisps", 5.3f);

            art.Box(-1.0f, -0.22f, 41f, 0, Hex(0x1b2a34));
            art.Box(0, -0.04f, 40f, 0.025f, Hex(0x727d72));
            for (int i = 0; i <= 40; i += 2)
            {
                float extent = i % 10 == 0 ? 0.18f : 0.08f;
                art.Box(i - 0.025f, -extent, i + 0.025f, 0, Hex(0xb19e7a));
            }
            foreach (float edge in new[] { 0f, Experiment.Width })
            {
                art.Box(edge - 0.028f, 0, edge + 0.028f, 17.5f, Hex(0x819190), Hex(0x435d66));
                for (int y = 2; y < 18; y += 2)
                    art.Box(edge - 0.11f, y, edge + 0.11f, y + 0.035f, Hex(0x687f80));
            }
            finish("Arena boundaries", 0.5f);
        }

        private static void Ridge(ArtMesh art, float[] xs, float[] ys, Color lower, Color upper, Color lit)
        {
            for (int i = 1; i < xs.Length; i++)
            {
                var left = new Vector2(xs[i - 1], ys[i - 1]);
                var right = new Vector2(xs[i], ys[i]);
                art.Quad(new Vector2(left.x, -8), new Vector2(right.x, -8), right, left, lower, upper);
                if (right.y < left.y)
                {
                    var seam = new Vector2(Mathf.Lerp(left.x, right.x, 0.58f), left.y - 2.6f);
                    art.Polygon(lit, left, right, seam);
                }
            }
        }

        public static void Tank(ArtMesh art, Color accent)
        {
            Color outline = Hex(0x101b22);
            Color tread = Hex(0x303c44);
            Color dim = Color.Lerp(Hex(0x192d36), accent, 0.35f);
            Color mid = Color.Lerp(Hex(0x263a45), accent, 0.63f);
            Color bright = Color.Lerp(accent, Pale, 0.32f);
            art.Polygon(outline, P(-0.48f, -0.21f), P(-0.41f, -0.32f), P(0.40f, -0.32f),
                P(0.48f, -0.22f), P(0.44f, -0.07f), P(-0.43f, -0.07f));
            art.Polygon(tread, P(-0.43f, -0.20f), P(-0.37f, -0.28f), P(0.36f, -0.28f),
                P(0.43f, -0.20f), P(0.39f, -0.11f), P(-0.39f, -0.11f));
            art.Line(P(-0.38f, -0.288f), P(0.38f, -0.288f), 0.016f, Hex(0x59666b));
            for (int i = -2; i <= 2; i++)
            {
                Vector2 wheel = P(i * 0.155f, -0.205f);
                art.Disc(wheel, 0.073f, outline, 12);
                art.Ring(wheel, 0.058f, 0.013f, Hex(0x71848b), 12);
                art.Disc(wheel, 0.026f, Hex(0x536670), 10);
                art.Disc(wheel, 0.010f, Pale, 8);
            }
            art.Polygon(outline, P(-0.49f, -0.09f), P(0.49f, -0.09f), P(0.37f, 0.16f),
                P(-0.21f, 0.19f), P(-0.43f, 0.09f));
            art.Quad(P(-0.45f, -0.06f), P(0.45f, -0.06f), P(0.33f, 0.13f),
                P(-0.34f, 0.13f), dim, mid);
            art.Polygon(bright, P(-0.34f, 0.13f), P(0.33f, 0.13f), P(0.26f, 0.17f),
                P(-0.21f, 0.17f));
            art.Polygon(outline, P(-0.23f, 0.135f), P(0.25f, 0.135f), P(0.19f, 0.29f),
                P(-0.08f, 0.31f), P(-0.20f, 0.27f));
            art.Polygon(mid, P(-0.18f, 0.155f), P(0.21f, 0.155f), P(0.16f, 0.26f),
                P(-0.09f, 0.29f), P(-0.17f, 0.25f));
            art.Line(P(-0.125f, 0.269f), P(0.1f, 0.269f), 0.022f, bright);
            art.Box(-0.35f, 0.015f, -0.23f, 0.065f, Hex(0x384850));
            art.Box(0.25f, 0.035f, 0.355f, 0.073f, accent);
            art.Line(P(-0.28f, 0.17f), P(-0.33f, 0.48f), 0.018f, tread);
            art.Disc(P(-0.33f, 0.48f), 0.022f, accent, 8);
        }

        public static void Cannon(ArtMesh art, Color accent)
        {
            float length = Experiment.MuzzleLength;
            art.Box(-0.055f, -0.083f, length, 0.085f, Hex(0x14242c));
            art.Box(-0.025f, -0.055f, length - 0.045f, 0.055f, Hex(0x3d515a), Hex(0x96a8a7));
            art.Box(0.17f, 0.049f, length - 0.06f, 0.057f, Pale);
            art.Box(0.33f, -0.068f, 0.41f, 0.069f, Hex(0x475863));
            art.Box(length - 0.12f, -0.094f, length, 0.096f, Hex(0x17262f));
            art.Box(length - 0.097f, -0.063f, length - 0.023f, 0.066f, Hex(0x718789));
            art.Box(length - 0.022f, -0.060f, length, 0.062f, accent);
            art.Disc(Vector2.zero, 0.123f, Hex(0x14242c), 18);
            art.Disc(Vector2.zero, 0.094f, accent, 18);
            art.Disc(Vector2.zero, 0.055f, Hex(0x384b52), 14);
            art.Disc(Vector2.zero, 0.025f, Pale, 10);
        }

        public static void Terrain(ArtMesh art, CellRect rect, float size)
        {
            float bottom = rect.Y * size;
            float top = (rect.Y + rect.Height) * size;
            float left = rect.X * size;
            float right = (rect.X + rect.Width) * size;
            float foundation = Experiment.ProtectedTerrainRows * size;
            if (bottom < foundation)
                art.Box(left, bottom, right, Mathf.Min(top, foundation), Hex(0x364554), Hex(0x56636a));
            if (top > foundation)
            {
                float start = Mathf.Max(bottom, foundation);
                art.Box(left, start, right, top, Soil(start), Soil(top));
            }
        }

        private static Color Soil(float y) =>
            Color.Lerp(Hex(0x293d46), Hex(0x819580), Mathf.Clamp01(y / 9.6f));

        public static Color Fade(Color source, float alpha) => new Color(source.r, source.g, source.b, alpha);
        private static Vector2 P(float x, float y) => new Vector2(x, y);
        public static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
