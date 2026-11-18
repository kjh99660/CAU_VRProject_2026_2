// Red warning edge on XRI's hemisphere mesh (same UV layout as ComfortVignette); URP/SPI/multiview stereo variant support.
// Centre stays fully clear; edges reach at most _Intensity opacity so the scene always shows through.
Shader "VRWebSwing/DangerVignette"
{
    Properties
    {
        _ApertureSize("Clear centre diameter", Range(0,1)) = 0.7
        _FeatheringEffect("Edge feather", Range(0,1)) = 0.5
        _VignetteColor("Edge color", Color) = (1,0.05,0.05,1)
        _Intensity("Overall opacity", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+6" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "DangerMask"
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            CBUFFER_START(UnityPerMaterial)
                float _ApertureSize, _FeatheringEffect, _Intensity;
                half4 _VignetteColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float aperture = saturate(_ApertureSize);
                float alphaMin = 0.5 - sqrt(max(0, 0.25 - aperture * aperture * 0.25));
                float edge = saturate((input.uv.y - alphaMin) / (_FeatheringEffect * _FeatheringEffect + 0.0001));
                half4 color = _VignetteColor;
                color.a *= edge * saturate(_Intensity);
                return color;
            }
            ENDHLSL
        }
    }
}
