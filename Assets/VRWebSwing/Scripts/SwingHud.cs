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
        [Header("Lives UI (endless only)")]
        [Tooltip("Seconds the 'life lost' notice stays up after a revive.")] [Min(0)] public float lifeNoticeSeconds = 1.5f;
        [Tooltip("Distance in meters from the head to the notice and the game-over panel.")] [Min(0.5f)] public float panelDistance = 2;
        [Tooltip("View darkness during game over, 0..1. Below 1 keeps the surroundings faintly visible for orientation.")] [Range(0, 1)] public float gameOverDim = 0.75f;
        [Tooltip("Seconds over which the view darkens at game over.")] [Min(0)] public float gameOverFadeTime = 0.7f;
        [Tooltip("Controller vibration strength on death, 0..1. 0 = off.")] [Range(0, 1)] public float hapticAmplitude = 0.6f;
        [Tooltip("Controller vibration length in seconds.")] [Min(0)] public float hapticDuration = 0.2f;
        string distanceLabel, bestLabel, livesLabel;
        GUIStyle distanceStyle, bestStyle;
        float nextRefresh;
        SwingComfort comfort;
        static readonly int FadeColorId = Shader.PropertyToID("_Color");
        MeshRenderer fade;
        Material fadeMaterial;
        TextMesh notice, gameOver;
        int seenDeathVersion, seenRunVersion = -1;
        bool noticePending;
        float noticeStart = float.NegativeInfinity;
        void Awake()
        {
            comfort = motor.GetComponent<SwingComfort>();
            // Compatibility for older/validation scenes: never display the former camera-attached debug panel.
            Transform legacy = input.head.Find("World HUD");
            if (legacy) legacy.gameObject.SetActive(false);
            if (course.IsEndless) CreateLivesUi();
        }
        // Built once at startup so no scene objects or material assets need to be added by hand.
        void CreateLivesUi()
        {
            var shader = Shader.Find("VRWebSwing/ScreenFade");
            if (shader)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                temp.SetActive(false); // its collider never enters physics
                Mesh sphere = temp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(temp);
                var go = new GameObject("View Fade");
                go.transform.SetParent(input.head, false);
                go.transform.localScale = Vector3.one * 0.6f; // inside any wall, outside the 0.05m near plane
                go.AddComponent<MeshFilter>().sharedMesh = sphere;
                fade = go.AddComponent<MeshRenderer>();
                fadeMaterial = new Material(shader);
                fade.sharedMaterial = fadeMaterial;
                fade.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fade.receiveShadows = false;
                fade.enabled = false;
            }
            else Debug.LogWarning("SwingHud: VRWebSwing/ScreenFade shader missing; revive and game-over fades are disabled.", this);
            Font font = distanceText && distanceText.font ? distanceText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            notice = CreatePanelText("Life Notice", font, 0.035f);
            gameOver = CreatePanelText("Game Over Panel", font, 0.035f);
        }
        TextMesh CreatePanelText(string name, Font font, float size)
        {
            // Parented to the player body: follows the revive teleport and origin rebase, but not head rotation (comfortable to read).
            var text = new GameObject(name).AddComponent<TextMesh>();
            text.transform.SetParent(motor.transform, false);
            text.font = font; text.fontSize = 64; text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            // Draw after the view fade so the text stays readable on the darkened view.
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(font.material) { renderQueue = 4010 };
            text.gameObject.SetActive(false);
            return text;
        }
        void PlaceInFront(Transform panel)
        {
            Vector3 forward = Vector3.ProjectOnPlane(input.head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = motor.transform.forward;
            forward.Normalize();
            panel.SetPositionAndRotation(input.head.position + forward * panelDistance, Quaternion.LookRotation(forward));
        }
        void Update()
        {
            if (course.IsEndless) UpdateLives();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            if (distanceText) distanceText.gameObject.SetActive(course.IsEndless && input.IsVR);
            if (course.IsEndless)
            {
                distanceLabel = $"{course.RunDistance:F1} m";
                bestLabel = $"BEST {course.BestDistance:F1} m | LAST {course.LastRunDistance:F1} m";
                // ASCII only: the legacy runtime font may lack symbol glyphs such as hearts on the headset.
                livesLabel = $"LIVES {course.Lives} / {course.maxLives}";
                if (distanceText) distanceText.text = "DISTANCE\n" + distanceLabel + "\n" + bestLabel + "\n" + livesLabel;
            }
        }
        void UpdateLives()
        {
            float now = Time.unscaledTime;
            if (seenRunVersion != course.RunVersion)
            {
                seenRunVersion = course.RunVersion; seenDeathVersion = course.DeathVersion;
                noticePending = false; noticeStart = float.NegativeInfinity;
            }
            if (seenDeathVersion != course.DeathVersion)
            {
                seenDeathVersion = course.DeathVersion;
                Vibrate();
                noticePending = course.Life == LifeState.Reviving;
                noticeStart = float.NegativeInfinity;
                if (course.Life == LifeState.GameOver && gameOver) PlaceInFront(gameOver.transform);
            }
            // Show the notice only after the revive teleport, in front of the new position.
            if (noticePending && now >= course.DeathTime + course.reviveFadeTime)
            {
                noticePending = false; noticeStart = now;
                if (notice)
                {
                    notice.text = course.Lives == 1 ? "LIFE LOST\n1 LIFE LEFT" : $"LIFE LOST\n{course.Lives} LIVES LEFT";
                    PlaceInFront(notice.transform);
                }
            }
            if (notice)
            {
                float shown = now - noticeStart;
                bool visible = shown >= 0 && shown < lifeNoticeSeconds && course.Life != LifeState.GameOver;
                notice.gameObject.SetActive(visible);
                if (visible) notice.color = new Color(1, 1, 1, Mathf.Clamp01((lifeNoticeSeconds - shown) / 0.3f)); // soft fade at the end
            }
            if (gameOver)
            {
                bool over = course.Life == LifeState.GameOver;
                gameOver.gameObject.SetActive(over);
                if (over)
                    gameOver.text = (course.LastDeathCause == DeathCause.BackWall ? "GAME OVER\nCAUGHT BY THE WALL" : "GAME OVER\nOUT OF LIVES")
                        + $"\n\nDISTANCE {course.RunDistance:F1} m\nBEST {course.BestDistance:F1} m\n\nRESTART IN {Mathf.CeilToInt(course.GameOverTimeLeft)}";
            }
            if (fade)
            {
                float alpha = FadeAlpha(now - course.DeathTime);
                fade.enabled = alpha > 0.001f; // skip the full-view overdraw when invisible
                if (fade.enabled) fadeMaterial.SetColor(FadeColorId, new Color(0, 0, 0, alpha));
            }
        }
        float FadeAlpha(float sinceDeath)
        {
            if (course.Life == LifeState.GameOver)
                return gameOverDim * (gameOverFadeTime > 0 ? Mathf.Clamp01(sinceDeath / gameOverFadeTime) : 1);
            float f = course.reviveFadeTime;
            if (f <= 0 || sinceDeath < 0 || sinceDeath >= 2 * f) return 0;
            return sinceDeath < f ? sinceDeath / f : 1 - (sinceDeath - f) / f; // out, teleport, back in
        }
        void Vibrate()
        {
            if (!input.IsVR || hapticAmplitude <= 0 || hapticDuration <= 0) return;
            UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand).SendHapticImpulse(0, hapticAmplitude, hapticDuration);
            UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand).SendHapticImpulse(0, hapticAmplitude, hapticDuration);
        }
        void OnDestroy()
        {
            if (fadeMaterial) Destroy(fadeMaterial);
            if (notice) Destroy(notice.GetComponent<MeshRenderer>().sharedMaterial);
            if (gameOver) Destroy(gameOver.GetComponent<MeshRenderer>().sharedMaterial);
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
                GUI.Box(new Rect(right, 12, 252, 130), "DISTANCE");
                GUI.Label(new Rect(right + 8, 39, 236, 42), distanceLabel, distanceStyle);
                GUI.Label(new Rect(right + 8, 83, 236, 25), bestLabel, bestStyle);
                GUI.Label(new Rect(right + 8, 108, 236, 25), livesLabel, bestStyle);
            }
        }
    }
}
