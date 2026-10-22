using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace VRWebSwing
{
    // Publish parameters before XRI's Update; the installed stereo mask renders them.
    [DefaultExecutionOrder(-20)]
    public sealed class SwingComfort : MonoBehaviour, ITunnelingVignetteProvider
    {
        public SwingMotor motor;
        public SwingInput input;
        public TunnelingVignetteController vignette;
        public ComfortMode mode = ComfortMode.SpeedBased;
        public ComfortTuning tuning = new ComfortTuning();
        [Tooltip("Buffered CSV recording of virtual motion, not a diagnosis.")] public bool recordSessions = true;
        public float Score { get; private set; }
        public float Strength { get; private set; }
        public float AppliedStrength => mask && mask.enabled ? Strength : 0;
        public float Acceleration { get; private set; }
        public SwingPredictionResult Prediction => predictor.Result;
        public ComfortTelemetry Telemetry { get; } = new ComfortTelemetry();
        public VignetteParameters vignetteParameters => parameters;
        readonly VignetteParameters parameters = new VignetteParameters { apertureSize = 1, featheringEffect = 0, easeInTime = 0, easeOutTime = 0, easeOutDelayTime = 0, easeInTimeLock = false };
        readonly SwingPrediction predictor = new SwingPrediction();
        static readonly ProfilerMarker Marker = new ProfilerMarker("VRWebSwing.Comfort.Update");
        MeshRenderer mask;
        bool registered, engaged, menuOpen, focused = true, haveVelocity;
        int resetVersion = -1, ropeVersion = -1, runVersion = -1;
        Vector3 previousVelocity;
        float previousFixedTime, nextPrediction;
        ComfortMode observedMode;
        void Awake() { if (vignette) mask = vignette.GetComponent<MeshRenderer>(); observedMode = mode; }
        void OnEnable()
        {
            input.ComfortModeRequested += CycleMode; input.PauseRequested += TogglePause;
            input.AbortRequested += AbortExperience; input.ExportRequested += ExportCsv;
            motor.MotionReset += ResetTransient;
            motor.OriginShifted += ResetPrediction;
            ResetTransient();
        }
        void OnDisable()
        {
            input.ComfortModeRequested -= CycleMode; input.PauseRequested -= TogglePause;
            input.AbortRequested -= AbortExperience; input.ExportRequested -= ExportCsv;
            motor.MotionReset -= ResetTransient;
            motor.OriginShifted -= ResetPrediction;
            ResetTransient(); Telemetry.Finish("disabled");
        }
        public void SetMode(ComfortMode value)
        {
            if (mode == value && observedMode == value) return;
            Telemetry.Finish("mode_changed"); mode = observedMode = value; ResetTransient();
        }
        public void CycleMode() => SetMode((ComfortMode)(((int)mode + 1) % 3));
        public void TogglePause() => SetMenuOpen(!motor.Paused);
        public void AbortExperience()
        {
            Telemetry.Finish("aborted"); motor.course.Respawn(); SetMenuOpen(true);
        }
        public void ExportCsv() => Telemetry.Export();
        public void SetMenuOpen(bool open)
        {
            menuOpen = open;
            motor.SetPaused(open); input.SetDesktopMenuOpen(open); ResetTransient();
        }
        void OnApplicationFocus(bool focus)
        {
            if (Application.isBatchMode) return; // no window focus exists in automated physics tests
            focused = focus;
            if (!focus && motor) SetMenuOpen(true);
        }
        void OnApplicationPause(bool paused)
        {
            if (paused && motor) { SetMenuOpen(true); Telemetry.Finish("application_paused"); }
        }
        void OnApplicationQuit() { ResetTransient(); Telemetry.Finish("quit"); }
        public void ResetTransient()
        {
            ResetPrediction(); Strength = 0; engaged = false;
            parameters.apertureSize = 1; parameters.featheringEffect = 0;
            if (registered && vignette) vignette.EndTunnelingVignette(this);
            registered = false;
            if (mask) mask.enabled = false;
        }
        void ResetPrediction()
        {
            predictor.Clear(); Score = Acceleration = 0; haveVelocity = false; nextPrediction = 0;
        }
        void Update()
        {
            using (Marker.Auto())
            {
                if (mode != observedMode) SetMode(mode);
                if (runVersion != motor.course.RunVersion)
                {
                    if (runVersion >= 0) Telemetry.Finish("restarted");
                    runVersion = motor.course.RunVersion; ResetTransient();
                }
                if (resetVersion != motor.ResetVersion)
                {
                    resetVersion = motor.ResetVersion; ResetTransient();
                    if (!motor.Paused && menuOpen) { menuOpen = false; input.SetDesktopMenuOpen(false); }
                }
                if (ropeVersion != motor.rope.StateVersion) { ropeVersion = motor.rope.StateVersion; ResetPrediction(); }
                bool complete = motor.course.State == CourseState.Complete;
                if (!recordSessions) Telemetry.Finish("recording_disabled");
                else if (!Telemetry.Active && !motor.Paused && !complete) Telemetry.Begin(mode, tuning, motor.rope);
                bool blocked = motor.Paused || menuOpen || !focused || Time.timeScale <= 0 || complete;
                if (blocked) ResetTransient();
                else
                {
                    float fixedNow = Time.fixedTime;
                    if (!haveVelocity) { previousVelocity = motor.Velocity; previousFixedTime = fixedNow; haveVelocity = true; }
                    else if (fixedNow > previousFixedTime + 0.00001f)
                    {
                        Acceleration = (motor.Velocity - previousVelocity).magnitude / (fixedNow - previousFixedTime);
                        if (!ComfortMath.IsFinite(Acceleration)) Acceleration = 0;
                        previousVelocity = motor.Velocity; previousFixedTime = fixedNow;
                    }
                    float speed = motor.Velocity.magnitude;
                    Score = ComfortMath.Normalize(speed, tuning.speedRange);
                    if (mode == ComfortMode.Predictive)
                    {
                        if (Time.unscaledTime >= nextPrediction)
                        {
                            predictor.Calculate(motor, tuning);
                            nextPrediction = Time.unscaledTime + 1 / Mathf.Clamp(tuning.predictionHz, 1, 30);
                        }
                        var prediction = predictor.Result;
                        Score = ComfortMath.PredictiveScore(Mathf.Max(speed, prediction.peakSpeed), Mathf.Max(Acceleration, prediction.peakAcceleration),
                            prediction.turnDegrees, prediction.obstacleDistance, prediction.maxCorrection, tuning);
                    }
                    if (mode == ComfortMode.Baseline) { Score = 0; Strength = 0; engaged = false; }
                    else
                    {
                        if (speed < 0.1f) Score = 0; // restore view at actual rest even if a forecast anticipates gravity
                        Strength = ComfortMath.SmoothStrength(Strength, Score, tuning, Time.unscaledDeltaTime, ref engaged);
                    }
                    ApplyMask();
                }
                Telemetry.Record(this, Time.unscaledDeltaTime);
                if (complete) Telemetry.Finish("complete");
            }
        }
        void ApplyMask()
        {
            bool visible = Strength > 0.0001f && (input.IsVR || tuning.previewOnDesktop) && vignette;
            parameters.apertureSize = Mathf.Lerp(1, Mathf.Clamp(tuning.minimumAperture, 0.25f, 1), Strength);
            parameters.featheringEffect = visible ? Mathf.Clamp(tuning.feathering, 0.01f, 1) : 0;
            if (visible && !registered) { vignette.BeginTunnelingVignette(this); registered = true; }
            else if (!visible && registered) { vignette.EndTunnelingVignette(this); registered = false; }
            if (mask) mask.enabled = visible;
        }
        void OnDrawGizmosSelected()
        {
            var result = predictor.Result;
            Gizmos.color = Color.magenta;
            for (int i = 1; i < result.pointCount; i++) Gizmos.DrawLine(predictor.points[i - 1] + Vector3.up * 0.9f, predictor.points[i] + Vector3.up * 0.9f);
        }
    }
}
