using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRWebSwing.Tests
{
    public sealed class EndlessCityRuntimeTests
    {
        SwingMotor motor;
        SwingSettings original, settings;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (motor && original) { motor.settings = original; motor.rope.settings = original; }
            if (settings) Object.Destroy(settings);
            yield return null;
        }
        [UnityTest] public IEnumerator EndlessPoolDistanceAnchorAndFloatingOrigin()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("WebSwingCourse"); yield return null;
            motor = Object.FindFirstObjectByType<SwingMotor>();
            var comfort = motor.GetComponent<SwingComfort>(); comfort.recordSessions = false; comfort.SetMenuOpen(false);
            motor.input.forceDesktop = true;
            var course = motor.course; var city = course.endlessCity; var rope = motor.rope;
            Assert.That(course.IsEndless, Is.True); Assert.That(course.checkpoints, Is.Empty);
            Assert.That(GameObject.Find("Landing 1"), Is.Null);
            Assert.That(GameObject.Find("Start Platform"), Is.Not.Null);
            Assert.That(motor.input.head.Find("World HUD"), Is.Null);
            Assert.That(motor.settings.failHeight, Is.EqualTo(-13));
            Assert.That(motor.GetComponent<SwingHud>().distanceText.anchor, Is.EqualTo(TextAnchor.UpperRight));
            Assert.That(motor.GetComponent<SwingHud>().distanceText.transform.parent, Is.EqualTo(motor.input.head));
            original = motor.settings; settings = Object.Instantiate(original);
            settings.gravity = settings.airAcceleration = 0; settings.autoShortenForFloor = false;
            motor.settings = rope.settings = settings;
            Assert.That(city.segments.Length, Is.EqualTo(12));
            Assert.That(city.ActiveSegmentCount, Is.EqualTo(11));
            // Score is forward progress, not vertical motion or repeated backtracking.
            course.CheckSegment(Vector3.zero, new Vector3(0, 10, 100));
            course.CheckSegment(Vector3.zero, new Vector3(0, 100, 80));
            Assert.That(course.RunDistance, Is.EqualTo(100));
            course.Restart();
            Assert.That(course.RunDistance, Is.Zero); Assert.That(course.LastRunDistance, Is.EqualTo(100));
            Assert.That(course.BestDistance, Is.EqualTo(100));
            // Pin an anchor in the previous tile; the spare fills the new forward tile instead of moving it.
            city.segmentsBehind = 0;
            motor.TeleportTo(new Vector3(0, 14, 24.1f));
            Assert.That(rope.TryAttach(new Ray(motor.Pivot, new Vector3(0, 22, 12) - motor.Pivot), SwingHand.Left, motor.Pivot), Is.True);
            Vector3 acceptedAnchor = rope.Anchor;
            settings.maxRopeLength = 2;
            Assert.That(rope.TryAttach(new Ray(motor.Pivot, new Vector3(0, 22, 12) - motor.Pivot), SwingHand.Right, motor.Pivot), Is.False);
            Assert.That(rope.Attached, Is.True); Assert.That(rope.Anchor, Is.EqualTo(acceptedAnchor));
            Assert.That(rope.Owner, Is.EqualTo(SwingHand.Left));
            settings.maxRopeLength = original.maxRopeLength;
            Transform pinned = null;
            foreach (var segment in city.segments) if (rope.AnchorSurface.IsChildOf(segment)) pinned = segment;
            Assert.That(pinned, Is.Not.Null);
            Vector3 pinnedPosition = pinned.position, originalAnchor = rope.Anchor;
            city.RefreshForPlayer();
            Assert.That(pinned.position, Is.EqualTo(pinnedPosition)); Assert.That(rope.Anchor, Is.EqualTo(originalAnchor));
            Assert.That(city.ActiveSegmentCount, Is.EqualTo(12));
            rope.Release(SwingHand.Left); city.RefreshForPlayer();
            Assert.That(city.ActiveSegmentCount, Is.EqualTo(11));
            Assert.That(pinned.gameObject.activeSelf, Is.False);
            // Shift coordinates while tethered: velocity, relative geometry, pending shot and score remain coherent.
            city.segmentsBehind = 2; course.Restart(); city.rebaseDistance = 48;
            motor.TeleportTo(new Vector3(0, 14, 60));
            Assert.That(rope.TryAttach(new Ray(motor.Pivot, Vector3.up), SwingHand.Left, motor.Pivot), Is.True);
            Vector3 velocity = Vector3.forward * 7;
            typeof(SwingMotor).GetProperty("Velocity").SetValue(motor, velocity);
            Vector3 relativeAnchor = rope.Anchor - motor.Pivot;
            Ray queuedRay = motor.input.Aim(SwingHand.Right);
            motor.input.Enqueue(SwingCommandKind.Attach, SwingHand.Right);
            course.CheckSegment(motor.transform.position, motor.transform.position);
            double distance = course.RunDistance;
            int resetVersion = motor.ResetVersion;
            city.RefreshForPlayer();
            Assert.That(city.OriginOffset, Is.EqualTo(48));
            Assert.That(Vector3.Distance(relativeAnchor, rope.Anchor - motor.Pivot), Is.LessThan(0.001f));
            Assert.That(motor.Velocity, Is.EqualTo(velocity)); Assert.That(rope.Attached, Is.True);
            Assert.That(motor.ResetVersion, Is.EqualTo(resetVersion));
            Assert.That(motor.input.TryConsume(out var queued), Is.True);
            Assert.That(Vector3.Distance(queued.ray.origin, queuedRay.origin - Vector3.forward * 48), Is.LessThan(0.001f));
            course.CheckSegment(motor.transform.position, motor.transform.position);
            Assert.That(course.RunDistance, Is.EqualTo(distance));
            // Recycle through 200 tiles: no new segment instances, no ever-growing scene, no completion gate.
            city.rebaseDistance = 512; course.Restart();
            int[] ids = new int[city.segments.Length];
            for (int i = 0; i < ids.Length; ++i) ids[i] = city.segments[i].GetInstanceID();
            for (int tile = 1; tile <= 200; ++tile)
            {
                float localZ = (float)(tile * 24d - city.OriginOffset);
                motor.TeleportTo(new Vector3(0, 14, localZ));
                course.CheckSegment(motor.transform.position, motor.transform.position);
                city.RefreshForPlayer();
                Assert.That(Mathf.Abs(motor.transform.position.z), Is.LessThan(512));
                Assert.That(city.ActiveSegmentCount, Is.EqualTo(11));
            }
            Assert.That(course.RunDistance, Is.EqualTo(4800)); Assert.That(course.State, Is.Not.EqualTo(CourseState.Complete));
            Assert.That(city.transform.childCount, Is.EqualTo(12));
            for (int i = 0; i < ids.Length; ++i) Assert.That(city.segments[i].GetInstanceID(), Is.EqualTo(ids[i]));
            // The void has no landing plane; falling ends this run and restores the start area and pool origin.
            Assert.That(Physics.Raycast(new Vector3(0, 15, motor.transform.position.z), Vector3.down, 20, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.False);
            motor.TeleportTo(new Vector3(0, -20, motor.transform.position.z));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(course.RunDistance, Is.Zero); Assert.That(course.BestDistance, Is.EqualTo(4800));
            Assert.That(course.LastRunDistance, Is.EqualTo(4800)); Assert.That(city.OriginOffset, Is.Zero);
            Assert.That(Vector3.Distance(motor.transform.position, course.start.position), Is.LessThan(0.01f));
            Assert.That(Physics.Raycast(course.start.position + Vector3.up, Vector3.down, 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
        }
    }
}
