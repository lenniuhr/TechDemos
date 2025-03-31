#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#define DEG2RAD 0.01745329251
#define ROOT2BY2 0.70710678118

half4 AlphaBlend(half4 top, half4 bottom)
{
    half3 color = (top.rgb * top.a) + (bottom.rgb * (1 - top.a));
    half alpha = top.a + bottom.a * (1 - top.a);
    return half4(color, alpha);
}

half3 MixColorsByAlpha(float3 colorBottom, float3 colorTop, float alpha)
{
    return lerp(colorBottom, colorTop, alpha);
}

half4 MixColorsByValue(float4 colorBottom, float4 colorTop, float value)
{
    return lerp(colorBottom, colorTop, value);
}

float InverseLerp(float from, float to, float value)
{
    return saturate((value - from) / (to - from));
}

float SmoothstepThreshold(float threshold, float smooth, float value)
{
    float lower = max(0, threshold - smooth);
    float upper = min(1, threshold + smooth);
    return smoothstep(lower, upper, value);
}

float RemapToRange(float fromA, float fromB, float toA,float toB, float value)
{
	float value01 = InverseLerp(fromA, fromB, value);
	return lerp(toA, toB, value01);
}

// Convert rgb to luminance
// with rgb in linear space with sRGB primaries and D65 white point
float LinearRgbToLuminance(float3 linearRgb)
{
    return dot(linearRgb, float3(0.2126729f, 0.7151522f, 0.0721750f));
}

// Calculate a rotation matrix by angle radians around axis
float3x3 AngleAxis3x3(float angle, float3 axis)
{
    float c, s;
    sincos(angle, s, c);

    float t = 1 - c;
    float x = axis.x;
    float y = axis.y;
    float z = axis.z;

    return float3x3(
        t * x * x + c, t * x * y - s * z, t * x * z + s * y,
        t * x * y + s * z, t * y * y + c, t * y * z - s * x,
        t * x * z - s * y, t * y * z + s * x, t * z * z + c
    );
}

float4x4 AxisMatrix(float3 xAxis, float3 yAxis, float3 zAxis)
{
    return float4x4(
		xAxis.x, yAxis.x, zAxis.x, 0,
		xAxis.y, yAxis.y, zAxis.y, 0,
		xAxis.z, yAxis.z, zAxis.z, 0,
		0, 0, 0, 1
    );
}

float4x4 LookAtMatrix(float3 at, float3 eye, float3 up)
{
    float3 zAxis = normalize(at - eye);
    float3 xAxis = normalize(cross(up, zAxis));
    float3 yAxis = cross(zAxis, xAxis);
    return AxisMatrix(xAxis, yAxis, zAxis);
}

float3x3 LookAtMatrixXZ(float3 at, float3 eye)
{
    at.y = 0;
    eye.y = 0;
    
    float3 zAxis = normalize(at - eye);
    float3 yAxis = float3(0, 1, 0);
    float3 xAxis = normalize(cross(yAxis, zAxis));
    return (float3x3)AxisMatrix(xAxis, yAxis, zAxis);
}

float2 RotateAroundPivot2D(float2 position, float2 pivot, float degree)
{
    float radAngle = -radians(degree); // "-" - clockwise
    float x = position.x;
    float y = position.y;

    float rX = pivot.x + (x - pivot.x) * cos(radAngle) - (y - pivot.y) * sin(radAngle);
    float rY = pivot.y + (x - pivot.x) * sin(radAngle) + (y - pivot.y) * cos(radAngle);

    return float2(rX, rY);
}

float2 Rotate2D(float2 position, float degree)
{
    float radAngle = -radians(degree); // "-" - clockwise
    float x = position.x;
    float y = position.y;

    float rX = x * cos(radAngle) - y * sin(radAngle);
    float rY = x * sin(radAngle) + y * cos(radAngle);

    return float2(rX, rY);
}

