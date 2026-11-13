using Artillery.Core;
using UnityEngine;

namespace Artillery
{
    public sealed class TankBody
    {
        public int Id { get; }
        public Rigidbody2D Body { get; }
        public CapsuleCollider2D Hull { get; }
        public Vector2 Pivot => Body.position + Vector2.up * Experiment.PivotHeight;
        public int Angle { get; set; } = 38;

        public TankBody(int id, Vector2 spawn, Transform parent, PhysicsMaterial2D material)
        {
            Id = id;
            var tank = new GameObject("Tank " + id);
            tank.transform.SetParent(parent, false);
            tank.transform.position = spawn;
            Body = tank.AddComponent<Rigidbody2D>();
            Body.mass = 1.7f;
            Body.gravityScale = 0;
            Body.angularDamping = 3f;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Hull = tank.AddComponent<CapsuleCollider2D>();
            Hull.direction = CapsuleDirection2D.Horizontal;
            Hull.size = new Vector2(0.94f, 0.66f);
            Hull.sharedMaterial = material;
        }

        public void ApplyForces(int drive, Collider2D ground)
        {
            if (Body.IsAwake())
                Body.AddForce(Body.mass * Experiment.Gravity * Vector2.down);
            if (drive == 0 || !Hull.IsTouching(ground)) return;
            float speedError = drive * 3.5f - Body.linearVelocity.x;
            Body.AddForce(Vector2.right * Mathf.Clamp(speedError * 35f, -46f, 46f));
        }
    }
}
