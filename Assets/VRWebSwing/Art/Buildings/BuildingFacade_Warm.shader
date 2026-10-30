Shader "VRWebSwing/Building Facade Warm"
{
    Properties
    {
        [NoScaleOffset] _BaseMap("Window facade", 2D) = "white" {}
        _TileSize("Meters per complete texture tile", Range(2, 20)) = 8
        _Tint("Facade tint", Color) = (1,1,1,1)
        _RoofColor("Roof and underside", Color) = (0.18,0.24,0.3,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Name "BuildingFacade"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "BuildingFacadeCommon.hlsl"
            ENDHLSL
        }
    }
}
