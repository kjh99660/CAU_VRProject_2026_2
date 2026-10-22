using UnityEngine;

namespace VRWebSwing
{
    public static class RopeMath
    {
        public static float FloorLimitedLength(float initialLength, float anchorY, float floorY, float pivotHeight, float clearance, float minLength)
        {
            float allowed = anchorY - floorY - Mathf.Max(0, pivotHeight) - Mathf.Max(0, clearance);
            return Mathf.Clamp(allowed, Mathf.Min(minLength, initialLength), initialLength);
        }

        // Reserve most of the correction budget for normal swing constraints, including under unusual dt/settings.
        public static float ReelLength(float current, float target, SwingSettings settings, float dt, float remainingCorrection)
        {
            if (!settings.autoShortenForFloor || !ComfortMath.IsFinite(dt) || dt <= 0) return current;
            float step = Mathf.Min(Mathf.Max(0, settings.floorReelSpeed) * dt,
                Mathf.Max(0, remainingCorrection) * 0.25f);
            return Mathf.MoveTowards(current, Mathf.Min(current, target), step);
        }

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
