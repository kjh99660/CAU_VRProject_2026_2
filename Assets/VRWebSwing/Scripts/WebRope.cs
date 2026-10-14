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
        public float Length { get; private set; }
        public SwingHand Owner { get; private set; }
        public string Status { get; private set; } = "Ready";
        public bool CanAttach { get; private set; }

        public bool FindAnchor(Ray ray, out RaycastHit hit)
        {
            return Physics.Raycast(ray, out hit, settings.maxAttachDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) && ((1 << hit.collider.gameObject.layer) & attachMask.value) != 0;
        }
        public bool TryAttach(Ray ray, SwingHand hand, Vector3 pivot)
        {
            if (!FindAnchor(ray, out RaycastHit hit)) { Status = "Miss (previous web preserved)"; return false; }
            float distance = Vector3.Distance(pivot, hit.point);
            if (distance < settings.minRopeLength || distance > settings.maxAttachDistance)
            { Status = "Anchor too close / far"; return false; }
            Anchor = hit.point; Length = distance; Owner = hand; Attached = true;
            Status = "Attached: " + hand; return true;
        }
        public void Release(SwingHand hand) { if (Attached && Owner == hand) Detach("Released"); }
        public void Detach(string reason) { Attached = false; Status = reason; }
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
        }
    }
}
