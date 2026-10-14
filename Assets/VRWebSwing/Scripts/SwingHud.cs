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
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            status = $"{course.State} | CP {course.NextCheckpoint}/3\nSpeed {motor.Velocity.magnitude:F1} m/s | Ground {motor.Grounded}\nWeb {rope.Attached} | Length {rope.Length:F1} m | Aim {rope.CanAttach}\n{rope.Status}\n";
            worldText.text = status + (input.IsVR
                ? "Triggers: hold web / release owner\nLeft stick: move | Right A: jump\nLeft Y: respawn | Left menu: restart"
                : "Mouse: look / aim | Click: capture + fire | Esc: free mouse\nLMB / RMB: hold left / right web; release to detach\nWASD: move | Space: jump | R: respawn | T: restart");
            if (course.State == CourseState.Complete) worldText.text += "\nCOMPLETE! Respawn or restart";
        }
        void OnGUI()
        {
            if (input.IsVR) return;
            GUI.Box(new Rect(12, 12, 510, 180), worldText.text);
            Color previousColor = GUI.color;
            GUI.color = rope.CanAttach ? Color.green : Color.white;
            float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
            GUI.DrawTexture(new Rect(x - 7, y - 1, 14, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1, y - 7, 2, 14), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
