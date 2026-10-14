using UnityEngine;

namespace VRWebSwing
{
    // Interpolate the visual tracking group only; the motor is the sole writer of the collision root.
    [DefaultExecutionOrder(100)]
    public sealed class SwingVisualSmoothing : MonoBehaviour
    {
        public SwingMotor motor;
        public Transform visualRoot;
        void LateUpdate()
        {
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            visualRoot.position = Vector3.Lerp(motor.PreviousPosition, transform.position, alpha);
        }
        void OnDisable() { if (visualRoot) visualRoot.localPosition = Vector3.zero; }
    }
}
