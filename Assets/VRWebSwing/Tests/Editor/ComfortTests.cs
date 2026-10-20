using NUnit.Framework;
using UnityEngine;

namespace VRWebSwing.Tests
{
    public sealed class ComfortTests
    {
        [Test] public void ScoreRejectsInvalidValuesAndZeroDirection()
        {
            Assert.That(ComfortMath.Normalize(float.NaN, new Vector2(1, 2)), Is.Zero);
            Assert.That(ComfortMath.Normalize(float.PositiveInfinity, Vector2.zero), Is.Zero);
            Assert.That(ComfortMath.Turn(Vector3.zero, Vector3.right * 20), Is.Zero);
            Assert.That(ComfortMath.Turn(Vector3.right * 10, Vector3.forward * 10), Is.EqualTo(90).Within(0.001));
        }
        [Test] public void HysteresisAndStopRecoveryAreBounded()
        {
            var tuning = new ComfortTuning();
            bool engaged = false;
            float strength = ComfortMath.SmoothStrength(0, 0.2f, tuning, 0.02f, ref engaged);
            Assert.That(engaged, Is.True);
            ComfortMath.SmoothStrength(strength, 0.05f, tuning, 0.02f, ref engaged);
            Assert.That(engaged, Is.True, "Intermediate score retains the state");
            Assert.That(ComfortMath.SmoothStrength(strength, 1, tuning, 0, ref engaged), Is.EqualTo(strength));
            for (int i = 0; i < 100; i++) strength = ComfortMath.SmoothStrength(strength, 0, tuning, 0.02f, ref engaged);
            Assert.That(strength, Is.Zero); Assert.That(engaged, Is.False);
            for (int i = 0; i < 100; i++) strength = ComfortMath.SmoothStrength(strength, 1, tuning, 0.02f, ref engaged);
            Assert.That(strength, Is.LessThanOrEqualTo(tuning.maxStrength));
        }
        [Test] public void PredictiveControlScoreIsFiniteAndExplainedByIndicators()
        {
            var tuning = new ComfortTuning();
            float quiet = ComfortMath.PredictiveScore(0, 0, 0, 10000, 0, tuning);
            float approach = ComfortMath.PredictiveScore(0, 45, 90, 0.2f, 0.1f, tuning);
            Assert.That(quiet, Is.Zero);
            Assert.That(approach, Is.GreaterThan(quiet).And.LessThanOrEqualTo(1));
        }
        [Test] public void SharedVelocityIntegrationPreservesOriginalMovementFormula()
        {
            var settings = ScriptableObject.CreateInstance<SwingSettings>();
            Vector3 velocity = new Vector3(12, 3, 7), direction = Vector3.forward;
            float dt = 0.02f;
            Vector3 expected = Vector3.ClampMagnitude(velocity + direction * settings.airAcceleration * dt + Vector3.down * settings.gravity * dt, settings.maxSpeed);
            Assert.That(Vector3.Distance(SwingMotionMath.IntegrateVelocity(velocity, direction, false, settings, dt), expected), Is.LessThan(0.00001));
            Assert.That(SwingMotionMath.IntegrateVelocity(velocity, direction, false, settings, 0), Is.EqualTo(velocity));
            Object.DestroyImmediate(settings);
        }
    }
}
