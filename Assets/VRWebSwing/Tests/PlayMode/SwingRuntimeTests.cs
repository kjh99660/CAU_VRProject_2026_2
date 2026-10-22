using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace VRWebSwing.Tests
{
    public sealed class SwingRuntimeTests
    {
        [UnityTest] public IEnumerator RuntimeFlowAndCollision()
        {
            SwingTestScene.LoadValidation();
            yield return null;
            var motor = Object.FindFirstObjectByType<SwingMotor>();
            var input = motor.input; var rope = motor.rope; var course = motor.course;
            input.forceDesktop = true;
            yield return null;
            // The same input queue used by Input System callbacks preserves two short taps in order.
            input.Clear();
            // InputManager destroys a nonpersistent old settings object when replaced.
            var previousInputSettings = Object.Instantiate(InputSystem.settings);
            var isolatedInputSettings = Object.Instantiate(previousInputSettings);
            isolatedInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            isolatedInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = isolatedInputSettings;
            var testMouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Left)); InputSystem.Update();
            InputSystem.QueueStateEvent(testMouse, new MouseState()); InputSystem.Update();
            InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Right)); InputSystem.Update();
            InputSystem.QueueStateEvent(testMouse, new MouseState()); InputSystem.Update();
            InputSystem.RemoveDevice(testMouse);
            InputSystem.settings = previousInputSettings;
            Object.Destroy(isolatedInputSettings);
            Assert.That(input.PendingCount, Is.EqualTo(4));
            Assert.That(input.TryConsume(out var first) && first.kind == SwingCommandKind.Attach && first.hand == SwingHand.Left);
            Assert.That(Vector3.Distance(first.ray.origin, input.head.position), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(first.ray.direction, input.head.forward), Is.LessThan(0.001f));
            Assert.That(input.TryConsume(out var second) && second.kind == SwingCommandKind.Release);
            input.Clear();
            // Successful left attach, ownership, failed shot preservation, right-hand replacement, release inertia.
            Ray shot = new Ray(motor.Pivot, new Vector3(0, 22, 12) - motor.Pivot);
            Assert.That(rope.TryAttach(shot, SwingHand.Left, motor.Pivot), Is.True);
            float length = rope.Length;
            rope.Release(SwingHand.Right); Assert.That(rope.Attached, Is.True);
            Assert.That(rope.TryAttach(new Ray(motor.Pivot, Vector3.down), SwingHand.Right, motor.Pivot), Is.False);
            Assert.That(rope.Owner, Is.EqualTo(SwingHand.Left));
            Assert.That(rope.TryAttach(shot, SwingHand.Right, motor.Pivot), Is.True);
            Vector3 momentum = new Vector3(3, 2, 6);
            typeof(SwingMotor).GetProperty("Velocity").SetValue(motor, momentum);
            rope.Release(SwingHand.Right);
            Assert.That(motor.Velocity, Is.EqualTo(momentum));
            Assert.That(length, Is.GreaterThan(1));
            // Near-anchor rejection does not replace a valid connection.
            Assert.That(rope.FindAnchor(shot, out var closeHit), Is.True);
            Assert.That(rope.TryAttach(shot, SwingHand.Left, closeHit.point), Is.False);
            course.Respawn(); Assert.That(motor.Velocity, Is.EqualTo(Vector3.zero)); Assert.That(rope.Attached, Is.False);
            // Real fixed-step pendulum simulation: a taut rope bounds distance without erasing its tangent.
            motor.TeleportTo(new Vector3(0, 14, 12));
            Assert.That(rope.TryAttach(new Ray(motor.Pivot, Vector3.up), SwingHand.Left, motor.Pivot), Is.True);
            typeof(SwingMotor).GetProperty("Velocity").SetValue(motor, new Vector3(0, 0, 10));
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(rope.Attached, Is.True, rope.Status);
                Assert.That(Vector3.Distance(motor.Pivot, rope.Anchor), Is.LessThanOrEqualTo(rope.Length + motor.settings.ropeTolerance));
                Assert.That(float.IsNaN(motor.Velocity.sqrMagnitude), Is.False);
            }
            Assert.That(motor.Velocity.magnitude, Is.GreaterThan(1));
            course.Restart();
            // Swept high-speed collision against a thin wall, with gravity disabled only on a temporary settings clone.
            var original = motor.settings;
            var testSettings = Object.Instantiate(original); testSettings.gravity = 0; testSettings.airAcceleration = 0;
            motor.settings = testSettings;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 15, 5); wall.transform.localScale = new Vector3(10, 10, 0.1f);
            Physics.SyncTransforms();
            motor.TeleportTo(new Vector3(0, 14, 0));
            typeof(SwingMotor).GetProperty("Velocity").SetValue(motor, new Vector3(0, 0, 35));
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(motor.transform.position.z, Is.LessThan(4.7f), "Capsule must stop before thin wall");
            Object.Destroy(wall); motor.settings = original; Object.Destroy(testSettings);
            // Landing and automatic fall reset.
            motor.TeleportTo(course.start.position + Vector3.up * 2);
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            Assert.That(motor.Grounded, Is.True, $"Landing position={motor.transform.position} velocity={motor.Velocity}");
            motor.TeleportTo(new Vector3(0, -20, 0));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(motor.transform.position.y, Is.GreaterThan(9));
            // Sequential checkpoint gating, latest spawn, completion, and full restart.
            Assert.That(course.CheckSegment(course.checkpoints[1].position, course.checkpoints[1].position), Is.False);
            Assert.That(course.NextCheckpoint, Is.EqualTo(0));
            course.CheckSegment(course.checkpoints[0].position, course.checkpoints[0].position);
            Assert.That(course.NextCheckpoint, Is.EqualTo(1));
            course.Respawn(); Assert.That(Vector3.Distance(motor.transform.position, course.respawnPoints[0].position), Is.LessThan(0.001f));
            course.CheckSegment(course.checkpoints[1].position, course.checkpoints[1].position);
            course.CheckSegment(course.checkpoints[2].position, course.checkpoints[2].position);
            Assert.That(course.State, Is.EqualTo(CourseState.Complete)); Assert.That(motor.Velocity, Is.EqualTo(Vector3.zero));
            course.Restart(); Assert.That(course.NextCheckpoint, Is.Zero); Assert.That(input.PendingCount, Is.Zero);
            yield return null;
        }
    }
}
