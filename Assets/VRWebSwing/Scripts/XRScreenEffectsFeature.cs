using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class XRScreenEffectsFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Shader effectsShader;

    private Material material;
    private EffectPass fogPass;
    private EffectPass vignettePass;

    public override void Create()
    {
        CoreUtils.Destroy(material);
        material = null;
        fogPass = null;
        vignettePass = null;

        if (effectsShader == null)
            return;

        material = CoreUtils.CreateEngineMaterial(effectsShader);

        // 불투명 물체와 Skybox 위에 안개를 적용.
        fogPass = new EffectPass(
            material,
            0,
            "XR Distance Fog",
            RenderPassEvent.BeforeRenderingTransparents);

        // 기존 Volume 후처리 이후에 독립 비네트를 합성.
        vignettePass = new EffectPass(
            material,
            1,
            "XR Independent Vignette",
            RenderPassEvent.AfterRenderingPostProcessing);
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (material == null)
            return;

        Camera camera = renderingData.cameraData.camera;

        // Scene View, Preview, Overlay 카메라는 제외.
        if (camera.cameraType != CameraType.Game ||
            renderingData.cameraData.renderType != CameraRenderType.Base)
            return;

        if (!camera.TryGetComponent(out XRScreenEffects effects) ||
            !effects.isActiveAndEnabled)
            return;

        if (effects.fogEnabled && effects.fogDensity > 0f)
            renderer.EnqueuePass(fogPass);

        if (effects.vignetteEnabled && effects.vignetteIntensity > 0f)
            renderer.EnqueuePass(vignettePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        fogPass = null;
        vignettePass = null;
    }

    private sealed class EffectPass : ScriptableRenderPass
    {
        private static readonly int CenterId =
            Shader.PropertyToID("_XRFXCenter");

        private static readonly int ParamsId =
            Shader.PropertyToID("_XRFXParams");

        private static readonly int ColorId =
            Shader.PropertyToID("_XRFXColor");

        private readonly Material material;
        private readonly int shaderPass;
        private readonly string label;

        private readonly MaterialPropertyBlock properties =
            new MaterialPropertyBlock();

        private sealed class PassData
        {
            public Material material;
            public MaterialPropertyBlock properties;
            public int shaderPass;

            public Vector4 center;
            public Vector4 parameters;
            public Vector4 color;
        }

        public EffectPass(
            Material material,
            int shaderPass,
            string label,
            RenderPassEvent injectionPoint)
        {
            this.material = material;
            this.shaderPass = shaderPass;
            this.label = label;

            renderPassEvent = injectionPoint;

            ConfigureInput(shaderPass == 0
                ? ScriptableRenderPassInput.Depth
                : ScriptableRenderPassInput.None);
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            UniversalResourceData resources =
                frameData.Get<UniversalResourceData>();

            Camera camera =
                frameData.Get<UniversalCameraData>().camera;

            if (!camera.TryGetComponent(out XRScreenEffects effects) ||
                !effects.isActiveAndEnabled)
                return;

            bool isFog = shaderPass == 0;

            if (isFog && !resources.cameraDepthTexture.IsValid())
                return;

            using var builder =
                renderGraph.AddRasterRenderPass<PassData>(
                    label, out var data);

            data.material = material;
            data.properties = properties;
            data.shaderPass = shaderPass;

            Color tint;

            if (isFog)
            {
                // XR Origin 루트가 아니라 실제 Camera Transform 기준.
                Vector3 position = camera.transform.position;

                data.center = new Vector4(
                    position.x, position.y, position.z, 0f);

                float start = Mathf.Max(0f, effects.fogStartDistance);
                float end = Mathf.Max(
                    start + 0.01f, effects.fogEndDistance);

                data.parameters = new Vector4(
                    start,
                    end,
                    Mathf.Clamp01(effects.fogDensity),
                    effects.fogSkybox ? 1f : 0f);

                tint = effects.fogColor;

                // 깊이 텍스처의 읽기 의존성을 Render Graph에 선언.
                builder.UseTexture(
                    resources.cameraDepthTexture,
                    AccessFlags.Read);
            }
            else
            {
                data.center = new Vector4(
                    Mathf.Clamp01(effects.vignetteCenter.x),
                    Mathf.Clamp01(effects.vignetteCenter.y),
                    0f,
                    0f);

                data.parameters = new Vector4(
                    Mathf.Clamp01(effects.vignetteIntensity),
                    Mathf.Max(0f, effects.vignetteRadius),
                    Mathf.Max(0.01f, effects.vignetteSoftness),
                    0f);

                tint = effects.vignetteColor;
            }

            // SetVector를 사용하므로 색 공간 변환을 명시적으로 처리.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                tint = tint.linear;

            data.color = new Vector4(tint.r, tint.g, tint.b, 1f);

            // 현재 화면을 유지하면서 알파 블렌딩.
            // 화면 색상을 별도 텍스처로 복사할 필요가 없음.
            builder.SetRenderAttachment(
                resources.activeColorTexture,
                0,
                AccessFlags.ReadWrite);

            builder.SetRenderFunc(
                (PassData pass, RasterGraphContext context) =>
                {
                    // 실행 시점에 설정하므로 카메라 간 값 공유를 방지.
                    pass.properties.Clear();
                    pass.properties.SetVector(CenterId, pass.center);
                    pass.properties.SetVector(ParamsId, pass.parameters);
                    pass.properties.SetVector(ColorId, pass.color);

                    context.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        pass.material,
                        pass.shaderPass,
                        MeshTopology.Triangles,
                        3,
                        1,
                        pass.properties);
                });
        }
    }
}