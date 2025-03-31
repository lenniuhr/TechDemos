#ifndef LIGHTING_INCLUDED
#define LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Constants
#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)

// Global variables
float4 _AmbientSkyColor;
float4 _AmbientEquatorColor;

float _ShadowThreshold;
float _ShadowStep;
float _Toonyness;
float _ToonBrightness;

struct MaterialInfo
{
    half3 brdfDiffuse;
    half3 brdfSpecular;
    half reflectivity;
    half smoothness;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half normalizationTerm;
};

MaterialInfo InitializeMaterialInfo(half3 albedo, half smoothness, half metallic) {
    half oneMinusReflectivity = kDielectricSpec.a;
    oneMinusReflectivity = kDielectricSpec.a - metallic * kDielectricSpec.a;
                
    MaterialInfo info;
    info.reflectivity = half(1.0) - oneMinusReflectivity;
    info.brdfDiffuse = albedo * oneMinusReflectivity;
    info.brdfSpecular = lerp(kDielectricSpec.rgb, albedo, metallic);
    info.smoothness = smoothness;
    info.perceptualRoughness = 1.0 - smoothness;
    info.roughness = max(info.perceptualRoughness * info.perceptualRoughness, HALF_MIN_SQRT); 
    info.roughness2 = max(info.roughness * info.roughness, HALF_MIN);
    info.normalizationTerm = info.roughness * half(4.0) + half(2.0);

    return info;
}

float invLerp(float from, float to, float value)
{
    return saturate((value - from) / (to - from));
}

half3 GetAmbientDiffuseColor(float3 normalWS) {
    float HORIZON_ANGLE = -0.25;

    float NoUp = dot(normalWS, float3(0, 1, 0));
    float colorLerp = invLerp(HORIZON_ANGLE, 1.0, NoUp);

    return lerp(_AmbientEquatorColor.rgb, _AmbientSkyColor.rgb, colorLerp);
}

half3 GetAmbientDiffuseColorToon() {
    return _AmbientEquatorColor.rgb;
}

// A lot of code taken from Lighting.hlsl, GlobalIllumination.hlsl, 
// BRDF.hlsl and CommonMaterial.hlsl
half3 SampleGIColor(MaterialInfo info, float3 normalWS, float3 viewDirectionWS) {

    half3 giColor = half3(0, 0, 0);

    // indirect diffuse (sample gradient)
    half3 indirectDiffuse = GetAmbientDiffuseColor(normalWS);
    //indirectDiffuse = SampleSH(normalWS);
    giColor += indirectDiffuse * info.brdfDiffuse;
    //return indirectDiffuse;

    // sample environment map for indirect specular
    //half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    //half mip = PerceptualRoughnessToMipmapLevel(info.perceptualRoughness);
    //half4 encodedIrradiance = half4(SAMPLE_TEXTURECUBE_LOD(_GlossyEnvironmentCubeMap, sampler_GlossyEnvironmentCubeMap, reflectVector, mip));
    //half3 indirectSpecular = DecodeHDREnvironment(encodedIrradiance, _GlossyEnvironmentCubeMap_HDR);

    half3 indirectSpecular = indirectDiffuse;

    // environment color
    half NoV = saturate(dot(normalWS, viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV);
    float grazingTerm = saturate(info.smoothness + info.reflectivity);
    float surfaceReduction = 1.0 / (info.roughness2 + 1.0);
    giColor += indirectSpecular * half3(surfaceReduction * lerp(info.brdfSpecular, grazingTerm, fresnelTerm));

    return giColor;
}

half3 GetRadiance(Light light, float3 normalWS)
{
    // diffuse light
    half NdotL = saturate(dot(normalWS, light.direction));
    half shadowAttenuation = step(_ShadowThreshold, light.shadowAttenuation);
    half lightAttenuation = light.distanceAttenuation * NdotL * shadowAttenuation;

    // custom toon light attenuation
    half toonLightAttenuation = lightAttenuation;
    if (lightAttenuation < _ShadowStep)
    {
        toonLightAttenuation = 0.0;
    }
    else 
    {
        // Illuminated colors get shifted to _ToonBrighntess, depending on _Toonyness
        float illuminatedWidth = (1.0 - _ShadowStep);
        float lightAttenuation10 = 1.0 - (lightAttenuation - _ShadowStep) / illuminatedWidth;
        toonLightAttenuation = _ToonBrightness * (1.0 - (1.0 - _Toonyness) * (lightAttenuation10 * illuminatedWidth));
    }

    // attenuated light
    half3 radiance = light.color * toonLightAttenuation;
    return radiance;
}

half3 GetMainLightIllumination(MaterialInfo info, Light light, float3 normalWS, float3 viewDirectionWS) 
{

    half3 lightColor = half3(0, 0, 0);

    half3 radiance = GetRadiance(light, normalWS);

    lightColor += info.brdfDiffuse * radiance;

    // specular light
    float3 lightDirectionWSFloat3 = float3(light.direction);

    float3 halfDir = SafeNormalize(lightDirectionWSFloat3 + float3(viewDirectionWS));

    float NoH = saturate(dot(float3(normalWS), halfDir));
    half LoH = half(saturate(dot(lightDirectionWSFloat3, halfDir)));

    float d = NoH * NoH * (info.roughness2 - 1.0h) + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = info.roughness2 / ((d * d) * max(0.1h, LoH2) * info.normalizationTerm);

    // On platforms where half actually means something, the denominator has a risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles

    lightColor += info.brdfSpecular * specularTerm * radiance;

    return lightColor;
}

// Returns only the diffuse part of the main light illumination
half3 GetMainLightIlluminationDiffuse(MaterialInfo info, Light light, float3 normalWS)
{
    return info.brdfDiffuse * GetRadiance(light, normalWS);
}

#endif