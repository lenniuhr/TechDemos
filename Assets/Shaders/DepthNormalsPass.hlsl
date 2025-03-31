#ifndef DEPTH_NORMALS_PASS_INCLUDED
#define DEPTH_NORMALS_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : NORMAL;
};

Varyings DepthNormalsVertex(Attributes IN)
{
    Varyings OUT;
    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
    OUT.normalWS = mul((float3x3)unity_ObjectToWorld, IN.normalOS);
    return OUT;
}

half4 DepthNormalsFragment(Varyings IN) : SV_TARGET
{
    return half4(normalize(IN.normalWS), 0.0);
}

#endif