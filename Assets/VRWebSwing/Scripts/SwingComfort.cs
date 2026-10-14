using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace VRWebSwing
{
    public sealed class SwingComfort : MonoBehaviour, ITunnelingVignetteProvider
    {
        public SwingMotor motor;
        public SwingInput input;
        public TunnelingVignetteController vignette;
        [Tooltip("Enable the installed XRI vignette during fast VR movement.")] public bool useVignette = true;
        [Tooltip("Virtual movement speed in m/s at which the vignette begins.")] public float speedThreshold = 6;
        public VignetteParameters vignetteParameters => null;
        bool active;
        void Update()
        {
            bool next = useVignette && input.IsVR && motor.Velocity.magnitude > speedThreshold;
            if (next == active) return;
            active = next;
            if (active) vignette.BeginTunnelingVignette(this); else vignette.EndTunnelingVignette(this);
        }
        void OnDisable() { if (active && vignette) vignette.EndTunnelingVignette(this); active = false; }
    }
}
