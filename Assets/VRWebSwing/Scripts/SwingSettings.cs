using UnityEngine;

namespace VRWebSwing
{
    [CreateAssetMenu(menuName = "VR Web Swing/Settings")]
    public sealed class SwingSettings : ScriptableObject
    {
        [Tooltip("Gravity acceleration in m/s².")] [Min(0)] public float gravity = 18;
        [Tooltip("Maximum virtual speed in m/s.")] [Min(1)] public float maxSpeed = 35;
        [Tooltip("Ground acceleration in m/s².")] [Min(0)] public float groundAcceleration = 24;
        [Tooltip("Ground movement speed in m/s.")] [Min(0)] public float groundSpeed = 8;
        [Tooltip("Air steering acceleration in m/s²; zero disables steering.")] [Min(0)] public float airAcceleration = 4;
        [Tooltip("Initial jump velocity in m/s.")] [Min(0)] public float jumpSpeed = 8;
        [Tooltip("Maximum ray length in meters.")] [Min(1)] public float maxAttachDistance = 40;
        [Tooltip("Reject anchors closer than this distance in meters to the body pivot.")] [Min(0.1f)] public float minRopeLength = 1;
        [Tooltip("Maximum rope correction per physics step in meters; larger errors detach.")] [Min(0.01f)] public float maxCorrection = 1.5f;
        [Tooltip("Rope tolerance in meters after collision-aware correction.")] [Min(0.001f)] public float ropeTolerance = 0.08f;
        [Tooltip("Maximum travel per collision substep in meters.")] [Range(0.05f, 0.3f)] public float collisionStep = 0.2f;
        [Tooltip("Body rope pivot height above the root/feet in meters. HMD motion never changes it.")] public float pivotHeight = 0.9f;
        [Tooltip("World Y in meters below which the player respawns.")] public float failHeight = -8;
    }
}
