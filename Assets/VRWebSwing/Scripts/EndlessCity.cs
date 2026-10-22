using UnityEngine;

namespace VRWebSwing
{
    // A bounded pool, plus one spare for a tethered segment outside the visible window.
    [DefaultExecutionOrder(-100)]
    public sealed class EndlessCity : MonoBehaviour
    {
        public SwingCourse course;
        public Transform startArea;
        public GameObject segmentPrefab;
        [Tooltip("Total pooled segments, including one spare for the current web anchor.")] [Range(4, 32)] public int poolSize = 12;
        [Tooltip("Repeat length in meters. Match the prefab's building spacing.")] [Min(12)] public float segmentLength = 24;
        [Tooltip("Segments retained behind the player; the remaining window extends forward.")] [Range(0, 8)] public int segmentsBehind = 2;
        [Tooltip("Rebase virtual Z coordinates beyond this distance in meters. Tracked poses and velocity are preserved.")] [Min(48)] public float rebaseDistance = 512;
        public Transform[] segments;
        public double OriginOffset { get; private set; }
        public int ActiveSegmentCount { get; private set; }
        public long FirstVisibleIndex { get; private set; }
        public int VisibleSegmentCount => segments.Length - 1;
        public double LogicalZ(float localZ) => OriginOffset + localZ;
        public double ForwardDistance(float localZ) => LogicalZ(localZ) - initialStartZ;
        long[] indices;
        long lastCenter = long.MinValue;
        int lastRopeVersion = -1;
        Vector3 initialStartPosition;
        float initialStartZ;
        bool initialized;

        void Awake() => Initialize();
        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            initialStartPosition = startArea.position; initialStartZ = course.start.position.z;
            int count = segments != null && segments.Length >= 4 && segments.Length <= 32 ? segments.Length : Mathf.Clamp(poolSize, 4, 32);
            if (segments == null || segments.Length != count)
            {
                segments = new Transform[count];
            }
            for (int i = 0; i < count; ++i)
                if (!segments[i]) segments[i] = Instantiate(segmentPrefab, transform).transform;
            indices = new long[segments.Length];
            for (int i = 0; i < indices.Length; ++i) indices[i] = long.MinValue;
        }
        public void ResetRun()
        {
            Initialize(); OriginOffset = 0;
            startArea.position = initialStartPosition;
            for (int i = 0; i < indices.Length; ++i) indices[i] = long.MinValue;
            // Teleport/reset will release the old tether; do not pin it during pool reset.
            course.motor.rope.Detach("New endless run");
            RefreshWindow(0); lastCenter = 0; lastRopeVersion = course.motor.rope.StateVersion;
            Physics.SyncTransforms();
        }
        void FixedUpdate()
        {
            if (course.motor.Paused) return;
            long center = CenterIndex();
            if (Mathf.Abs(course.motor.transform.position.z) >= Mathf.Max(48, rebaseDistance)
                || center != lastCenter || lastRopeVersion != course.motor.rope.StateVersion)
                RefreshForPlayer();
        }
        long CenterIndex() => System.Math.Max(0, (long)System.Math.Floor((LogicalZ(course.motor.transform.position.z) - initialStartZ) / segmentLength));
        public void RefreshForPlayer()
        {
            Initialize();
            float z = course.motor.transform.position.z;
            if (Mathf.Abs(z) >= Mathf.Max(48, rebaseDistance))
            {
                float shift = Mathf.Floor(z / segmentLength) * segmentLength;
                Vector3 offset = Vector3.forward * shift;
                OriginOffset += shift;
                startArea.position -= offset;
                for (int i = 0; i < segments.Length; ++i) segments[i].position -= offset;
                course.motor.ShiftOrigin(offset);
            }
            lastCenter = CenterIndex(); lastRopeVersion = course.motor.rope.StateVersion;
            RefreshWindow(lastCenter); Physics.SyncTransforms();
        }
        void RefreshWindow(long center)
        {
            int behind = Mathf.Clamp(segmentsBehind, 0, segments.Length - 3);
            long first = System.Math.Max(0, center - behind), last = first + VisibleSegmentCount - 1;
            FirstVisibleIndex = first;
            for (long desired = first; desired <= last; ++desired)
            {
                bool exists = false;
                for (int i = 0; i < indices.Length; ++i)
                    if (indices[i] == desired) { exists = true; break; }
                if (exists) continue;
                for (int i = 0; i < indices.Length; ++i)
                {
                    if (IsPinned(i) || (indices[i] >= first && indices[i] <= last)) continue;
                    indices[i] = desired;
                    segments[i].position = new Vector3(0, 0, (float)(initialStartZ + desired * (double)segmentLength - OriginOffset));
                    segments[i].gameObject.SetActive(true);
                    break;
                }
            }
            ActiveSegmentCount = 0;
            for (int i = 0; i < segments.Length; ++i)
            {
                bool active = (indices[i] >= first && indices[i] <= last) || IsPinned(i);
                segments[i].gameObject.SetActive(active);
                if (active) ++ActiveSegmentCount;
            }
        }
        bool IsPinned(int index)
        {
            var rope = course.motor.rope;
            return rope.Attached && rope.AnchorSurface && (rope.AnchorSurface == segments[index] || rope.AnchorSurface.IsChildOf(segments[index]));
        }
    }
}
