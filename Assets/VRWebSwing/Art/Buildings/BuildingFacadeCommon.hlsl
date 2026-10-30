#ifndef VR_WEB_SWING_FACADE_INCLUDED
#define VR_WEB_SWING_FACADE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
CBUFFER_START(UnityPerMaterial)
    float _TileSize;
    half4 _Tint, _RoofColor;
CBUFFER_END
struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 face : TEXCOORD1;
    half fog : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};
Varyings Vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    float3 size = float3(length(unity_ObjectToWorld._m00_m10_m20),
        length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
    float3 meters = (input.positionOS.xyz + 0.5) * size;
    float3 face = abs(input.normalOS);
    output.uv = float2(face.x > 0.5 ? meters.z : meters.x, meters.y) / max(_TileSize, 0.01);
    output.face = face;
    output.fog = ComputeFogFactor(output.positionCS.z);
    return output;
}
half4 Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _Tint.rgb;
    color *= lerp(0.82h, 1.0h, input.face.z);
    color = lerp(color, _RoofColor.rgb, step(0.5h, input.face.y));
    return half4(MixFog(color, input.fog), 1);
}

#endif
