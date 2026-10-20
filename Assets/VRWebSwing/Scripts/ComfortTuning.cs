using System;
using UnityEngine;

namespace VRWebSwing
{
    public enum ComfortMode { Baseline, SpeedBased, Predictive }

    [Serializable]
    public sealed class ComfortTuning
    {
        [Header("Peripheral mask (never changes camera FOV)")]
        [Tooltip("Allow the same mask in desktop mode for debugging; this is not an HMD test.")] public bool previewOnDesktop = true;
        [Tooltip("Maximum normalized restriction, 0..1. Does not change swing forces or speed.")] [Range(0, 1)] public float maxStrength = 0.7f;
        [Tooltip("Minimum transparent aperture diameter, 0..1. Increase to make the next target easier to find.")] [Range(0.25f, 1)] public float minimumAperture = 0.55f;
        [Tooltip("XR mask edge feathering, 0..1.")] [Range(0.01f, 1)] public float feathering = 0.3f;
        [Tooltip("Restriction rise rate in normalized units per second.")] [Min(0.01f)] public float enterRate = 2;
        [Tooltip("Restriction recovery rate in normalized units per second.")] [Min(0.01f)] public float restoreRate = 1.5f;
        [Tooltip("Heuristic score to start restriction, 0..1.")] [Range(0, 1)] public float activateScore = 0.08f;
        [Tooltip("Lower score to stop restriction; hysteresis prevents threshold flicker.")] [Range(0, 1)] public float deactivateScore = 0.03f;
        [Header("Normalized indicators: lower threshold -> full threshold")]
        [Tooltip("Artificial movement speed thresholds in m/s.")] public Vector2 speedRange = new Vector2(6, 24);
        [Tooltip("Artificial velocity acceleration thresholds in m/s²; excludes HMD pose derivatives.")] public Vector2 accelerationRange = new Vector2(10, 45);
        [Tooltip("Direction change across the prediction horizon in degrees; ignored below 1 m/s.")] public Vector2 turnRange = new Vector2(15, 90);
        [Tooltip("Obstacle approach distance: strongest below x, zero beyond y, in meters.")] public Vector2 obstacleRange = new Vector2(0.5f, 4);
        [Tooltip("Predicted rope correction thresholds in meters per prediction step.")] public Vector2 constraintRange = new Vector2(0.005f, 0.1f);
        [Tooltip("Predictive blend weights: acceleration, direction change, obstacle approach, rope correction.")] public Vector4 predictionWeights = new Vector4(0.4f, 0.2f, 0.25f, 0.15f);
        [Tooltip("Blend of predictive indicators with the speed score, 0..1. This is a control heuristic, not a sickness probability.")] [Range(0, 1)] public float predictiveWeight = 0.5f;
        [Header("Prediction cost and assumptions")]
        [Tooltip("Prediction horizon in seconds. Initial tuning value, not a validated comfort threshold.")] [Range(0.2f, 0.5f)] public float horizon = 0.3f;
        [Tooltip("Copied-state simulation steps per refresh, bounded to 4..32.")] [Range(4, 32)] public int predictionSteps = 15;
        [Tooltip("Prediction refresh rate in Hz; no per-frame scene simulation.")] [Range(1, 30)] public float predictionHz = 10;
        [Tooltip("Maximum obstacle SphereCasts per prediction refresh. Truncates on the first predicted hit.")] [Range(0, 12)] public int obstacleCasts = 6;
        [Header("Measurement")]
        [Tooltip("CSV sample frequency in Hz; writing happens only at export/session end.")] [Range(1, 20)] public float sampleHz = 5;
        [Tooltip("Maximum samples retained in memory per mode/session. Extra samples are counted and discarded.")] [Range(64, 8192)] public int maxSamples = 2048;
    }

    public static class ComfortMath
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float Normalize(float value, Vector2 range)
        {
            if (!IsFinite(value) || !IsFinite(range.x) || !IsFinite(range.y)) return 0;
            return Mathf.Clamp01((value - range.x) / Mathf.Max(0.0001f, range.y - range.x));
        }
        public static float Turn(Vector3 from, Vector3 to) => from.sqrMagnitude >= 1 && to.sqrMagnitude >= 1 ? Vector3.Angle(from, to) : 0;
        public static float PredictiveScore(float speed, float acceleration, float turn, float obstacleDistance, float correction, ComfortTuning t)
        {
            Vector4 weights = Vector4.Max(t.predictionWeights, Vector4.zero);
            float sum = weights.x + weights.y + weights.z + weights.w;
            float extras = sum > 0.0001f ? (weights.x * Normalize(acceleration, t.accelerationRange)
                + weights.y * Normalize(turn, t.turnRange)
                + weights.z * (1 - Normalize(obstacleDistance, t.obstacleRange))
                + weights.w * Normalize(correction, t.constraintRange)) / sum : 0;
            float current = Normalize(speed, t.speedRange);
            // Retain a speed floor while allowing anticipated motion to increase the mask.
            return Mathf.Clamp01(current + (1 - current) * Mathf.Clamp01(t.predictiveWeight) * extras);
        }
        public static float SmoothStrength(float current, float score, ComfortTuning t, float dt, ref bool engaged)
        {
            if (!IsFinite(score) || !IsFinite(dt) || dt <= 0) return IsFinite(current) ? current : 0;
            if (score >= t.activateScore) engaged = true;
            else if (score <= Mathf.Min(t.deactivateScore, t.activateScore)) engaged = false;
            float target = engaged ? Mathf.Clamp01(score) * Mathf.Clamp01(t.maxStrength) : 0;
            return Mathf.MoveTowards(current, target, Mathf.Max(0.01f, target > current ? t.enterRate : t.restoreRate) * Mathf.Min(dt, 0.1f));
        }
    }
}
