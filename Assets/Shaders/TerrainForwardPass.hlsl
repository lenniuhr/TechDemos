
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"  
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float _Smoothness;
float _Metallic;

float4 _RockLight;
float4 _RockDark;
float4 _GrassLight;
float4 _GrassDark;

sampler2D _BlendNoiseTex;
float _BlendNoiseOffset;
float _BlendNoiseScale;
float _BlendNoiseWeight;

static const float BIOME_BLEND_EDGE = 0.5;

float4 TriplanarOffset(float3 vertPos, float3 normal, float3 scale, sampler2D tex, float2 offset) {
    float3 scaledPos = vertPos / scale;
    float4 colX = tex2D(tex, scaledPos.zy + offset);
    float4 colY = tex2D(tex, scaledPos.xz + offset);
    float4 colZ = tex2D(tex, scaledPos.xy + offset);

    // Square normal to make all values positive + increase blend sharpness
    float3 blendWeight = normal * normal;

    // Divide blend weight by the sum of its components. This will make x + y + z = 1
    blendWeight = blendWeight / (blendWeight.x + blendWeight.y + blendWeight.z);
    return colX * blendWeight.x + colY * blendWeight.y + colZ * blendWeight.z;
}

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS : NORMAL;
    float4 color : COLOR;
    float3 uv : TEXCOORD0;
};

struct Varyings
{
    float4 positionHCS  : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float fogFactor : TEXCOORD1;
    float3 normalWS : NORMAL;
    float4 color : COLOR;
};

Varyings TerrainForwardVertex(Attributes IN)
{
    Varyings OUT;
    OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
    OUT.positionWS = mul(unity_ObjectToWorld, IN.positionOS).xyz;
    OUT.normalWS = normalize(mul((float3x3)unity_ObjectToWorld, IN.normalOS));
    OUT.color = IN.color;
    OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
    return OUT;
}

half4 TerrainForwardFragment(Varyings IN) : SV_TARGET
{
    float3 normalWS = normalize(IN.normalWS);
    // steepness from 0 to 1 based on normal.y
    float steepness = 1 - (IN.normalWS.y * 0.5 + 0.5);

    float4 noise = TriplanarOffset(IN.positionWS, normalWS, 30, _BlendNoiseTex, 0);
    float4 rockNoise = TriplanarOffset(IN.positionWS, normalWS, 50, _BlendNoiseTex, 0);

    // blend on biome edges
    float4 blendNoise = TriplanarOffset(IN.positionWS, normalWS, _BlendNoiseScale, _BlendNoiseTex, 0);
    float blendOffset = (blendNoise.r - _BlendNoiseOffset) * _BlendNoiseWeight;
    float blendEdge = min(BIOME_BLEND_EDGE + blendOffset, 0.99999);

    if (IN.color.a < blendEdge) {
        discard;
    }

    // colors
    int r = 10;
    float grass = floor(noise.r * r) / float(r);
    float4 grassCol = lerp(_GrassLight, _GrassDark, grass);

    // lerp with rock color
    float rock = floor(rockNoise.r * r) / float(r);
    float4 rockCol = lerp(_RockLight, _RockDark, rock);

    float t = 0.38;
    float n = (noise.r - 0.4) * t;
    float rockWeight = step(0.24 + n, steepness);

    half4 color = lerp(grassCol, rockCol, rockWeight);
    
    // Indirect Light
    half3 sh = SampleSH(normalWS);
    half3 indirect = sh * color;
    
    // Direct Light
    half4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS.xyz); // was once calculated in the vertex shader REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
    Light light = GetMainLight(shadowCoord);
    float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
    
    float NdotL = saturate(dot(normalWS, light.direction));
    half3 direct = NdotL * color * light.color * light.shadowAttenuation;
    
    // Rim Light
    float NdotV = dot(normalWS, viewDirectionWS);
    float strength = pow(saturate(1.0 - NdotV), 8.0);
    half3 rim = strength * light.color * (indirect + direct);
    
    return half4(indirect + direct + rim, 1);
    
    return color;

    /*
    // Get Light
    half4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS.xyz); // was once calculated in the vertex shader REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
    Light light = GetMainLight(shadowCoord);

    float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

    // calculate brdf properties
    MaterialInfo info = InitializeMaterialInfo(color.rgb, _Smoothness, _Metallic);

    // global illumination
    half3 giColor = SampleGIColor(info, normalWS, viewDirectionWS);

    //return half4(GetAmbientDiffuseColor(normalWS), 1);

    // diffuse light
    half3 lightColor = GetMainLightIllumination(info, light, normalWS, viewDirectionWS);
    
    return half4(lightColor, 1);

    half4 finalColor = half4(MixFog(giColor + lightColor, IN.fogFactor), 1);

    return finalColor;*/
}