inline float GammaToLinearSpaceExact(float value)
{
    if (value <= 0.04045F)
        return value / 12.92F;
    else if (value < 1.0F)
        return pow((value + 0.055F) / 1.055F, 2.4F);
    else
        return pow(value, 2.2F);
}

inline half3 GammaToLinearSpace(half3 sRGB)
{
    // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);

    // Precise version, useful for debugging.
    //return half3(GammaToLinearSpaceExact(sRGB.r), GammaToLinearSpaceExact(sRGB.g), GammaToLinearSpaceExact(sRGB.b));
}

inline float LinearToGammaSpaceExact(float value)
{
    if (value <= 0.0F)
        return 0.0F;
    else if (value <= 0.0031308F)
        return 12.92F * value;
    else if (value < 1.0F)
        return 1.055F * pow(value, 0.4166667F) - 0.055F;
    else
        return pow(value, 0.45454545F);
}

inline half3 LinearToGammaSpace(half3 linRGB)
{
    linRGB = max(linRGB, half3(0.h, 0.h, 0.h));
    // An almost-perfect approximation from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return max(1.055h * pow(linRGB, 0.416666667h) - 0.055h, 0.h);

    // Exact version, useful for debugging.
    //return half3(LinearToGammaSpaceExact(linRGB.r), LinearToGammaSpaceExact(linRGB.g), LinearToGammaSpaceExact(linRGB.b));
}

float4 WorldToScreenPos(float3 positionWS)
{
    float4 projectedCoords = mul(UNITY_MATRIX_VP, float4(positionWS, 1));
    projectedCoords.xyz /= projectedCoords.w;
    projectedCoords.xy *= float2(0.5, -0.5);
    projectedCoords.xy += float2(0.5, 0.5);
    return projectedCoords;
}

// Reconstruct world pos from depth
float3 ReconstructWorldPos(float3 positionWS, float w, float linearDepth)
{
    float3 camRelativeWorldPos = positionWS - _WorldSpaceCameraPos;
    
    // Compute projective scaling factor
    float perspectiveDivide = 1.0f / w;

    // Scale our view ray to unit depth diff
    float3 direction = camRelativeWorldPos * perspectiveDivide;

    // Advance by depthDiff along our view ray from the camera position.
    // This is the worldspace coordinate of the corresponding fragment
    // we retrieved from the depthDiff buffer.
    return direction * linearDepth + _WorldSpaceCameraPos;
}

float3 ReconstructWorldPos(float2 uv)
{
    float depth = SampleSceneDepth(uv);
    return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
}

void CoordinateSystem(float3 v1, out float3 v2, out float3 v3)
{
    if (abs(v1.x) > abs(v1.y))
    {
        v2 = float3(-v1.z, 0, v1.x) / sqrt(v1.x * v1.x + v1.z * v1.z);
    }
    else
    {
        v2 = float3(0, v1.z, -v1.y) / sqrt(v1.y * v1.y + v1.z * v1.z);
    }
    v3 = cross(v1, v2);
}

float3 ApplyNormalMap(float3 normalWS, float4 tangentWS, float3 texNormal, float3 normalStrength)
{
    float3 normalTS = texNormal.xzy;
    float3 binormal = cross(normalWS, tangentWS.xyz) * tangentWS.w;
    
    float3 appliedNormal = normalize(
		normalTS.x * tangentWS.xyz +
		normalTS.y * normalWS +
		normalTS.z * binormal
	);
    
    return normalize(lerp(normalWS, appliedNormal, normalStrength));
}

float3 ApplyNormalMap(float3 normal, float3 texNormal)
{
    // Build up corrdinate system
    float3 tangent;
    float3 bitangent;
    CoordinateSystem(normal, tangent, bitangent);
    
    float3x3 tangentSpace = float3x3(
        tangent.x, normal.x, bitangent.x,
        tangent.y, normal.y, bitangent.y,
        tangent.z, normal.z, bitangent.z
    );
    
    return normalize(mul(tangentSpace, texNormal));
}

#endif