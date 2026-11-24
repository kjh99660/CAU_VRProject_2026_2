using System.Collections.Generic;
using UnityEngine;

namespace VRWebSwing
{
    [System.Serializable]
    public sealed class EndlessGimmick
    {
        [Tooltip("Gimmick prefab. Its root is placed on the corridor centre line (X=0, Y=0); local +Z points forward.")] public GameObject prefab;
        [Tooltip("Forward length in meters occupied by the prefab itself, measured from its root.")] [Min(0)] public float length = 24;
        [Tooltip("Empty gap in meters before the prefab, after the previous segment.")] [Min(0)] public float spaceBefore;
        [Tooltip("Empty gap in meters after the prefab, before the next segment.")] [Min(0)] public float spaceAfter;
        [Tooltip("How many times this gimmick appears back to back each time it is chosen (no city segments between copies).")] [Min(1)] public int repeatCount = 1;
        public float SlotLength => spaceBefore + length + spaceAfter;
    }

    // A bounded pool, plus one spare for a tethered segment outside the visible window.
    // The corridor is a sequence of slots: city segments with a gimmick after every N cities.
    // Gimmicks first appear once each in list order, then at random with the same spacing rule.
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
        [Header("Gimmicks")]
        [Tooltip("Gimmick prefabs. Each appears once in this order, then they repeat at random. Empty list = city segments only.")] public EndlessGimmick[] gimmicks;
        [Tooltip("City segments placed before each gimmick, including before the first one.")] [Range(1, 8)] public int citySegmentsBetweenGimmicks = 1;
        [Tooltip("Random order seed. 0 = a new order on every run.")] public int randomSeed;
        [Tooltip("Inactive instances created per gimmick at start. The pool grows only if more are visible at once.")] [Range(1, 8)] public int gimmickPrewarmCount = 2;
        [Header("Chasing back wall")]
        [Tooltip("Prefab with a BackWall component. Spawned once the player passes the last in-order gimmick (random section starts). Empty = no wall.")] public BackWall backWallPrefab;
        [Tooltip("Distance in meters behind the player where the wall appears.")] [Min(0)] public float backWallSpawnDistance = 30;
        [Tooltip("On revive, a wall closer than this (meters) behind the revive point is pushed back to this distance.")] [Min(0)] public float backWallReviveDistance = 20;
        [Header("Revive platform")]
        [Tooltip("Platform copied once for revives. Empty = the 'Start Platform' child of Start Area.")] public Transform revivePlatformSource;
        [Tooltip("Meters before a gimmick's slot where the platform goes after a laser death (half the platform size keeps it off the gimmick).")] [Min(0)] public float reviveGimmickMargin = 6;
        public Transform RevivePlatform { get; private set; }
        public BackWall BackWall { get; private set; }
        /// <summary>Raised once per run when the chasing wall appears.</summary>
        public event System.Action BackWallSpawned;
        public double OriginOffset { get; private set; }
        public int ActiveSegmentCount { get; private set; }
        public int ActiveGimmickCount { get; private set; }
        public long FirstVisibleIndex { get; private set; }
        public int VisibleSegmentCount => segments.Length - 1;
        public double LogicalZ(float localZ) => OriginOffset + localZ;
        public double ForwardDistance(float localZ) => LogicalZ(localZ) - initialStartZ;
        /// <summary>Gimmick list index of the slot, or -1 for a city segment.</summary>
        public int SlotKind(long index) { EnsureSlots(index); return slots[(int)index].kind; }

        struct Slot { public double start; public float length; public int kind; }
        readonly List<Slot> slots = new List<Slot>(64);
        readonly List<Transform> gimmickObjects = new List<Transform>();
        readonly List<int> gimmickKinds = new List<int>();
        readonly List<long> gimmickSlots = new List<long>();
        int[] validGimmicks = new int[0];
        System.Random random;
        int orderedCursor, lastGimmick, citiesSinceGimmick;
        long lastOrderedSlot = -1;
        int repeatRemaining;
        bool finalOrderedGroup;
        long[] indices;
        long lastCenter = long.MinValue;
        int lastRopeVersion = -1;
        Vector3 initialStartPosition;
        float initialStartZ, revivePlatformX, revivePlatformY;
        bool initialized;

