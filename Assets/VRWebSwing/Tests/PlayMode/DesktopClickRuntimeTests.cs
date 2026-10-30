using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace VRWebSwing.Tests
{
    public sealed class DesktopClickRuntimeTests
    {
        Mouse mouse;
        InputSettings previousSettings, isolatedSettings;
        SwingMotor motor;
        SwingSettings movementCopy, originalMovement;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (previousSettings) InputSystem.settings = previousSettings;
            if (isolatedSettings) Object.Destroy(isolatedSettings);
            if (motor && originalMovement) { motor.settings = motor.rope.settings = originalMovement; }
            if (movementCopy) Object.Destroy(movementCopy);
            yield return null;
        }
        void MouseState(bool left = false, bool right = false)
        {
            var state = new UnityEngine.InputSystem.LowLevel.MouseState();
            if (left) state = state.WithButton(MouseButton.Left);
            if (right) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.Update();
        }
        void AimAtTarget() => motor.input.head.LookAt(new Vector3(0, 22, 12));
        [UnityTest] public IEnumerator FocusRecoveryFiresButExplicitPauseDoesNot()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("WebSwingCourse"); yield return null;
            motor = Object.FindFirstObjectByType<SwingMotor>();
            motor.input.forceDesktop = true;
            var comfort = motor.GetComponent<SwingComfort>(); comfort.recordSessions = false;
            comfort.SetMenuOpen(false);
            originalMovement = motor.settings; movementCopy = Object.Instantiate(originalMovement);
            movementCopy.gravity = 0; motor.settings = motor.rope.settings = movementCopy;
            previousSettings = Object.Instantiate(InputSystem.settings);
            isolatedSettings = Object.Instantiate(previousSettings);
            isolatedSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            isolatedSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = isolatedSettings;
            mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            motor.course.Restart(); motor.input.Clear();
            Assert.That(motor.input.IsVR, Is.False);
            comfort.HandleApplicationFocus(false);
            Assert.That(motor.Paused, Is.True); Assert.That(comfort.FocusSuspended, Is.True);
            Assert.That(motor.input.DesktopMenuOpen, Is.False, "Losing focus must not become an explicit menu pause");
            comfort.HandleApplicationFocus(true); AimAtTarget(); MouseState(left: true);
            Assert.That(motor.Paused, Is.False); Assert.That(motor.input.PendingCount, Is.EqualTo(1));
            yield return new WaitForFixedUpdate();
            Assert.That(motor.rope.Attached, Is.True, motor.rope.Status);
            Assert.That(motor.rope.Owner, Is.EqualTo(SwingHand.Left));
            MouseState(); yield return new WaitForFixedUpdate(); Assert.That(motor.rope.Attached, Is.True, "Mouse release must retain the toggled web");
            MouseState(left: true); yield return new WaitForFixedUpdate(); Assert.That(motor.rope.Attached, Is.False);
            MouseState();
            // Capture the second hand's ray and connect it through the same real callback path.
            AimAtTarget(); MouseState(right: true); yield return new WaitForFixedUpdate();
            Assert.That(motor.rope.Attached, Is.True, motor.rope.Status);
            Assert.That(motor.rope.Owner, Is.EqualTo(SwingHand.Right));
            MouseState(); yield return new WaitForFixedUpdate(); Assert.That(motor.rope.Attached, Is.True);
            MouseState(right: true); yield return new WaitForFixedUpdate(); Assert.That(motor.rope.Attached, Is.False);
            MouseState();
            // Explicit pause and abort must remain stopped even when the window regains focus or a menu is clicked.
            comfort.SetMenuOpen(true); comfort.HandleApplicationFocus(false); comfort.HandleApplicationFocus(true);
            AimAtTarget(); MouseState(left: true); yield return new WaitForFixedUpdate();
            Assert.That(motor.Paused, Is.True); Assert.That(motor.rope.Attached, Is.False);
            MouseState(); comfort.AbortExperience();
            AimAtTarget(); MouseState(right: true); yield return new WaitForFixedUpdate();
            Assert.That(motor.Paused, Is.True); Assert.That(motor.rope.Attached, Is.False);
            MouseState(); comfort.SetMenuOpen(false); motor.input.Clear();
            motor.input.desktopToggleWeb = false;
            AimAtTarget(); MouseState(left: true); MouseState();
            Assert.That(motor.input.PendingCount, Is.EqualTo(2), "A short click must retain both edges before the next physics update");
            yield return new WaitForFixedUpdate(); Assert.That(motor.rope.Attached, Is.False);
        }
    }
}
