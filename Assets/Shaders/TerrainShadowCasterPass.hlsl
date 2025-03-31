#ifndef TERRAIN_SHADOW_CASTER_PASS_INCLUDED
#define TERRAIN_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Unity variables
float3 _LightDirection;
float4 _ShadowBias;

// Terrain normal bias
float _NormalBias;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
};

float3 ApplyTerrainShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
{
    float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
    float scale = invNdotL * _NormalBias;

    // normal bias is negative since we want to apply an inset normal offset
    positionWS = lightDirection * _ShadowBias.xxx + positionWS;
    positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

float4 GetShadowPositionHClip(Attributes IN)
{
    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
    float4 positionCS = TransformWorldToHClip(ApplyTerrainShadowBias(positionWS, normalWS, _LightDirection));

    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif

    return positionCS;
}

Varyings TerrainShadowCasterVertex(Attributes IN)
{
    Varyings OUT;
    OUT.positionCS = GetShadowPositionHClip(IN);
    return OUT;
}

half4 TerrainShadowCasterFragment(Varyings IN) : SV_TARGET
{
    return 0;
}

#endif