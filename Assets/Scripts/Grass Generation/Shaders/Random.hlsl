#ifndef RANDOM_INCLUDED
#define RANDOM_INCLUDED

uint NextRandom(inout uint state)
{
    state = state * 747796405 + 2891336453;
    uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
    result = (result >> 22) ^ result;
    return result;
}

// Returns a random value in range [0, 1)
float RandomValue(inout uint state)
{
    return NextRandom(state) / 4294967296.0; // 2^32
}

float Random(float3 seed)
{
    return frac(sin(dot(seed.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
}

float Rand3dTo1d(float3 value, float3 dotDir = float3(12.9898, 78.233, 37.719))
{
    //make value smaller to avoid artefacts
    float3 smallValue = sin(value);
    //get scalar value from 3d vector
    float random = dot(smallValue, dotDir);
    //make value more random by making it bigger and then taking the factional part
    random = frac(sin(random) * 143758.5453);
    return random;
}

float3 Rand3dTo3d(float3 value)
{
    return float3(
        Rand3dTo1d(value, float3(12.989, 78.233, 37.719)),
        Rand3dTo1d(value, float3(39.346, 11.135, 83.155)),
        Rand3dTo1d(value, float3(73.156, 52.235, 09.151))
    );
}

float Rand2dTo1d(float2 value, float2 dotDir = float2(12.9898, 78.233))
{
    float2 smallValue = sin(value);
    float random = dot(smallValue, dotDir);
    random = frac(sin(random) * 143758.5453);
    return random;
}
            
float2 Rand2dTo2d(float2 value)
{
    return float2(
        Rand2dTo1d(value, float2(12.989, 78.233)),
        Rand2dTo1d(value, float2(39.346, 11.135))
    );
}

float Rand1dTo1d(float value, float mutator = 0.546)
{
    float random = frac(sin(value + mutator) * 143758.5453);
    return random;
}
            
float3 Rand1dTo3d(float value)
{
    return float3(
        Rand1dTo1d(value, 3.9812),
        Rand1dTo1d(value, 7.1536),
        Rand1dTo1d(value, 5.7241)
    );
}

float Halton(uint base, uint index)
{
    float result = 0;
    float digitWeight = 1;
    while (index > 0u)
    {
        digitWeight = digitWeight / float(base);
        uint nominator = index % base;
        result += float(nominator) * digitWeight;
        index = index / base;
    }
    return result;
}

#endif