        public struct LayoutSlot { public int kind; public double start; public float length; }
        /// <summary>
        /// Edit mode only (editor layout preview): the deterministic in-order section, i.e. every gimmick in list order with its
        /// repeats and city segments, plus the cities before the first random gimmick. Uses the same generator as Play mode.
        /// backWallSlot is the output index after which the chasing wall would spawn, or -1 without gimmicks.
        /// </summary>
        public void GetOrderedLayout(List<LayoutSlot> output, out int backWallSlot)
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Layout preview would overwrite the live corridor.");
            output.Clear(); backWallSlot = -1;
            validGimmicks = ValidGimmicks();
            ResetSlots();
            if (validGimmicks.Length == 0) { while (slots.Count < Mathf.Max(poolSize, 4)) AppendSlot(); }
            else
            {
                while (lastOrderedSlot < 0 && slots.Count < 4096) AppendSlot();
                backWallSlot = (int)lastOrderedSlot;
                for (int i = 0; i < citySegmentsBetweenGimmicks; ++i) AppendSlot();
            }
            foreach (Slot slot in slots) output.Add(new LayoutSlot { kind = slot.kind, start = slot.start, length = slot.length });
            slots.Clear(); lastOrderedSlot = -1;
        }
        int[] ValidGimmicks()
        {
            var valid = new List<int>();
            if (gimmicks != null)
                for (int i = 0; i < gimmicks.Length; ++i)
                    if (gimmicks[i] != null && gimmicks[i].prefab) valid.Add(i);
            return valid.ToArray();
        }
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
            validGimmicks = ValidGimmicks();
            foreach (int kind in validGimmicks)
                for (int n = 0; n < gimmickPrewarmCount; ++n) AddGimmickInstance(kind).gameObject.SetActive(false);
            if (backWallPrefab) { BackWall = Instantiate(backWallPrefab, transform); BackWall.Deactivate(); }
            Transform source = revivePlatformSource ? revivePlatformSource : startArea.Find("Start Platform");
            if (source)
            {
                RevivePlatform = Instantiate(source.gameObject, transform).transform;
                RevivePlatform.name = "Revive Platform";
                RevivePlatform.gameObject.SetActive(false);
                revivePlatformX = source.position.x; revivePlatformY = source.position.y;
            }
            else Debug.LogWarning("EndlessCity: no revive platform source; revives will place the player without a platform.", this);
            ResetSlots();
        }
        /// <summary>Moves the single revive platform to the revive point and returns the feet position to teleport to.
        /// Only Z follows the death; X and height match the start platform and start respawn point, which keeps revives in the clear corridor.</summary>
        public Vector3 PlaceRevivePlatform(Vector3 deathPosition, Transform hazard, Vector3 startFeet)
        {
            float z = deathPosition.z;
            // A hazard death (laser) revives in front of the whole run of that gimmick, never inside it.
            if (hazard && TryGimmickRunStart(hazard, out double runStart)) z = LocalZ(runStart) - reviveGimmickMargin;
            if (RevivePlatform)
            {
                RevivePlatform.position = new Vector3(revivePlatformX, revivePlatformY, z);
                RevivePlatform.gameObject.SetActive(true);
            }
            return new Vector3(startFeet.x, startFeet.y, z);
        }
        /// <summary>Keeps a running wall at least backWallReviveDistance behind the revive point.</summary>
        public void PushBackWallBehind(float z)
        {
            if (BackWall && BackWall.Running && BackWall.FrontZ > z - backWallReviveDistance) BackWall.SetFrontZ(z - backWallReviveDistance);
        }
        bool TryGimmickRunStart(Transform hazard, out double runStart)
        {
            runStart = 0;
            for (int i = 0; i < gimmickObjects.Count; ++i)
            {
                if (!hazard.IsChildOf(gimmickObjects[i]) || gimmickSlots[i] < 0 || gimmickSlots[i] >= slots.Count) continue;
                int slot = (int)gimmickSlots[i], kind = slots[slot].kind;
                while (slot > 0 && slots[slot - 1].kind == kind) --slot; // back-to-back copies count as one gimmick
                runStart = slots[slot].start;
                return true;
            }
            return false;
        }
        public void ResetRun()
        {
            Initialize(); OriginOffset = 0;
            startArea.position = initialStartPosition;
            for (int i = 0; i < indices.Length; ++i) indices[i] = long.MinValue;
            for (int i = 0; i < gimmickSlots.Count; ++i) gimmickSlots[i] = long.MinValue;
            if (BackWall) BackWall.Deactivate();
            if (RevivePlatform) RevivePlatform.gameObject.SetActive(false);
            ResetSlots();
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
            // The random section starts after the last in-order gimmick; the wall chases from there on.
            if (BackWall && !BackWall.Running && lastOrderedSlot >= 0 && center > lastOrderedSlot) SpawnBackWall();
        }
        void SpawnBackWall()
        {
            Vector3 player = course.motor.transform.position;
            BackWall.Activate(course.motor, new Vector3(0, 0, player.z - backWallSpawnDistance));
            BackWallSpawned?.Invoke();
        }
        // Inspector ⋮ menu, Play mode only: spawn (or re-place) the chasing wall behind the player right now.
        [ContextMenu("Spawn Back Wall Now")]
        void ForceSpawnBackWall() => SpawnBackWall();
        [ContextMenu("Spawn Back Wall Now", true)]
        bool CanForceSpawnBackWall() => Application.isPlaying && BackWall && course && course.motor;
        long CenterIndex()
        {
            double d = LogicalZ(course.motor.transform.position.z) - initialStartZ;
            if (d <= 0) return 0;
            while (SlotEnd(slots.Count - 1) <= d) AppendSlot();
            // Last slot whose start is at or before the player.
            int lo = 0, hi = slots.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (slots[mid].start <= d) lo = mid; else hi = mid - 1;
            }
            return lo;
        }
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
                for (int i = 0; i < gimmickObjects.Count; ++i) gimmickObjects[i].position -= offset;
                if (BackWall) BackWall.transform.position -= offset;
                if (RevivePlatform) RevivePlatform.position -= offset;
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
            EnsureSlots(last);
            for (long desired = first; desired <= last; ++desired)
            {
                Slot slot = slots[(int)desired];
                if (slot.kind < 0) PlaceCity(desired, slot, first, last);
                else PlaceGimmick(desired, slot, first, last);
            }
            ActiveSegmentCount = 0;
            for (int i = 0; i < segments.Length; ++i)
            {
                bool active = (indices[i] >= first && indices[i] <= last) || IsPinned(segments[i]);
                segments[i].gameObject.SetActive(active);
                if (active) ++ActiveSegmentCount;
            }
            ActiveGimmickCount = 0;
            for (int i = 0; i < gimmickObjects.Count; ++i)
            {
                bool active = (gimmickSlots[i] >= first && gimmickSlots[i] <= last) || IsPinned(gimmickObjects[i]);
                gimmickObjects[i].gameObject.SetActive(active);
                if (active) ++ActiveGimmickCount;
            }
        }
        void PlaceCity(long desired, Slot slot, long first, long last)
        {
            for (int i = 0; i < indices.Length; ++i)
                if (indices[i] == desired) return;
            for (int i = 0; i < indices.Length; ++i)
            {
                if (IsPinned(segments[i]) || (indices[i] >= first && indices[i] <= last)) continue;
                indices[i] = desired;
                segments[i].position = new Vector3(0, 0, LocalZ(slot.start));
                segments[i].gameObject.SetActive(true);
                return;
            }
        }
        void PlaceGimmick(long desired, Slot slot, long first, long last)
        {
            for (int i = 0; i < gimmickSlots.Count; ++i)
                if (gimmickSlots[i] == desired) return;
            int free = -1;
            for (int i = 0; i < gimmickObjects.Count; ++i)
            {
                if (gimmickKinds[i] != slot.kind || IsPinned(gimmickObjects[i]) || (gimmickSlots[i] >= first && gimmickSlots[i] <= last)) continue;
                free = i; break;
            }
            // Grows only while more copies of one gimmick are visible than prewarmed; reuse afterwards.
            if (free < 0) { AddGimmickInstance(slot.kind); free = gimmickObjects.Count - 1; }
            gimmickSlots[free] = desired;
            Transform t = gimmickObjects[free];
            // Re-enable so gimmick scripts reset their state through OnEnable.
            t.gameObject.SetActive(false);
            t.position = new Vector3(0, 0, LocalZ(slot.start + gimmicks[slot.kind].spaceBefore));
            t.gameObject.SetActive(true);
        }
        Transform AddGimmickInstance(int kind)
        {
            Transform t = Instantiate(gimmicks[kind].prefab, transform).transform;
            gimmickObjects.Add(t); gimmickKinds.Add(kind); gimmickSlots.Add(long.MinValue);
            return t;
        }
        float LocalZ(double logicalDistance) => (float)(initialStartZ + logicalDistance - OriginOffset);
        double SlotEnd(int index) => slots[index].start + slots[index].length;
        void ResetSlots()
        {
            random = new System.Random(randomSeed != 0 ? randomSeed : System.Environment.TickCount);
            slots.Clear(); orderedCursor = 0; lastGimmick = -1; citiesSinceGimmick = 0; lastOrderedSlot = -1;
            repeatRemaining = 0; finalOrderedGroup = false;
            AppendSlot();
        }
        void EnsureSlots(long index) { while (slots.Count <= index) AppendSlot(); }
        void AppendSlot()
        {
            int kind = NextKind();
            float length = kind < 0 ? segmentLength : Mathf.Max(0, gimmicks[kind].SlotLength);
            double start = slots.Count == 0 ? 0 : SlotEnd(slots.Count - 1);
            slots.Add(new Slot { start = start, length = length, kind = kind });
        }
        int NextKind()
        {
            // Finish the current run of back-to-back copies first; no city segments between them.
            if (repeatRemaining > 0)
            {
                --repeatRemaining;
                if (repeatRemaining == 0 && finalOrderedGroup) { lastOrderedSlot = slots.Count; finalOrderedGroup = false; }
                return lastGimmick;
            }
            if (validGimmicks.Length == 0 || citiesSinceGimmick < citySegmentsBetweenGimmicks) { ++citiesSinceGimmick; return -1; }
            citiesSinceGimmick = 0;
            int kind;
            if (orderedCursor < validGimmicks.Length)
            {
                kind = validGimmicks[orderedCursor++];
                finalOrderedGroup = orderedCursor == validGimmicks.Length;
            }
            else
            {
                // Avoid the same gimmick twice in a row when there is a choice.
                int pick = random.Next(validGimmicks.Length - (validGimmicks.Length > 1 && lastGimmick >= 0 ? 1 : 0));
                kind = validGimmicks[pick];
                if (validGimmicks.Length > 1 && lastGimmick >= 0 && kind == lastGimmick) kind = validGimmicks[validGimmicks.Length - 1];
            }
            lastGimmick = kind;
            repeatRemaining = Mathf.Max(1, gimmicks[kind].repeatCount) - 1;
            // The random section (and the chasing wall) starts after the last copy of the last in-order gimmick.
            if (repeatRemaining == 0 && finalOrderedGroup) { lastOrderedSlot = slots.Count; finalOrderedGroup = false; }
            return kind;
        }
        bool IsPinned(Transform root)
        {
            var rope = course.motor.rope;
            return rope.Attached && rope.AnchorSurface && (rope.AnchorSurface == root || rope.AnchorSurface.IsChildOf(root));
        }
    }
}
