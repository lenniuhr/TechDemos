#ifndef DEPTH_ONLY_PASS_INCLUDED
#define DEPTH_ONLY_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
};

Varyings DepthOnlyVertex(Attributes IN)
{
    Varyings OUT;
    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
    return OUT;
}

half4 DepthOnlyFragment(Varyings IN) : SV_TARGET
{
    return half4(0, 0, 0, 0);
}

#endif