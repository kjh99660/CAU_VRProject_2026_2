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
