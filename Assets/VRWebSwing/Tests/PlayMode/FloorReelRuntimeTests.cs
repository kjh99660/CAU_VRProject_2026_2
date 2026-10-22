using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRWebSwing.Tests
{
    public sealed class FloorReelRuntimeTests
    {
        GameObject floor, anchor, wall;
        SwingSettings originalSettings, settings;
        SwingMotor motor;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (motor && originalSettings) { motor.settings = originalSettings; motor.rope.settings = originalSettings; }
            if (floor) Object.Destroy(floor);
            if (anchor) Object.Destroy(anchor);
            if (wall) Object.Destroy(wall);
            if (settings) Object.Destroy(settings);
            yield return null;
        }
        [UnityTest] public IEnumerator OneShotFloorPlanReelsGraduallyAndStopsAtWalls()
        {
            SwingTestScene.LoadValidation(); yield return null;
            motor = Object.FindFirstObjectByType<SwingMotor>();
            var comfort = motor.GetComponent<SwingComfort>(); comfort.recordSessions = false; comfort.SetMenuOpen(false);
            originalSettings = motor.settings;
            settings = Object.Instantiate(originalSettings); settings.gravity = settings.airAcceleration = 0;
            motor.settings = motor.rope.settings = settings;
            var rope = motor.rope;
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(100, 9.5f, 0); floor.transform.localScale = new Vector3(40, 1, 40);
            anchor = GameObject.CreatePrimitive(PrimitiveType.Cube); anchor.layer = 8;
            anchor.transform.position = new Vector3(108, 20, 0); anchor.transform.localScale = Vector3.one * 2;
            Physics.SyncTransforms(); motor.TeleportTo(new Vector3(100, 11, 0));
            Ray shot = new Ray(motor.Pivot, anchor.transform.position - motor.Pivot);
            Assert.That(rope.TryAttach(shot, SwingHand.Left, motor.Pivot), Is.True);
            float initial = rope.Length, target = rope.TargetLength;
            Assert.That(rope.HasFloorSample, Is.True); Assert.That(rope.SampledFloorY, Is.EqualTo(10).Within(0.001f));
            Assert.That(target, Is.LessThan(initial));
            Assert.That(rope.Anchor.y - target - settings.pivotHeight, Is.EqualTo(10 + settings.floorClearance).Within(0.001f));
            // Change the floor after planning: the stored target must not be recomputed during this connection.
            floor.transform.position += Vector3.down * 2; Physics.SyncTransforms();
            Vector3 previous = motor.transform.position;
            var predictor = new SwingPrediction();
            predictor.Calculate(motor, new ComfortTuning { horizon = 0.2f, predictionSteps = 10, obstacleCasts = 0 });
            Vector3 predictedNext = predictor.points[1];
            Assert.That(rope.Length, Is.EqualTo(initial)); // prediction must not reel the real rope
            yield return new WaitForFixedUpdate();
            Assert.That(rope.Length, Is.LessThan(initial));
            Assert.That(initial - rope.Length, Is.LessThanOrEqualTo(settings.floorReelSpeed * Time.fixedDeltaTime + 0.001f));
            Assert.That(Vector3.Distance(previous, motor.transform.position), Is.LessThan(0.08f));
            Assert.That(Vector3.Distance(predictedNext, motor.transform.position), Is.LessThan(0.01f));
            for (int i = 0; i < 80; ++i) yield return new WaitForFixedUpdate();
            Assert.That(rope.Attached, Is.True, rope.Status);
            Assert.That(rope.TargetLength, Is.EqualTo(target)); Assert.That(rope.SampledFloorY, Is.EqualTo(10).Within(0.001f));
            Assert.That(rope.Length, Is.EqualTo(target).Within(0.002f));
            // Disabled and no-floor cases leave the initial radius alone.
            settings.autoShortenForFloor = false;
            Assert.That(rope.TryAttach(new Ray(motor.Pivot, anchor.transform.position - motor.Pivot), SwingHand.Right, motor.Pivot), Is.True);
            Assert.That(rope.TargetLength, Is.EqualTo(rope.Length)); Assert.That(rope.HasFloorSample, Is.False);
            settings.autoShortenForFloor = true; settings.floorMask = 0;
            Assert.That(rope.TryAttach(new Ray(motor.Pivot, anchor.transform.position - motor.Pivot), SwingHand.Left, motor.Pivot), Is.True);
            Assert.That(rope.HasFloorSample, Is.False); Assert.That(rope.Reeling, Is.False);
            // A new wall blocks only the attempted reel. It must not teleport the capsule or destroy a valid old connection.
            settings.floorMask = Physics.DefaultRaycastLayers;
            motor.TeleportTo(new Vector3(100, 11, 0)); Physics.SyncTransforms();
            Assert.That(rope.TryAttach(new Ray(motor.Pivot, anchor.transform.position - motor.Pivot), SwingHand.Right, motor.Pivot), Is.True);
            Assert.That(rope.Reeling, Is.True);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(100.335f, 14, 0); wall.transform.localScale = new Vector3(0.05f, 12, 10);
            Physics.SyncTransforms();
            for (int i = 0; i < 8; ++i) yield return new WaitForFixedUpdate();
            Assert.That(rope.Attached, Is.True, rope.Status); Assert.That(rope.Reeling, Is.False);
            Assert.That(rope.TargetLength, Is.EqualTo(rope.Length)); Assert.That(motor.transform.position.x, Is.LessThan(100.1f));
            rope.Release(SwingHand.Right);
            Assert.That(rope.Reeling, Is.False); Assert.That(rope.HasFloorSample, Is.False);
            Assert.That(rope.TargetLength, Is.EqualTo(rope.Length));
        }
    }
}
