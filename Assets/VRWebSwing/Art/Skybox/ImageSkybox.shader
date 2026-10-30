// Adapt a non-panoramic photograph to a stationary sky. The original JPG is not modified.
Shader "VRWebSwing/Image Skybox"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Sky photograph", 2D) = "white" {}
        _Exposure("Exposure", Range(0, 3)) = 1
        _Rotation("Heading (degrees)", Range(0, 360)) = 45
        _Repeats("Mirrored image panels (even number)", Range(2, 8)) = 4
        _Horizon("Image horizon from bottom", Range(0, 1)) = 0.265
        _VerticalFov("Source vertical view (degrees)", Range(30, 100)) = 60
        _PoleFade("Top/bottom transition width", Range(0.05, 0.5)) = 0.2
        _ZenithColor("Upper sky", Color) = (0.18, 0.32, 0.52, 1)
        _NadirColor("Lower sky", Color) = (0.64, 0.64, 0.67, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Exposure, _Rotation, _Repeats, _Horizon, _VerticalFov, _PoleFade;
                half4 _ZenithColor, _NadirColor;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.direction);
                float azimuth = atan2(direction.x, direction.z) / TWO_PI + 0.5 + _Rotation / 360;
                float panels = 2 * max(1, round(_Repeats * 0.5));
                // Triangle-wave U meets at identical edge pixels, avoiding a hard wrap seam.
                float u = 1 - abs(frac(azimuth * panels * 0.5) * 2 - 1);
                float slope = direction.y / max(length(direction.xz), 0.0001);
                float v = _Horizon + slope / (2 * tan(radians(_VerticalFov) * 0.5));
                half3 photo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(u, saturate(v))).rgb;
                float fade = max(_PoleFade, 0.01);
                photo = lerp(photo, _ZenithColor.rgb, smoothstep(1 - fade, 1 + fade, v));
                photo = lerp(photo, _NadirColor.rgb, 1 - smoothstep(-fade, fade, v));
                return half4(photo * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
