using UnityEngine;

namespace VRWebSwing
{
    [DefaultExecutionOrder(200)]
    public sealed class WebRope : MonoBehaviour
    {
        public SwingSettings settings;
        public SwingInput input;
        public LineRenderer line;
        public Transform aimMarker;
        [Tooltip("Static attachable surfaces. Walls outside this mask still occlude aiming.")] public LayerMask attachMask;
        public bool Attached { get; private set; }
        public Vector3 Anchor { get; private set; }
        public Transform AnchorSurface { get; private set; }
        public float Length { get; private set; }
        public float TargetLength { get; private set; }
        public bool HasFloorSample { get; private set; }
        public float SampledFloorY { get; private set; }
        public bool FloorClearanceLimited { get; private set; }
        public bool Reeling => Attached && settings.autoShortenForFloor && TargetLength < Length - 0.001f;
        public SwingHand Owner { get; private set; }
        public string Status { get; private set; } = "Ready";
        public bool CanAttach { get; private set; }
        public int StateVersion { get; private set; }
        public int SuccessfulShots { get; private set; }
        public int FailedShots { get; private set; }

        public bool FindAnchor(Ray ray, out RaycastHit hit)
        {
            return Physics.Raycast(ray, out hit, settings.maxAttachDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) && ((1 << hit.collider.gameObject.layer) & attachMask.value) != 0
                && Vector3.Distance(transform.position + Vector3.up * settings.pivotHeight, hit.point) <= settings.maxRopeLength;
        }
        public bool TryAttach(Ray ray, SwingHand hand, Vector3 pivot)
        {
            if (!FindAnchor(ray, out RaycastHit hit)) { ++FailedShots; Status = "Miss (previous web preserved)"; return false; }
            float distance = Vector3.Distance(pivot, hit.point);
            if (distance < settings.minRopeLength || distance > settings.maxAttachDistance || distance > settings.maxRopeLength)
            { ++FailedShots; Status = "Anchor too close / far"; return false; }
            Anchor = hit.point; Length = distance; Owner = hand; Attached = true;
            AnchorSurface = hit.collider.transform;
            PlanFloorReel(pivot, hit.normal);
            ++StateVersion; ++SuccessfulShots; Status = "Attached: " + hand; return true;
        }
        void PlanFloorReel(Vector3 pivot, Vector3 surfaceNormal)
        {
            TargetLength = Length; HasFloorSample = FloorClearanceLimited = false; SampledFloorY = 0;
            if (!settings.autoShortenForFloor) return;
            // A fixed, one-shot sample set, not a continuous ground search or full-circle simulation.
            Vector3 outsideWall = Vector3.ProjectOnPlane(surfaceNormal, Vector3.up).normalized * settings.floorProbeOffset;
            Vector3 underAnchor = Anchor + outsideWall;
            SampleFloor(underAnchor);
            SampleFloor(new Vector3(pivot.x, Anchor.y, pivot.z));
            SampleFloor(Vector3.Lerp(underAnchor, new Vector3(pivot.x, Anchor.y, pivot.z), 0.5f));
            if (!HasFloorSample) return;
            float allowed = Anchor.y - SampledFloorY - settings.pivotHeight - settings.floorClearance;
            FloorClearanceLimited = allowed < settings.minRopeLength;
            TargetLength = RopeMath.FloorLimitedLength(Length, Anchor.y, SampledFloorY,
                settings.pivotHeight, settings.floorClearance, settings.minRopeLength);
        }
        void SampleFloor(Vector3 column)
        {
            // Start just below the attach height so overhead targets do not count as the floor.
            Vector3 origin = new Vector3(column.x, Anchor.y - 0.05f, column.z);
            int mask = settings.floorMask.value & Physics.DefaultRaycastLayers;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, settings.floorProbeDistance, mask, QueryTriggerInteraction.Ignore)
                || hit.normal.y < settings.floorNormalMinY) return;
            if (!HasFloorSample || hit.point.y > SampledFloorY) SampledFloorY = hit.point.y;
            HasFloorSample = true;
        }
        // Called only after the motor's collision-aware correction succeeds.
        public void CommitReelLength(float length) { Length = Mathf.Clamp(length, TargetLength, Length); }
        public void StopReeling() { TargetLength = Length; Status = "Floor reel stopped by obstacle"; }
        public void Release(SwingHand hand) { if (Attached && Owner == hand) Detach("Released"); }
        public void Detach(string reason)
        {
            if (Attached) ++StateVersion;
            Attached = false; AnchorSurface = null; TargetLength = Length; HasFloorSample = FloorClearanceLimited = false; Status = reason;
        }
        public void ShiftOrigin(Vector3 offset)
        {
            if (Attached) Anchor -= offset;
            if (aimMarker) aimMarker.position -= offset;
        }
        void LateUpdate()
        {
            line.enabled = Attached;
            if (Attached) { line.SetPosition(0, input.Hand(Owner).position); line.SetPosition(1, Anchor); }
            CanAttach = FindAnchor(input.Aim(input.PreviewHand), out RaycastHit hit);
            aimMarker.gameObject.SetActive(CanAttach);
            if (CanAttach) aimMarker.position = hit.point + hit.normal * 0.04f;
        }
        void OnDisable() { Detach("Disabled"); if (line) line.enabled = false; }
        void OnDrawGizmosSelected()
        {
            if (!Attached) return;
            Gizmos.color = Color.cyan; Gizmos.DrawSphere(Anchor, 0.15f); Gizmos.DrawWireSphere(Anchor, Length);
            if (HasFloorSample)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(Anchor, Anchor + Vector3.down * TargetLength);
                Gizmos.DrawWireCube(new Vector3(Anchor.x, SampledFloorY, Anchor.z), new Vector3(2, 0.02f, 2));
            }
        }
    }
}
