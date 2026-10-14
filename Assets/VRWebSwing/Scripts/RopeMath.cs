using UnityEngine;

namespace VRWebSwing
{
    public static class RopeMath
    {
        // A unilateral maximum-length constraint: slack rope never pulls the player.
        public static Vector3 Correction(Vector3 pivot, Vector3 anchor, float length)
        {
            Vector3 offset = pivot - anchor;
            float distance = offset.magnitude;
            return distance > length && distance > 0.0001f
                ? -offset * ((distance - length) / distance) : Vector3.zero;
        }

        public static Vector3 RemoveOutwardVelocity(Vector3 velocity, Vector3 pivot, Vector3 anchor)
        {
            Vector3 offset = pivot - anchor;
            float squared = offset.sqrMagnitude;
            if (squared < 0.000001f) return velocity;
            float outward = Vector3.Dot(velocity, offset);
            return outward > 0 ? velocity - offset * (outward / squared) : velocity;
        }
    }
}
