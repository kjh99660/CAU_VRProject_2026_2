using UnityEngine;

namespace VRWebSwing
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SwingMotor : MonoBehaviour
    {
        public SwingSettings settings;
        public SwingInput input;
        public WebRope rope;
        public SwingCourse course;
        public Vector3 Velocity { get; private set; }
        public Vector3 PreviousPosition { get; private set; }
        public bool Grounded { get; private set; }
        public Vector3 Pivot => transform.position + Vector3.up * settings.pivotHeight;
        CharacterController body;
        float correctionUsed;
        void Awake() { body = GetComponent<CharacterController>(); }
        void FixedUpdate()
        {
            PreviousPosition = transform.position;
            while (input.TryConsume(out SwingCommand command))
            {
                switch (command.kind)
                {
                    case SwingCommandKind.Restart: course.Restart(); return;
                    case SwingCommandKind.Respawn: course.Respawn(); return;
                    case SwingCommandKind.Attach:
                        if (course.State != CourseState.Complete) rope.TryAttach(command.ray, command.hand, Pivot);
                        break;
                    case SwingCommandKind.Release: rope.Release(command.hand); break;
                    case SwingCommandKind.Jump:
                        if (Grounded) { Velocity = new Vector3(Velocity.x, settings.jumpSpeed, Velocity.z); Grounded = false; }
                        break;
                }
            }
            if (course.State == CourseState.Complete) return;
            Vector3 forward = Vector3.ProjectOnPlane(input.head.forward, Vector3.up).normalized;
            Vector3 direction = forward * input.Move.y + Vector3.Cross(Vector3.up, forward) * input.Move.x;
            float dt = Time.fixedDeltaTime;
            if (Grounded)
            {
                Vector3 horizontal = Vector3.MoveTowards(new Vector3(Velocity.x, 0, Velocity.z),
                    direction * settings.groundSpeed, settings.groundAcceleration * dt);
                Velocity = new Vector3(horizontal.x, Mathf.Max(Velocity.y, -2), horizontal.z);
            }
            else Velocity += direction * (settings.airAcceleration * dt);
            Velocity += Vector3.down * (settings.gravity * dt);
            Velocity = Vector3.ClampMagnitude(Velocity, settings.maxSpeed);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Velocity.magnitude * dt / settings.collisionStep));
            correctionUsed = 0;
            for (int i = 0; i < steps; ++i)
            {
                Vector3 previous = transform.position;
                CollisionFlags flags = body.Move(Velocity * (dt / steps));
                Grounded = (flags & CollisionFlags.Below) != 0 || (Velocity.y <= 0 && ProbeGround());
                if (Grounded && Velocity.y < 0) Velocity = new Vector3(Velocity.x, 0, Velocity.z);
                if (rope.Attached)
                {
                    Vector3 correction = RopeMath.Correction(Pivot, rope.Anchor, rope.Length);
                    correctionUsed += correction.magnitude;
                    if (correctionUsed > settings.maxCorrection) rope.Detach("Correction budget exceeded");
                    else
                    {
                        if (correction.sqrMagnitude > 0) body.Move(correction); // capsule sweep; never teleport through walls
                        if (Vector3.Distance(Pivot, rope.Anchor) > rope.Length + settings.ropeTolerance)
                            rope.Detach("Wall blocked rope constraint");
                        else if (Vector3.Distance(Pivot, rope.Anchor) >= rope.Length - 0.001f)
                            Velocity = RopeMath.RemoveOutwardVelocity(Velocity, Pivot, rope.Anchor);
                    }
                }
                if (course.CheckSegment(previous, transform.position)) return;
            }
            if (transform.position.y < settings.failHeight) course.Respawn();
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            float inward = Vector3.Dot(Velocity, hit.normal);
            if (inward < 0) Velocity -= hit.normal * inward;
        }
        bool ProbeGround()
        {
            // Small downward moves can stay within the controller skin and report no Below flag.
            Vector3 bottomCenter = transform.position + body.center + Vector3.down * (body.height * 0.5f - body.radius);
            return Physics.SphereCast(bottomCenter + Vector3.up * 0.04f, body.radius * 0.9f, Vector3.down,
                out RaycastHit hit, 0.12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(hit.normal, Vector3.up) <= body.slopeLimit;
        }
        public void TeleportTo(Vector3 feet)
        {
            if (!body) body = GetComponent<CharacterController>();
            body.enabled = false; transform.position = feet; body.enabled = true;
            PreviousPosition = feet;
            Velocity = Vector3.zero; Grounded = false; rope.Detach("Reset"); input.Clear();
        }
        public void Stop() { Velocity = Vector3.zero; rope.Detach("Course complete"); input.Clear(); }
        void OnDisable() { Velocity = Vector3.zero; Grounded = false; if (rope) rope.Detach("Motor disabled"); }
        void OnDrawGizmosSelected()
        {
            if (!settings) return;
            Gizmos.color = Color.yellow; Gizmos.DrawSphere(Pivot, 0.08f); Gizmos.DrawRay(Pivot, Velocity * 0.2f);
        }
    }
}
