using UnityEngine;

namespace VRWebSwing
{
    public sealed class SwingHud : MonoBehaviour
    {
        public SwingMotor motor;
        public SwingCourse course;
        public WebRope rope;
        public SwingInput input;
        public TextMesh distanceText;
        string distanceLabel, bestLabel;
        GUIStyle distanceStyle, bestStyle;
        float nextRefresh;
        SwingComfort comfort;
        void Awake()
        {
            comfort = motor.GetComponent<SwingComfort>();
            // Compatibility for older/validation scenes: never display the former camera-attached debug panel.
            Transform legacy = input.head.Find("World HUD");
            if (legacy) legacy.gameObject.SetActive(false);
        }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            if (distanceText) distanceText.gameObject.SetActive(course.IsEndless && input.IsVR);
            if (course.IsEndless)
            {
                distanceLabel = $"{course.RunDistance:F1} m";
                bestLabel = $"BEST {course.BestDistance:F1} m | LAST {course.LastRunDistance:F1} m";
                if (distanceText) distanceText.text = "DISTANCE\n" + distanceLabel + "\n" + bestLabel;
            }
        }
        void OnGUI()
        {
            if (input.IsVR) return;
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
            if (course.IsEndless)
            {
                if (distanceStyle == null)
                {
                    distanceStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleRight };
                    bestStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight };
                }
                float right = Screen.width - 264;
                GUI.Box(new Rect(right, 12, 252, 108), "DISTANCE");
                GUI.Label(new Rect(right + 8, 39, 236, 42), distanceLabel, distanceStyle);
                GUI.Label(new Rect(right + 8, 83, 236, 25), bestLabel, bestStyle);
            }
        }
    }
}
