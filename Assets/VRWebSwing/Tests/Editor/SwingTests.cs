using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace VRWebSwing.Tests
{
    public sealed class SwingTests
    {
        [Test] public void SlackRopeDoesNotPull()
        {
            Assert.That(RopeMath.Correction(Vector3.right, Vector3.zero, 2), Is.EqualTo(Vector3.zero));
        }
        [Test] public void FloorLengthIncludesFeetAndClearanceAndNeverExtends()
        {
            float length = RopeMath.FloorLimitedLength(20, 22, 10, 0.9f, 0.5f, 1);
            Assert.That(length, Is.EqualTo(10.6f).Within(0.0001f));
            Assert.That(22 - length - 0.9f, Is.EqualTo(10.5f).Within(0.0001f));
            Assert.That(RopeMath.FloorLimitedLength(5, 22, 10, 0.9f, 0.5f, 1), Is.EqualTo(5));
            Assert.That(RopeMath.FloorLimitedLength(5, 11, 10, 0.9f, 0.5f, 1), Is.EqualTo(1));
        }
        [Test] public void ReelingIsRateLimitedBudgetLimitedAndOptional()
        {
            var settings = ScriptableObject.CreateInstance<SwingSettings>();
            Assert.That(RopeMath.ReelLength(10, 5, settings, 0.02f, 1.5f), Is.EqualTo(9.94f).Within(0.0001f));
            Assert.That(RopeMath.ReelLength(10, 5, settings, 1, 0.04f), Is.EqualTo(9.99f).Within(0.0001f));
            Assert.That(RopeMath.ReelLength(10, 5, settings, 0, 1.5f), Is.EqualTo(10));
            Assert.That(RopeMath.ReelLength(10, 5, settings, float.NaN, 1.5f), Is.EqualTo(10));
            settings.autoShortenForFloor = false;
            Assert.That(RopeMath.ReelLength(10, 5, settings, 0.02f, 1.5f), Is.EqualTo(10));
            Object.DestroyImmediate(settings);
        }
        [Test] public void TautRopeProjectsToMaximumLength()
        {
            Vector3 pivot = new Vector3(3, 4, 0);
            Assert.That((pivot + RopeMath.Correction(pivot, Vector3.zero, 2)).magnitude, Is.EqualTo(2).Within(0.0001));
        }
        [Test] public void ProjectionPreservesTangentialAndInwardVelocity()
        {
            Assert.That(RopeMath.RemoveOutwardVelocity(new Vector3(5, 7, 0), Vector3.right, Vector3.zero), Is.EqualTo(new Vector3(0, 7, 0)));
            Assert.That(RopeMath.RemoveOutwardVelocity(new Vector3(-5, 7, 0), Vector3.right, Vector3.zero), Is.EqualTo(new Vector3(-5, 7, 0)));
        }
        [Test] public void CoincidentAnchorRemainsFiniteAtRest()
        {
            Assert.That(RopeMath.RemoveOutwardVelocity(Vector3.zero, Vector3.zero, Vector3.zero), Is.EqualTo(Vector3.zero));
            Assert.That(RopeMath.Correction(Vector3.zero, Vector3.zero, 1), Is.EqualTo(Vector3.zero));
        }
    }
}
