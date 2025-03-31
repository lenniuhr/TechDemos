#ifndef GRASS_FORWARD_PASS_INCLUDED
#define GRASS_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"  
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Assets/Scripts/Grass Generation/Shaders/GrassInput.hlsl"

StructuredBuffer<DrawTriangle> _DrawTriangles;

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

half4 _Color;

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 texcoord : TEXCOORD0;
    float4 color : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    float3 normalWS : TEXCOORD3;
    float height : TEXCOORD4;
};

Varyings GrassVertex(uint vertexID: SV_VertexID)
{
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    DrawVertex vert = tri.vertices[vertexID % 3];

    Varyings OUT;
    OUT.positionHCS = TransformObjectToHClip(vert.position);
    OUT.positionWS = TransformObjectToWorld(vert.position);
    OUT.color = vert.color;
    OUT.normalWS = vert.normal;
    OUT.texcoord = vert.texcoord;
    OUT.height = vert.height;
    return OUT;
}

half4 GrassFragment(Varyings IN) : SV_Target
{
    float3 normalWS = normalize(lerp(IN.normalWS, float3(0, 1, 0), 0.0));
    
    // Ambient Light
    half3 ambient = IN.color * SampleSH(normalWS);
    
    // Direct Light
    half4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS.xyz); // was once calculated in the vertex shader REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
    Light light = GetMainLight(shadowCoord);
    float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
    
    float NdotL = saturate(dot(normalWS, light.direction));
    half3 direct = NdotL * IN.color * light.color * light.shadowAttenuation;
    
    return half4(ambient + direct, 1);
}

// Depth Normals pass

struct LeanVaryings
{
    float4 positionHCS : SV_POSITION;
    float3 normalWS : TEXCOORD3;
};

LeanVaryings DepthNormalsVertex(uint vertexID : SV_VertexID)
{
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    DrawVertex vert = tri.vertices[vertexID % 3];

    LeanVaryings OUT;
    OUT.positionHCS = TransformObjectToHClip(vert.position);
    OUT.normalWS = vert.normal;
    return OUT;
}

void DepthNormalsFragment(LeanVaryings IN, half facing : VFACE, out half4 normalWS : SV_Target0)
{
    //IN.normalWS = facing > 0 ? normalize(IN.normalWS) : normalize(-IN.normalWS);
    normalWS = half4(normalize(IN.normalWS), 0);
}

#endif