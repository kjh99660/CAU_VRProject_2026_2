using UnityEngine;

namespace VRWebSwing
{
    // Shared by the live motor and the copied-state predictor. No scene or tracking state is mutated.
    public static class SwingMotionMath
    {
        public static Vector3 IntegrateVelocity(Vector3 velocity, Vector3 direction, bool grounded, SwingSettings settings, float dt)
        {
            if (!ComfortMath.IsFinite(dt) || dt <= 0) return velocity;
            if (grounded)
            {
                Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0, velocity.z),
                    direction * settings.groundSpeed, settings.groundAcceleration * dt);
                velocity = new Vector3(horizontal.x, Mathf.Max(velocity.y, -2), horizontal.z);
            }
            else velocity += direction * (settings.airAcceleration * dt);
            velocity += Vector3.down * (settings.gravity * dt);
            return Vector3.ClampMagnitude(velocity, settings.maxSpeed);
        }
    }
}
