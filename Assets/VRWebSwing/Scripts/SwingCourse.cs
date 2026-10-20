using UnityEngine;

namespace VRWebSwing
{
    public enum CourseState { Ready, Playing, Complete }
    public sealed class SwingCourse : MonoBehaviour
    {
        public SwingMotor motor;
        public Transform start;
        public Transform[] checkpoints;
        public Transform[] respawnPoints;
        [Tooltip("Checkpoint sphere radius in meters; swept player segments detect fast crossings.")] public float radius = 4;
        public CourseState State { get; private set; }
        public int NextCheckpoint { get; private set; }
        public int RunVersion { get; private set; }
        void Start() => Restart();
        public void Restart() { ++RunVersion; NextCheckpoint = 0; State = CourseState.Ready; motor.TeleportTo(start.position); }
        public void Respawn()
        {
            if (State == CourseState.Complete) { Restart(); return; }
            State = NextCheckpoint == 0 ? CourseState.Ready : CourseState.Playing;
            motor.TeleportTo(NextCheckpoint == 0 ? start.position : respawnPoints[NextCheckpoint - 1].position);
        }
        public bool CheckSegment(Vector3 from, Vector3 to)
        {
            if (State == CourseState.Complete) return true;
            if ((to - start.position).sqrMagnitude > 1) State = CourseState.Playing;
            if (NextCheckpoint >= checkpoints.Length) return false;
            Vector3 target = checkpoints[NextCheckpoint].position;
            // Check body center against checkpoint sphere, including high-speed traversal.
            from += Vector3.up; to += Vector3.up;
            Vector3 delta = to - from;
            float t = delta.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector3.Dot(target - from, delta) / delta.sqrMagnitude) : 0;
            if ((from + delta * t - target).sqrMagnitude > radius * radius) return false;
            ++NextCheckpoint;
            if (NextCheckpoint == checkpoints.Length) { State = CourseState.Complete; motor.Stop(); return true; }
            return false;
        }
        void OnDrawGizmos()
        {
            if (checkpoints == null) return;
            for (int i = 0; i < checkpoints.Length; ++i)
            {
                if (!checkpoints[i]) continue;
                Gizmos.color = i < NextCheckpoint ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(checkpoints[i].position, radius);
            }
        }
    }
}
