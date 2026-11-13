using System;
using System.Collections.Generic;
using Artillery.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Artillery.Presentation
{
    public sealed class ArenaPresentation : IDisposable
    {
        private enum Quality { Compact, Standard, High }
        private static readonly Color[] TankColors =
        {
            ArenaArtwork.Teal, ArenaArtwork.Hex(0xcf8b75), ArenaArtwork.Hex(0xa4b6d3),
            ArenaArtwork.Hex(0xd4b885)
        };
        private readonly Camera camera;
        private readonly GameObject root;
        private readonly Material surfaceMaterial, terrainMaterial, effectMaterial;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly ArtMesh art = new ArtMesh();
        private readonly List<CellRect> rectangles = new List<CellRect>();
        private readonly ArenaHud hud = new ArenaHud();
        private readonly Dictionary<string, Transform> backdrop = new Dictionary<string, Transform>();
        private readonly Transform drillPointer, drillAnchor;
        private readonly ArenaEffects effects;
        private IArenaViewState state;
        private GameObject map;
        private Mesh[] terrainMeshes;
        private int[] revisions;
        private Transform[] hulls, turrets, shells;
        private PresentationStatus status;
        private Quality quality = Quality.Standard;
        private bool disposed;

        public ArenaPresentation(Transform parent, Camera camera)
        {
            this.camera = camera;
            root = new GameObject("Arena presentation");
            root.transform.SetParent(parent, false);
            surfaceMaterial = CreateMaterial("ArtilleryArenaSurface");
            terrainMaterial = CreateMaterial("ArtilleryArenaSurface");
            effectMaterial = CreateMaterial("ArtilleryArenaEffects");
            camera.backgroundColor = ArenaArtwork.Sky;
            ArenaArtwork.Backdrop(art, WriteBackdrop);
            art.Ring(Vector2.zero, Experiment.DrillRadius, 0.045f, ArenaArtwork.Fade(ArenaArtwork.Teal, 0.70f), 40);
            drillPointer = Write("Excavation cursor", root.transform, -1.9f, effectMaterial);
            art.Ring(Vector2.zero, Experiment.DrillRadius, 0.055f, ArenaArtwork.Amber, 40);
            drillAnchor = Write("Excavation origin", root.transform, -2f, effectMaterial);
            drillPointer.gameObject.SetActive(false);
            drillAnchor.gameObject.SetActive(false);
            effects = new ArenaEffects(CreateMesh("Pooled cosmetic effects", root.transform, -1.65f, effectMaterial, out _));
            ApplyQuality();
        }

        public void Attach(IArenaViewState nextState)
        {
            if (disposed) return;
            if (nextState == null) throw new ArgumentNullException(nameof(nextState));
            ReleaseMap();
            state = nextState;
            map = new GameObject("Arena models");
            map.transform.SetParent(root.transform, false);
            terrainMeshes = new Mesh[state.TerrainChunks];
            revisions = new int[terrainMeshes.Length];
            Array.Fill(revisions, -1);
            for (int i = 0; i < terrainMeshes.Length; i++)
            {
                terrainMeshes[i] = CreateMesh("Terrain chunk " + i, map.transform, 0, terrainMaterial, out _);
                terrainMeshes[i].MarkDynamic();
            }

            hulls = new Transform[state.TankCount];
            turrets = new Transform[hulls.Length];
            for (int i = 0; i < hulls.Length; i++)
            {
                Color color = TankColors[i % TankColors.Length];
                ArenaArtwork.Tank(art, color);
                hulls[i] = Write("Tank hull", map.transform, -0.75f, surfaceMaterial);
                ArenaArtwork.Cannon(art, color);
                turrets[i] = Write("Global cannon", map.transform, -0.85f, surfaceMaterial);
            }

            shells = new Transform[Experiment.MaxShots];
            for (int i = 0; i < shells.Length; i++)
            {
                art.Disc(Vector2.zero, 0.145f, ArenaArtwork.Hex(0x563221), 18);
                art.Disc(Vector2.zero, 0.119f, ArenaArtwork.Amber, 18);
                art.Disc(Vector2.zero, 0.058f, ArenaArtwork.Hex(0xffffdf), 16);
                shells[i] = Write("Shell", map.transform, -1.25f, surfaceMaterial);
            }
            effects.Reset();
            Refresh(default);
        }

        public void Refresh(PresentationStatus nextStatus)
        {
            if (state == null || disposed) return;
            status = nextStatus;
            RefreshTerrain();
            for (int i = 0; i < hulls.Length; i++)
            {
                TankPose pose = state.GetTank(i);
                Place(hulls[i], pose.Position);
                hulls[i].localRotation = Quaternion.Euler(0, 0, pose.Rotation);
                Place(turrets[i], pose.Pivot);
                turrets[i].localRotation = Quaternion.Euler(0, 0, pose.Angle);
            }
            for (int i = 0; i < shells.Length; i++)
            {
                bool visible = i < state.ShotCount;
                shells[i].gameObject.SetActive(visible);
                if (visible)
                    Place(shells[i], state.GetShot(i).Position);
            }
            Show(drillPointer, status.DrillPointer);
            Show(drillAnchor, status.DrillAnchor);
            effects.Update(state, (int)quality);
        }

        public void CycleQuality()
        {
            if (disposed) return;
            quality = (Quality)(((int)quality + 1) % 3);
            ApplyQuality();
        }

        public void DrawHud()
        {
            if (state != null && !disposed)
                hud.Draw(camera, state, status, QualityLabel());
        }

        private void RefreshTerrain()
        {
            float cell = state.CellSize;
            for (int i = 0; i < terrainMeshes.Length; i++)
            {
                int revision = state.ChunkRevision(i);
                if (revisions[i] == revision) continue;
                revisions[i] = revision;
                rectangles.Clear();
                state.GetTerrainRectangles(i, rectangles);
                foreach (CellRect rect in rectangles)
                    ArenaArtwork.Terrain(art, rect, cell);
                art.Upload(terrainMeshes[i]);
            }
        }

        private void ApplyQuality()
        {
            terrainMaterial.SetFloat("_TerrainDetail", quality == Quality.High ? 1 : quality == Quality.Standard ? 0.45f : 0);
            backdrop["Solar haze"].gameObject.SetActive(quality != Quality.Compact);
            backdrop["Cloud wisps"].gameObject.SetActive(quality == Quality.High);
        }

        private string QualityLabel() => quality switch
        {
            Quality.Compact => "Compacto",
            Quality.High => "Alto",
            _ => "Padrão"
        };

        private void WriteBackdrop(string label, float depth) =>
            backdrop.Add(label, Write(label, root.transform, depth, label == "Solar haze" ? effectMaterial : surfaceMaterial));

        private Transform Write(string label, Transform parent, float depth, Material material)
        {
            art.Upload(CreateMesh(label, parent, depth, material, out Transform item));
            return item;
        }

        private Mesh CreateMesh(string label, Transform parent, float depth, Material material, out Transform item)
        {
            var obj = new GameObject(label);
            item = obj.transform;
            item.SetParent(parent, false);
            item.localPosition = new Vector3(0, 0, depth);
            var mesh = new Mesh { name = label };
            meshes.Add(mesh);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }

        private static Material CreateMaterial(string name)
        {
            var shader = Resources.Load<Shader>(name);
            if (shader == null) throw new InvalidOperationException("Shader ausente em Resources: " + name);
            return new Material(shader) { name = name };
        }

        private static void Show(Transform transform, Vector2? position)
        {
            transform.gameObject.SetActive(position.HasValue);
            if (position.HasValue)
                Place(transform, position.Value);
        }

        private static void Place(Transform transform, Vector2 position) =>
            transform.localPosition = new Vector3(position.x, position.y, transform.localPosition.z);

        private void ReleaseMap()
        {
            if (map == null) return;
            map.SetActive(false);
            foreach (var filter in map.GetComponentsInChildren<MeshFilter>(true))
            {
                meshes.Remove(filter.sharedMesh);
                Object.Destroy(filter.sharedMesh);
            }
            Object.Destroy(map);
            map = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null) root.SetActive(false);
            foreach (Mesh mesh in meshes)
                Object.Destroy(mesh);
            meshes.Clear();
            Object.Destroy(surfaceMaterial);
            Object.Destroy(terrainMaterial);
            Object.Destroy(effectMaterial);
            Object.Destroy(root);
        }
    }
}
