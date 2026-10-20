using UnityEngine;

namespace VRWebSwing
{
    public sealed class SwingHud : MonoBehaviour
    {
        public SwingMotor motor;
        public SwingCourse course;
        public WebRope rope;
        public SwingInput input;
        public TextMesh worldText;
        string status;
        float nextRefresh;
        SwingComfort comfort;
        void Awake() { comfort = motor.GetComponent<SwingComfort>(); }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            status = $"{course.State} | CP {course.NextCheckpoint}/3\nSpeed {motor.Velocity.magnitude:F1} m/s | Ground {motor.Grounded}\nWeb {rope.Attached} | Length {rope.Length:F1} m | Aim {rope.CanAttach}\n{rope.Status}\n";
            worldText.text = status + (input.IsVR
                ? "Triggers: hold web / release owner\nLeft stick: move | Right A: jump\nLeft Y: respawn | Left menu: restart"
                : "Mouse: look / aim | Click: capture + fire | Esc: free mouse\nLMB / RMB: hold left / right web; release to detach\nWASD: move | Space: jump | R: respawn | T: restart");
            if (course.State == CourseState.Complete) worldText.text += "\nCOMPLETE! Respawn or restart";
            if (comfort)
            {
                worldText.text += $"\nComfort: {comfort.mode} | Score {comfort.Score:F2} | Mask {comfort.AppliedStrength:F2}\nM / Left X: mode | P or F1 / Right menu: pause\nBackspace / Right B: STOP + safe return | F8: CSV";
                if (motor.Paused) worldText.text += "\nPAUSED - P to resume, R to return, T to restart";
            }
        }
        void OnGUI()
        {
            if (input.IsVR) return;
            GUI.Box(new Rect(12, 12, 600, 265), worldText.text);
            if (comfort && input.DesktopMenuOpen)
            {
                if (GUI.Button(new Rect(12, 280, 120, 28), "A Baseline")) comfort.SetMode(ComfortMode.Baseline);
                if (GUI.Button(new Rect(140, 280, 120, 28), "B SpeedBased")) comfort.SetMode(ComfortMode.SpeedBased);
                if (GUI.Button(new Rect(268, 280, 120, 28), "C Predictive")) comfort.SetMode(ComfortMode.Predictive);
                if (GUI.Button(new Rect(396, 280, 100, 28), "Pause/resume")) comfort.TogglePause();
                if (GUI.Button(new Rect(504, 280, 108, 28), "STOP / return")) comfort.AbortExperience();
                GUI.Label(new Rect(12, 315, 800, 35), "CSV: " + (string.IsNullOrEmpty(comfort.Telemetry.LastError)
                    ? comfort.Telemetry.LastExportPath : comfort.Telemetry.LastError));
            }
            Color previousColor = GUI.color;
            GUI.color = rope.CanAttach ? Color.green : Color.white;
            float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
            GUI.DrawTexture(new Rect(x - 7, y - 1, 14, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1, y - 7, 2, 14), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
