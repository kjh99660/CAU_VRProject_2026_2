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
        [Tooltip("Maximum physical rope length in meters at a new connection, measured from the body pivot. Longer shots are rejected without replacing the existing web.")] [Min(1)] public float maxRopeLength = 40;
        [Tooltip("Reject anchors closer than this distance in meters to the body pivot.")] [Min(0.1f)] public float minRopeLength = 1;
        [Header("One-shot floor clearance reel")]
        [Tooltip("At each successful connection, sample the floor once and reel towards a circle-bottom clearance target. Applies equally in A/B/C modes.")] public bool autoShortenForFloor = true;
        [Tooltip("Foot clearance above sampled floor at the lowest point of a full swing circle, in meters.")] [Min(0)] public float floorClearance = 0.5f;
        [Tooltip("Maximum rope shortening speed in m/s. Does not teleport the player to the target length.")] [Min(0)] public float floorReelSpeed = 3;
        [Tooltip("Maximum downward floor probe distance from the anchor, in meters. At most three rays per successful connection.")] [Min(1)] public float floorProbeDistance = 80;
        [Tooltip("Horizontal offset from an anchor wall for the floor probe, in meters.")] [Min(0.05f)] public float floorProbeOffset = 0.45f;
        [Tooltip("Floor layers; player Ignore Raycast layer is always excluded. Trigger volumes are ignored.")] public LayerMask floorMask = Physics.DefaultRaycastLayers;
        [Tooltip("Minimum upward floor normal Y. Rejects surfaces too steep to treat as a floor.")] [Range(0.1f, 1)] public float floorNormalMinY = 0.6f;
        [Tooltip("Maximum rope correction per physics step in meters; larger errors detach.")] [Min(0.01f)] public float maxCorrection = 1.5f;
        [Tooltip("Rope tolerance in meters after collision-aware correction.")] [Min(0.001f)] public float ropeTolerance = 0.08f;
        [Tooltip("Maximum travel per collision substep in meters.")] [Range(0.05f, 0.3f)] public float collisionStep = 0.2f;
        [Tooltip("Body rope pivot height above the root/feet in meters. HMD motion never changes it.")] public float pivotHeight = 0.9f;
        [Tooltip("World Y in meters below which the player respawns.")] public float failHeight = -13;
    }
}
