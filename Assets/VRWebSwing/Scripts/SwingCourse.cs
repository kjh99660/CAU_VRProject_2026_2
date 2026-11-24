using UnityEngine;

namespace VRWebSwing
{
    public enum CourseState { Ready, Playing, Complete }
    public enum DeathCause { Fall, Laser, BackWall }
    // Alive: normal play. Reviving: fading out, or standing on the revive platform until a web attaches. GameOver: frozen until the next run.
    public enum LifeState { Alive, Reviving, GameOver }
    public sealed class SwingCourse : MonoBehaviour
    {
        public SwingMotor motor;
        public Transform start;
        public Transform[] checkpoints;
        public Transform[] respawnPoints;
        public EndlessCity endlessCity;
        public bool IsEndless => endlessCity;
        public double RunDistance { get; private set; }
        public double BestDistance { get; private set; }
        public double LastRunDistance { get; private set; }
        [Tooltip("Checkpoint sphere radius in meters; swept player segments detect fast crossings.")] public float radius = 4;
        public CourseState State { get; private set; }
        public int NextCheckpoint { get; private set; }
        public int RunVersion { get; private set; }
        [Header("Lives (endless only)")]
        [Tooltip("Lives per run. Falls and lasers cost one; touching the chasing wall ends the run regardless.")] [Range(1, 9)] public int maxLives = 3;
        [Tooltip("Seconds the view fades out before the revive teleport; it fades back in over the same time.")] [Range(0, 1)] public float reviveFadeTime = 0.25f;
        [Tooltip("Seconds the game-over result stays up before a new run starts automatically.")] [Min(0)] public float gameOverDelay = 4;
        public int Lives { get; private set; }
        public LifeState Life { get; private set; }
        public DeathCause LastDeathCause { get; private set; }
        /// <summary>Unscaled time of the latest death; UI fades are timed from it.</summary>
        public float DeathTime { get; private set; } = float.NegativeInfinity;
        /// <summary>Increments on every death so UI can react once per death.</summary>
        public int DeathVersion { get; private set; }
        public float GameOverTimeLeft => Life == LifeState.GameOver ? Mathf.Max(0, DeathTime + gameOverDelay - Time.unscaledTime) : 0;
        bool revivePending;
        Vector3 reviveFeet;
        void Start() => Restart();
        public void Restart()
        {
            if (RunDistance > 0) LastRunDistance = RunDistance;
            RunDistance = 0;
            Lives = maxLives; Life = LifeState.Alive; revivePending = false;
            if (IsEndless) endlessCity.ResetRun();
            ++RunVersion; NextCheckpoint = 0; State = CourseState.Ready; motor.TeleportTo(start.position);
        }
        /// <summary>Player death. Endless runs spend a life and revive on a platform; otherwise behaves like Respawn().</summary>
        public void Die(DeathCause cause, Transform hazard = null)
        {
            if (!IsEndless) { Respawn(); return; }
            if (Life == LifeState.GameOver || revivePending) return;
            LastDeathCause = cause; DeathTime = Time.unscaledTime; ++DeathVersion;
            Lives = cause == DeathCause.BackWall ? 0 : Mathf.Max(0, Lives - 1);
            motor.SetPaused(true); // freeze; the wall and the city window stop with the motor
            if (Lives == 0) { Life = LifeState.GameOver; return; }
            Life = LifeState.Reviving;
            // Build the platform now; the teleport waits until the view has faded out so the jump is never seen.
            reviveFeet = endlessCity.PlaceRevivePlatform(motor.transform.position, hazard, start.position);
            revivePending = true;
            if (reviveFadeTime <= 0) CompleteRevive();
        }
        void CompleteRevive()
        {
            revivePending = false;
            endlessCity.PushBackWallBehind(reviveFeet.z);
            motor.TeleportTo(reviveFeet);
            Physics.SyncTransforms();
        }
        void Update()
        {
            if (revivePending && Time.unscaledTime >= DeathTime + reviveFadeTime) CompleteRevive();
            else if (Life == LifeState.GameOver && GameOverTimeLeft <= 0) Restart();
        }
        void FixedUpdate()
        {
            // The wall waits while the player stands on the revive platform; the first web attach resumes the chase.
            if (Life == LifeState.Reviving && !revivePending && motor.rope.Attached) Life = LifeState.Alive;
        }
        public void Respawn()
        {
            if (IsEndless) { Restart(); return; }
            if (State == CourseState.Complete) { Restart(); return; }
            State = NextCheckpoint == 0 ? CourseState.Ready : CourseState.Playing;
            motor.TeleportTo(NextCheckpoint == 0 ? start.position : respawnPoints[NextCheckpoint - 1].position);
        }
        public bool CheckSegment(Vector3 from, Vector3 to)
        {
            if (State == CourseState.Complete) return true;
            if ((to - start.position).sqrMagnitude > 1) State = CourseState.Playing;
            if (IsEndless)
            {
                RunDistance = System.Math.Max(RunDistance, System.Math.Max(0, endlessCity.ForwardDistance(to.z)));
                BestDistance = System.Math.Max(BestDistance, RunDistance);
                return false;
            }
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
