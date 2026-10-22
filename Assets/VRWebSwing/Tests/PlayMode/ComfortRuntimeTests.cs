using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRWebSwing.Tests
{
    public sealed class ComfortRuntimeTests
    {
        [UnityTest] public IEnumerator ComparisonPredictionAndLifecycle()
        {
            SwingTestScene.LoadValidation();
            yield return null;
            var motor = Object.FindFirstObjectByType<SwingMotor>();
            var comfort = motor.GetComponent<SwingComfort>();
            Assert.That(comfort, Is.Not.Null);
            comfort.recordSessions = false;
            motor.input.forceDesktop = true;
            comfort.SetMenuOpen(false);
            float gravity = motor.settings.gravity, maxSpeed = motor.settings.maxSpeed;
            float originalFov = motor.input.head.GetComponent<Camera>().fieldOfView;
            foreach (ComfortMode mode in System.Enum.GetValues(typeof(ComfortMode)))
            {
                comfort.SetMode(mode);
                Assert.That(motor.settings.gravity, Is.EqualTo(gravity));
                Assert.That(motor.settings.maxSpeed, Is.EqualTo(maxSpeed));
            }
            // Prediction is read-only and shares the next fixed step with the motor in unobstructed air.
            motor.TeleportTo(new Vector3(0, 15, 12));
            Assert.That(motor.rope.TryAttach(new Ray(motor.Pivot, Vector3.up), SwingHand.Left, motor.Pivot), Is.True);
            typeof(SwingMotor).GetProperty("Velocity").SetValue(motor, new Vector3(0, 0, 10));
            var predictor = new SwingPrediction();
            var tuning = new ComfortTuning { horizon = 0.2f, predictionSteps = 10, obstacleCasts = 0 };
            Vector3 originalPosition = motor.transform.position, originalVelocity = motor.Velocity;
            predictor.Calculate(motor, tuning);
            Vector3 nextPoint = predictor.points[1];
            Assert.That(motor.transform.position, Is.EqualTo(originalPosition));
            Assert.That(motor.Velocity, Is.EqualTo(originalVelocity));
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(motor.transform.position, nextPoint), Is.LessThan(0.05f));
            comfort.SetMode(ComfortMode.SpeedBased);
            typeof(SwingMotor).GetProperty("Velocity").SetValue(motor, new Vector3(0, 0, 25));
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(comfort.Strength, Is.GreaterThan(0));
            Assert.That(motor.input.head.GetComponent<Camera>().fieldOfView, Is.EqualTo(originalFov));
            comfort.SetMode(ComfortMode.Baseline); yield return null;
            Assert.That(comfort.Strength, Is.Zero);
            // Artificial parent tilt is removed while genuine local head pitch/roll survives unchanged.
            comfort.TogglePause();
            motor.input.enabled = false;
            Quaternion headPose = Quaternion.Euler(20, 0, 15);
            motor.input.head.localRotation = headPose;
            motor.transform.rotation = Quaternion.Euler(12, 30, 20);
            yield return null; yield return null;
            var visuals = motor.GetComponent<SwingVisualSmoothing>();
            Assert.That(Quaternion.Angle(visuals.visualRoot.rotation, Quaternion.Euler(0, motor.transform.eulerAngles.y, 0)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(motor.input.head.localRotation, headPose), Is.LessThan(0.01f));
            motor.transform.rotation = Quaternion.identity; motor.input.enabled = true;
            comfort.SetMode(ComfortMode.Predictive);
            comfort.TogglePause(); yield return new WaitForFixedUpdate(); yield return null;
            motor.course.Respawn();
            Assert.That(comfort.Prediction.pointCount, Is.Zero);
            Assert.That(comfort.Strength, Is.Zero);
            yield return null;
            Assert.That(comfort.Acceleration, Is.LessThan(30));
            comfort.AbortExperience();
            Assert.That(motor.Paused, Is.True); Assert.That(motor.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(motor.rope.Attached, Is.False); Assert.That(comfort.Strength, Is.Zero);
            Vector3 parked = motor.transform.position;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(motor.transform.position, Is.EqualTo(parked));
            Assert.That(Time.timeScale, Is.EqualTo(1));
            // Export structure and the absence of fabricated XR CPU/GPU/frame-drop data.
            var log = new ComfortTelemetry();
            log.Begin(ComfortMode.Baseline, tuning, motor.rope); log.Record(comfort, 0.02f); log.Finish("aborted");
            Assert.That(File.Exists(log.LastExportPath), Is.True, log.LastError);
            string[] csv = File.ReadAllLines(log.LastExportPath);
            Assert.That(csv[1].Split(',').Length, Is.EqualTo(csv[0].Split(',').Length));
            Assert.That(csv[1].Split(',')[13], Is.Empty); Assert.That(csv[1].Split(',')[14], Is.Empty); Assert.That(csv[1].Split(',')[15], Is.Empty);
            Assert.That(csv[1].Split(',')[19], Is.EqualTo("1"));
            File.Delete(log.LastExportPath);
            comfort.enabled = false; Assert.That(comfort.Strength, Is.Zero);
            comfort.enabled = true;
            motor.course.Restart();
            Assert.That(motor.Paused, Is.False); Assert.That(comfort.Strength, Is.Zero);
            yield return null;
            // A new prediction can legitimately react once gravity starts moving the reset body again.
            Assert.That(ComfortMath.IsFinite(comfort.Strength), Is.True);
            Assert.That(comfort.Strength, Is.LessThanOrEqualTo(comfort.tuning.maxStrength));
        }
    }
}
