using Unity.Profiling;
using UnityEngine;

namespace VRWebSwing
{
    public struct SwingPredictionResult
    {
        public float peakSpeed, peakAcceleration, turnDegrees, obstacleDistance, maxCorrection;
        public bool obstacleHit;
        public int pointCount;
    }

    public sealed class SwingPrediction
    {
        static readonly ProfilerMarker Marker = new ProfilerMarker("VRWebSwing.Comfort.Predict");
        public readonly Vector3[] points = new Vector3[33];
        public SwingPredictionResult Result { get; private set; }
        public void Clear() { Result = default; }
        public SwingPredictionResult Calculate(SwingMotor motor, ComfortTuning tuning)
        {
            using (Marker.Auto())
            {
                var result = new SwingPredictionResult { peakSpeed = motor.Velocity.magnitude, obstacleDistance = 10000, pointCount = 1 };
                Vector3 position = motor.transform.position, velocity = motor.Velocity, initialVelocity = velocity;
                Vector3 direction = motor.SteeringDirection; // assume current virtual steering direction is held
                var settings = motor.settings;
                bool attached = motor.rope.Attached, grounded = motor.Grounded;
                Vector3 anchor = motor.rope.Anchor;
                float length = motor.rope.Length;
                float targetLength = motor.rope.TargetLength;
                int steps = Mathf.Clamp(tuning.predictionSteps, 4, 32);
                float dt = Mathf.Clamp(tuning.horizon, 0.2f, 0.5f) / steps;
                int casts = Mathf.Clamp(tuning.obstacleCasts, 0, 12);
                int castStride = casts > 0 ? Mathf.CeilToInt((float)steps / casts) : steps + 1;
                Vector3 lastCastPosition = position;
                points[0] = position;
                for (int i = 0; i < steps; ++i)
                {
                    Vector3 beforeVelocity = velocity;
                    velocity = SwingMotionMath.IntegrateVelocity(velocity, direction, grounded, settings, dt);
                    int substeps = Mathf.Max(1, Mathf.CeilToInt(velocity.magnitude * dt / Mathf.Max(0.05f, settings.collisionStep)));
                    float correctionBudget = 0;
                    for (int j = 0; j < substeps; ++j)
                    {
                        position += velocity * (dt / substeps);
                        if (grounded) { position.y = motor.transform.position.y; velocity.y = 0; }
                        if (!attached) continue;
                        Vector3 pivot = position + Vector3.up * settings.pivotHeight;
                        Vector3 correction = RopeMath.Correction(pivot, anchor, length);
                        correctionBudget += correction.magnitude;
                        result.maxCorrection = Mathf.Max(result.maxCorrection, correctionBudget);
                        if (correctionBudget > settings.maxCorrection) { attached = false; continue; }
                        position += correction;
                        pivot += correction;
                        if (Vector3.Distance(pivot, anchor) >= length - 0.001f)
                            velocity = RopeMath.RemoveOutwardVelocity(velocity, pivot, anchor);
                    }
                    if (attached)
                    {
                        float remaining = Mathf.Max(0, settings.maxCorrection - correctionBudget);
                        float nextLength = RopeMath.ReelLength(length, targetLength, settings, dt, remaining);
                        Vector3 pivot = position + Vector3.up * settings.pivotHeight;
                        Vector3 correction = RopeMath.Correction(pivot, anchor, nextLength);
                        if (correction.magnitude <= remaining)
                        {
                            position += correction; length = nextLength;
                            result.maxCorrection = Mathf.Max(result.maxCorrection, correctionBudget + correction.magnitude);
                            if (Vector3.Distance(pivot + correction, anchor) >= length - 0.001f)
                                velocity = RopeMath.RemoveOutwardVelocity(velocity, pivot + correction, anchor);
                        }
                    }
                    result.peakSpeed = Mathf.Max(result.peakSpeed, velocity.magnitude);
                    result.peakAcceleration = Mathf.Max(result.peakAcceleration, (velocity - beforeVelocity).magnitude / dt);
                    result.turnDegrees = Mathf.Max(result.turnDegrees, ComfortMath.Turn(initialVelocity, velocity));
                    points[result.pointCount++] = position;
                    // Read-only world queries, never Physics.Simulate. A sphere is an approximation of the capsule.
                    if (casts > 0 && (i + 1) % castStride == 0)
                    {
                        Vector3 delta = position - lastCastPosition;
                        float travel = delta.magnitude;
                        if (travel > 0.001f && Physics.SphereCast(lastCastPosition + motor.BodyCenter, motor.BodyRadius,
                            delta / travel, out RaycastHit hit, travel, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        {
                            result.obstacleDistance = Vector3.Distance(motor.transform.position, lastCastPosition) + hit.distance;
                            result.obstacleHit = true;
                            points[result.pointCount - 1] = lastCastPosition + delta.normalized * hit.distance;
                            break; // collision response and subsequent bounce/slide are unknown
                        }
                        lastCastPosition = position;
                    }
                }
                Result = result;
                return result;
            }
        }
    }
}
