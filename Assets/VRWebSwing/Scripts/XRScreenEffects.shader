Shader "Hidden/XR/IndependentScreenEffects"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always

        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        float4 _XRFXCenter;
        float4 _XRFXParams;
        float4 _XRFXColor;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionCS =
                GetFullScreenTriangleVertexPosition(input.vertexID);

            output.uv =
                GetFullScreenTriangleTexCoord(input.vertexID);

            return output;
        }

        half4 FragFog(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float rawDepth = SampleSceneDepth(input.uv);

            #if UNITY_REVERSED_Z
                bool isSky = rawDepth <= 0.0;
                float deviceDepth = rawDepth;
            #else
                bool isSky = rawDepth >= 1.0;
                float deviceDepth =
                    lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
            #endif

            float density = saturate(_XRFXParams.z);

            if (isSky)
            {
                float skyOpacity = density * _XRFXParams.w;
                return half4(_XRFXColor.rgb, skyOpacity);
            }

            float3 worldPosition = ComputeWorldSpacePosition(
                input.uv,
                deviceDepth,
                UNITY_MATRIX_I_VP);

            float cameraDistance =
                distance(worldPosition, _XRFXCenter.xyz);

            float startDistance = _XRFXParams.x;
            float endDistance = _XRFXParams.y;

            float t = saturate(
                (cameraDistance - startDistance) /
                max(endDistance - startDistance, 0.0001));

            /*    
            t = t * t * (3.0 - 2.0 * t);

            return half4(_XRFXColor.rgb, t * density);
            */
            float fogFactor =
                (1.0 - exp(-3.0 * t)) /
                (1.0 - exp(-3.0));

            float opacity = saturate(fogFactor * density);

            return half4(_XRFXColor.rgb, opacity);      
        }

        half4 FragVignette(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 offset = (input.uv - _XRFXCenter.xy) * 2.0;
            float radius = length(offset);

            float intensity = saturate(_XRFXParams.x);
            float innerRadius = _XRFXParams.y;
            float softness = max(_XRFXParams.z, 0.001);

            float opacity = smoothstep(
                innerRadius,
                innerRadius + softness,
                radius) * intensity;

            return half4(_XRFXColor.rgb, opacity);
        }

        ENDHLSL

        Pass
        {
            Name "DistanceFog"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragFog
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            ENDHLSL
        }

        Pass
        {
            Name "IndependentVignette"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragVignette
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            ENDHLSL
        }
    }

    Fallback Off
}