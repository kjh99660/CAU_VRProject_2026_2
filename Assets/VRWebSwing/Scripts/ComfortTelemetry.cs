using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace VRWebSwing
{
    // Bounded numerical samples only. File I/O happens only on export/session boundaries.
    public sealed class ComfortTelemetry
    {
        struct Sample
        {
            public double elapsed;
            public float speed, acceleration, turn, score, strength, meanFrameMs, maxFrameMs;
            public bool paused, xr;
            public int successfulShots, failedShots;
        }
        Sample[] samples;
        int count, discarded, frames, initialSuccess, initialFailure, exportIndex;
        double started, nextSample, frameSeconds, activeSeconds;
        float maxFrameMs;
        string session;
        ComfortMode mode;
        public bool Active { get; private set; }
        public string LastExportPath { get; private set; } = "";
        public string LastError { get; private set; } = "";
        public int SampleCount => count;
        public int DiscardedSamples => discarded;
        public float ActiveFraction => Mathf.Clamp01((float)(activeSeconds / Math.Max(0.0001, Time.realtimeSinceStartupAsDouble - started)));
        public void Begin(ComfortMode comparisonMode, ComfortTuning tuning, WebRope rope)
        {
            int capacity = Mathf.Clamp(tuning.maxSamples, 64, 8192);
            if (samples == null || samples.Length != capacity) samples = new Sample[capacity];
            count = discarded = frames = exportIndex = 0; frameSeconds = activeSeconds = 0; maxFrameMs = 0;
            started = nextSample = Time.realtimeSinceStartupAsDouble;
            initialSuccess = rope.SuccessfulShots; initialFailure = rope.FailedShots;
            mode = comparisonMode; session = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
            Active = true; LastError = "";
        }
        public void Record(SwingComfort comfort, float dt)
        {
            if (!Active || !ComfortMath.IsFinite(dt) || dt < 0) return;
            ++frames; frameSeconds += dt; maxFrameMs = Mathf.Max(maxFrameMs, dt * 1000);
            if (comfort.AppliedStrength > 0.01f) activeSeconds += dt;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSample) return;
            nextSample = now + 1 / Mathf.Clamp(comfort.tuning.sampleHz, 1, 20);
            if (count < samples.Length)
                samples[count++] = new Sample
                {
                    elapsed = now - started, speed = comfort.motor.Velocity.magnitude,
                    acceleration = comfort.Acceleration, turn = comfort.Prediction.turnDegrees, score = comfort.Score,
                    strength = comfort.AppliedStrength, paused = comfort.motor.Paused, xr = comfort.input.IsVR,
                    meanFrameMs = (float)(frameSeconds * 1000 / Math.Max(1, frames)), maxFrameMs = maxFrameMs,
                    successfulShots = comfort.motor.rope.SuccessfulShots - initialSuccess,
                    failedShots = comfort.motor.rope.FailedShots - initialFailure
                };
            else ++discarded;
            frameSeconds = 0; frames = 0; maxFrameMs = 0;
        }
        public void Export(string outcome = "snapshot")
        {
            if (samples == null || string.IsNullOrEmpty(session)) return;
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "ComfortSessions");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, session + "_" + mode + "_" + exportIndex++ + ".csv");
                var text = new StringBuilder(512 + count * 160);
                float activeFraction = ActiveFraction;
                text.AppendLine("session,mode,elapsed_s,speed_mps,acceleration_mps2,predicted_turn_deg,control_heuristic_score,vignette_strength,active_time_fraction,paused,xr_device,loop_frame_mean_ms,loop_frame_max_ms,xr_cpu_ms,xr_gpu_ms,xr_dropped_frames,successful_shots,failed_shots,outcome,aborted,completed,discarded_samples");
                // A summary row also records an abort before the first sampled frame.
                for (int i = 0; i < Math.Max(1, count); ++i)
                {
                    Sample s = count > 0 ? samples[i] : default;
                    text.Append(session).Append(',').Append(mode).Append(',');
                    Number(text, s.elapsed); Number(text, s.speed); Number(text, s.acceleration); Number(text, s.turn);
                    Number(text, s.score); Number(text, s.strength); Number(text, activeFraction);
                    text.Append(s.paused ? 1 : 0).Append(',').Append(s.xr ? 1 : 0).Append(',');
                    Number(text, s.meanFrameMs); Number(text, s.maxFrameMs);
                    text.Append(",,,"); // XR CPU/GPU/dropped frames are not measured; empty cells.
                    text.Append(s.successfulShots).Append(',').Append(s.failedShots).Append(',').Append(outcome).Append(',')
                        .Append(outcome == "aborted" ? 1 : 0).Append(',').Append(outcome == "complete" ? 1 : 0).Append(',').Append(discarded).AppendLine();
                }
                File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
                LastExportPath = path; LastError = "";
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                LastError = exception.GetType().Name + ": " + exception.Message;
                Debug.LogWarning("Comfort CSV export failed: " + LastError);
            }
        }
        public void Finish(string outcome) { if (!Active) return; Export(outcome); Active = false; }
        static void Number(StringBuilder text, double value) => text.Append(value.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
    }
}
