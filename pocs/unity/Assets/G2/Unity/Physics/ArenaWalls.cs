using Artillery.G2.Core;
using UnityEngine;

namespace Artillery.G2
{
    internal static class ArenaWalls
    {
        internal static void Create(Transform parent, PhysicsMaterial2D material)
        {
            const float thickness = 1f;
            const float verticalMargin = 1f;
            var size = new Vector2(thickness, Experiment.Height + 2 * verticalMargin);
            CreateWall("Left arena wall", -thickness / 2);
            CreateWall("Right arena wall", Experiment.Width + thickness / 2);

            void CreateWall(string label, float x)
            {
                var wall = new GameObject(label);
                wall.transform.SetParent(parent, false);
                wall.transform.localPosition = new Vector3(x, Experiment.Height / 2, 0);
                var collider = wall.AddComponent<BoxCollider2D>();
                collider.size = size;
                collider.sharedMaterial = material;
            }
        }
    }
}
