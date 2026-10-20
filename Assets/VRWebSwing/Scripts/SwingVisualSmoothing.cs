using UnityEngine;

namespace VRWebSwing
{
    // Interpolate the visual tracking group only; the motor is the sole writer of the collision root.
    [DefaultExecutionOrder(100)]
    public sealed class SwingVisualSmoothing : MonoBehaviour
    {
        public SwingMotor motor;
        public Transform visualRoot;
        [Tooltip("Remove only artificial parent pitch/roll. Tracked HMD local pose remains untouched.")] public bool stabilizeArtificialTilt = true;
        void LateUpdate()
        {
            float alpha = Time.fixedDeltaTime > 0.0001f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1;
            visualRoot.position = Vector3.Lerp(motor.PreviousPosition, transform.position, alpha);
            if (stabilizeArtificialTilt) visualRoot.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        }
        void OnDisable() { if (visualRoot) visualRoot.localPosition = Vector3.zero; }
    }
}
