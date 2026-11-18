using UnityEngine;

namespace VRWebSwing
{
    // Head-locked red edge warning, played once when the chasing back wall appears.
    // Separate from SwingComfort's tunneling vignette so A/B/C comfort comparisons and logs stay unaffected.
    public sealed class DangerVignette : MonoBehaviour
    {
        public EndlessCity city;
        [Tooltip("Renderer of the head-locked hemisphere using the VRWebSwing/DangerVignette shader.")] public MeshRenderer mask;
        [Tooltip("Edge tint. Its alpha is multiplied by Max Opacity.")] public Color color = new Color(1f, 0.05f, 0.05f, 1f);
        [Tooltip("Peak opacity at the outer edge. Below 1 keeps the world visible through the red.")] [Range(0, 1)] public float maxOpacity = 0.45f;
        [Tooltip("Total effect time in seconds, including fades.")] [Range(1.5f, 3f)] public float duration = 2f;
        [Tooltip("Fade-in time in seconds.")] [Min(0)] public float fadeIn = 0.2f;
        [Tooltip("Fade-out time in seconds.")] [Min(0)] public float fadeOut = 0.6f;
        [Tooltip("Diameter of the fully clear centre (0 = none, 1 = only the far edge is tinted).")] [Range(0, 1)] public float aperture = 0.7f;
        [Tooltip("Softness of the transition from the clear centre to the red edge.")] [Range(0, 1)] public float feather = 0.5f;
        public bool Playing { get; private set; }

        static readonly int ColorId = Shader.PropertyToID("_VignetteColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int ApertureId = Shader.PropertyToID("_ApertureSize");
        static readonly int FeatherId = Shader.PropertyToID("_FeatheringEffect");
        MaterialPropertyBlock block;
        float elapsed;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (mask) mask.enabled = false;
        }
        void OnEnable() { if (city) city.BackWallSpawned += Play; }
        void OnDisable()
        {
            if (city) city.BackWallSpawned -= Play;
            Stop();
        }
        public void Play()
        {
            if (!mask) return;
            elapsed = 0; Playing = true; mask.enabled = true;
            Apply(0);
        }
        public void Stop()
        {
            Playing = false;
            if (mask) mask.enabled = false;
        }
        void Update()
        {
            if (!Playing) return;
            // A death/restart removes the wall; drop the warning with it.
            if (city && city.BackWall && !city.BackWall.Running) { Stop(); return; }
            elapsed += Time.deltaTime;
            if (elapsed >= duration) { Stop(); return; }
            float up = fadeIn > 0 ? elapsed / fadeIn : 1;
            float down = fadeOut > 0 ? (duration - elapsed) / fadeOut : 1;
            // Single smooth rise and fall; no pulsing, which is uncomfortable in VR.
            Apply(Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(up, down))) * maxOpacity);
        }
        void Apply(float intensity)
        {
            mask.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(ApertureId, aperture);
            block.SetFloat(FeatherId, feather);
            mask.SetPropertyBlock(block);
        }
    }
}
