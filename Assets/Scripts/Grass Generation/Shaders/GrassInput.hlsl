#ifndef GRASS_INPUT_INCLUDED
#define GRASS_INPUT_INCLUDED

struct DrawVertex {
    float3 position;
    float3 normal;
    float2 texcoord;
    float height;
    float4 color;
};

struct DrawTriangle {
    DrawVertex vertices[3];
    float4 color;
};

struct SourceVertex
{
    float3 position;
    float3 normal;
    float4 color;
    float size;
};

struct IndirectArgs
{
    uint numVerticesPerInstance;
    uint numInstances;
    uint startVertexIndex;
    uint startInstanceIndex;
    uint numSourceVertices;
};

struct IndirectDrawArgs
{
    uint vertexCountPerInstance;
    uint instanceCount;
    uint startVertex;
    uint startInstance;
};

#endif