using Artillery.Core;
using UnityEngine;

namespace Artillery.Presentation
{
    internal sealed class ArenaEffects
    {
        private const int MuzzleTicks = 9;
        private const int ImpactTicks = 26;
        private readonly Mesh mesh;
        private readonly ArtMesh art = new ArtMesh();
        private readonly Flash[] flashes = new Flash[Experiment.MaxShots];
        private int seenShot = -1;
        private int renderedTick = -1;
        private int renderedQuality = -1;
        private int nextFlash;

        public ArenaEffects(Mesh mesh)
        {
            this.mesh = mesh;
            mesh.MarkDynamic();
            Reset();
        }

        public void Reset()
        {
            seenShot = renderedTick = renderedQuality = -1;
            nextFlash = 0;
            for (int i = 0; i < flashes.Length; i++)
                flashes[i].Tick = -MuzzleTicks;
            mesh.Clear();
        }

        public void Update(IArenaViewState state, int quality)
        {
            if (renderedTick == state.Tick && renderedQuality == quality) return;
            renderedTick = state.Tick;
            renderedQuality = quality;

            for (int i = 0; i < state.ShotCount; i++)
            {
                ShotPose shot = state.GetShot(i);
                if (shot.Id <= seenShot) continue;
                seenShot = shot.Id;
                CaptureFlash(state, shot);
            }

            foreach (Flash flash in flashes)
            {
                int age = state.Tick - flash.Tick;
                if (age >= MuzzleTicks) continue;
                float life = 1f - age / (float)MuzzleTicks;
                Vector2 center = flash.Position + flash.Direction * ((1f - life) * 0.28f);
                art.Radial(center, 0.22f + (1f - life) * 0.24f,
                    ArenaArtwork.Fade(ArenaArtwork.Amber, 0.74f * life),
                    ArenaArtwork.Fade(ArenaArtwork.Amber, 0), quality == 0 ? 10 : 18);
                art.Line(center - flash.Direction * 0.09f, center + flash.Direction * 0.22f,
                    0.10f * life, ArenaArtwork.Fade(ArenaArtwork.Pale, life));
            }

            int newest = state.ImpactCount - 1;
            for (int index = Mathf.Max(0, newest - 7); index <= newest; index++)
            {
                ImpactRecord impact = state.GetImpact(index);
                int age = state.Tick - impact.tick;
                if (age < ImpactTicks)
                    DrawImpact(impact, age / (float)ImpactTicks, quality);
            }
            art.Upload(mesh);
        }

        private void CaptureFlash(IArenaViewState state, ShotPose shot)
        {
            Vector2 direction = shot.Velocity.normalized;
            Vector2 point = shot.Position - shot.Velocity * Experiment.Step;
            float distance = 6.25f;
            for (int i = 0; i < state.TankCount; i++)
            {
                TankPose tank = state.GetTank(i);
                float angle = tank.Angle * Mathf.Deg2Rad;
                var aim = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 muzzle = tank.Pivot + aim * Experiment.MuzzleLength;
                float candidate = (muzzle - shot.Position).sqrMagnitude;
                if (candidate >= distance || Vector2.Dot(direction, aim) < 0.5f) continue;
                distance = candidate;
                point = muzzle;
                direction = aim;
            }
            flashes[nextFlash] = new Flash { Tick = state.Tick, Position = point, Direction = direction };
            nextFlash = (nextFlash + 1) % flashes.Length;
        }

        private void DrawImpact(ImpactRecord impact, float progress, int quality)
        {
            Vector2 center = new Vector2(impact.x, impact.y);
            float life = 1f - progress;
            float wave = 0.22f + (1f - life * life) * Experiment.BlastRadius;
            int segments = quality == 0 ? 16 : 30;
            art.Radial(center, 0.44f + progress * 0.97f,
                ArenaArtwork.Fade(ArenaArtwork.Amber, 0.53f * life * life),
                ArenaArtwork.Fade(ArenaArtwork.Amber, 0), segments);
            art.Ring(center, wave, Mathf.Lerp(0.021f, 0.055f, life),
                ArenaArtwork.Fade(ArenaArtwork.Amber, 0.8f * life), segments);
            if (quality == 0) return;

            int particles = quality == 2 ? 15 : 7;
            for (int i = 0; i < particles; i++)
            {
                float angle = (i * 2.39996f + impact.shot * 0.39f) % (2f * Mathf.PI);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = 0.85f + ((i * 11 + impact.shot) % 9) * 0.19f;
                Vector2 point = center + direction * (0.13f + progress * speed)
                    + Vector2.down * (0.78f * progress * progress);
                Color spark = ArenaArtwork.Fade(i % 3 == 0 ? ArenaArtwork.Pale : ArenaArtwork.Amber, life * life);
                art.Line(point - direction * 0.17f * life, point, 0.035f * life, spark);
            }
        }

        private struct Flash
        {
            public int Tick;
            public Vector2 Position, Direction;
        }
    }
}
