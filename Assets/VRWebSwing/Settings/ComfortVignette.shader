// Uses XRI's hemisphere UVs and parameter names; URP/SPI/multiview stereo variant support.
Shader "VRWebSwing/ComfortVignette"
{
    Properties
    {
        _ApertureSize("Transparent aperture diameter", Range(0,1)) = 1
        _FeatheringEffect("Edge feather", Range(0,1)) = 0.3
        _VignetteColor("Mask color", Color) = (0,0,0,1)
        _VignetteColorBlend("Edge color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "ComfortMask"
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
                float _ApertureSize, _FeatheringEffect;
                half4 _VignetteColor, _VignetteColorBlend;
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
                float alpha = saturate((input.uv.y - alphaMin) / (_FeatheringEffect * _FeatheringEffect + 0.0001));
                half4 color = lerp(_VignetteColor, _VignetteColorBlend, saturate(input.uv.y * 2));
                color.a *= alpha;
                return color;
            }
            ENDHLSL
        }
    }
}
