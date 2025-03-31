using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[ExecuteAlways]
public class TerrainGenerator : MonoBehaviour
{
    public ComputeShader marchingCubesShader;
    public ComputeShader clearDensityShader;
    public Material[] terrainMaterials;
    [Range(0, 1)]
    public float threshold;
    public bool autoUpdate;
    [Range(0.001f, 1)]
    public float blendGap;
    public int chunksPerUpdate;
    public bool enableGizmos;

    // Constants
    private const int CHUNK_SIZE = 32;
    private const int NUM_POINTS_PER_AXIS = CHUNK_SIZE + 3;
    private const int NUM_THREADS_CS = 8; // Must be the same as in Compute Shader

    // Private variables
    private Triangle[] m_Triangles = new Triangle[CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE * 5]; // Marching Cubes triangle array
    private Dictionary<Vector3Int, TerrainChunk> m_Chunks = new Dictionary<Vector3Int, TerrainChunk>();
    private UniqueQueue<Vector3Int> m_ChunkQueue = new UniqueQueue<Vector3Int>();

    private void OnEnable()
    {
        DeleteWorldInEditor();
    }

    private void OnDisable()
    {
        DeleteWorldInEditor();
    }

    private void OnDestroy()
    {
        DeleteWorldInEditor();
    }

    private void OnValidate()
    {
        if(autoUpdate)
        {
            DensityBox[] densityBoxes = GetComponentsInChildren<DensityBox>();
            AddChunksInBoxesToQueue(densityBoxes);
        }
    }
    private void Update()
    {
        if (!autoUpdate) return;

        if (clearDensityShader == null || marchingCubesShader == null)
        {
            Debug.LogWarning("Shader variables have not been assigned");
            return;
        }

        // Get density boxed that have been changed
        DensityBox[] updatedDensityBoxes = GetComponentsInChildren<DensityBox>().Where(densityBox => densityBox.HasChanged()).ToArray();
        AddChunksInBoxesToQueue(updatedDensityBoxes);

        if (m_ChunkQueue.Count > 0)
        {
            for (int i = 0; i < chunksPerUpdate; i++)
            {
                if (m_ChunkQueue.TryDequeue(out Vector3Int chunkId))
                {
                    UpdateChunk(chunkId);
                }
            }
        }
    }

    public void GenerateWorldInEditor()
    {
        DensityBox[] densityBoxes = GetComponentsInChildren<DensityBox>();
        AddChunksInBoxesToQueue(densityBoxes);
    }

    public void DeleteWorldInEditor()
    {
        foreach (TerrainChunk chunk in m_Chunks.Values)
        {
            chunk.ReleaseBuffers();
        }
        Transform chunksTransform = transform.Find("Chunks");

        m_Chunks.Clear();
        while (chunksTransform.childCount > 0)
        {
            DestroyImmediate(chunksTransform.GetChild(0).gameObject);
        }
    }

    private void DeleteTerrainChunk(Vector3Int chunkId)
    {
        TerrainChunk chunk = m_Chunks[chunkId];
        chunk.ReleaseBuffers();
        m_Chunks.Remove(chunkId);
        DestroyImmediate(chunk.meshObject);
    }


    private void AddChunksInBoxesToQueue(DensityBox[] densityBoxes)
    {
        foreach (DensityBox densityBox in densityBoxes)
        {
            // Overlapping chunks from last frame
            foreach(Vector3Int chunkId in densityBox.GetLastOverlappingChunks())
            {
                m_ChunkQueue.Enqueue(chunkId);
            }
            UpdateOverlappingChunks(densityBox);
            // Overlapping chunks from this frame
            foreach (Vector3Int chunkId in densityBox.GetLastOverlappingChunks())
            {
                m_ChunkQueue.Enqueue(chunkId);
            }
        }
    }

    public void AddChunksToQueue(HashSet<Vector3Int> chunkIds)
    {
        foreach (Vector3Int chunkId in chunkIds)
        {
            m_ChunkQueue.Enqueue(chunkId);
        }
    }

    public void UpdateChunk(Vector3Int chunkId)
    {
        if (!m_Chunks.ContainsKey(chunkId))
        {
            InitTerrainChunk(chunkId);
        }
        TerrainChunk chunk = m_Chunks[chunkId];

        DensityBox[] overlappingBoxes = GetComponentsInChildren<DensityBox>().Where(x => IsOverlapping(chunk.position, x)).ToArray();
        if (overlappingBoxes.Length == 0)
        {
            DeleteTerrainChunk(chunkId);
        }
        else
        {
            ComputeDensity(chunk);
            ComputeMesh(chunk);
        }
    }

    private bool IsOverlapping(Vector3 chunkCenter, DensityBox densityBox)
    {
        Vector3 boxMax = densityBox.transform.position + (densityBox.transform.localScale / 2.0f);
        Vector3 boxMin = densityBox.transform.position - (densityBox.transform.localScale / 2.0f);

        Vector3 chunkMax = chunkCenter + new Vector3(CHUNK_SIZE / 2, CHUNK_SIZE / 2, CHUNK_SIZE / 2) + Vector3.one;
        Vector3 chunkMin = chunkCenter - new Vector3(CHUNK_SIZE / 2, CHUNK_SIZE / 2, CHUNK_SIZE / 2) - Vector3.one;

        if (chunkMin.x > boxMax.x || chunkMin.y > boxMax.y || chunkMin.z > boxMax.z) return false;
        if (chunkMax.x < boxMin.x || chunkMax.y < boxMin.y || chunkMax.z < boxMin.z) return false;

        return true;
    }

    private void UpdateOverlappingChunks(DensityBox densityBox)
    {
        densityBox.ClearLastOverlappingChunks();

        Vector3 boxMin = densityBox.transform.position - (densityBox.transform.localScale / 2.0f);
        Vector3 boxMax = densityBox.transform.position + (densityBox.transform.localScale / 2.0f);

        Vector3Int chunkMin = Vector3Int.RoundToInt((boxMin - Vector3.one) / CHUNK_SIZE); // -1 for normals edge
        Vector3Int chunkMax = Vector3Int.RoundToInt((boxMax + Vector3.one) / CHUNK_SIZE); // +1 for normals edge

        for (int x = chunkMin.x; x <= chunkMax.x; x++)
        {
            for (int y = chunkMin.y; y <= chunkMax.y; y++)
            {
                for (int z = chunkMin.z; z <= chunkMax.z; z++)
                {
                    densityBox.AddLastOverlappingChunk(new Vector3Int(x, y, z));
                }
            }
        }
    }

    #region Chunk Generation

    private void InitTerrainChunk(Vector3Int chunkId)
    {
        Vector3 position = CHUNK_SIZE * chunkId;
        float horizontalOffset = -CHUNK_SIZE / 2.0f;
        Vector3 offset = horizontalOffset * Vector3.one - Vector3.one;
        string name = "Terrain Chunk (" + chunkId.x + ", " + chunkId.y + ", " + chunkId.z + ")";
        Transform chunksTransform = transform.Find("Chunks");

        TerrainChunk terrainChunk = new TerrainChunk(position, chunksTransform, name, terrainMaterials, NUM_POINTS_PER_AXIS, offset);
        m_Chunks.Add(chunkId, terrainChunk);
    }

    private void ComputeDensity(TerrainChunk chunk)
    {
        int numThreadGroups = (NUM_POINTS_PER_AXIS + (NUM_THREADS_CS - 1)) / NUM_THREADS_CS;

        // Clear points buffer
        int clearKernel = clearDensityShader.FindKernel("Clear");
        clearDensityShader.SetBuffer(clearKernel, "_Points", chunk.pointsBuffer);
        clearDensityShader.SetBuffer(clearKernel, "_Biomes", chunk.biomesBuffer);
        clearDensityShader.SetInt("_NumPointsPerAxis", NUM_POINTS_PER_AXIS);
        clearDensityShader.Dispatch(clearKernel, numThreadGroups, numThreadGroups, numThreadGroups);

        DensityBox[] densityBoxes = GetComponentsInChildren<DensityBox>();
        DensityBox[] overlappingBoxes = densityBoxes.Where(x => IsOverlapping(chunk.position, x)).ToArray();

        foreach (DensityBox densityBox in overlappingBoxes)
        {
            ComputeShader densityShader = densityBox.densityShader;
            int kernel = densityShader.FindKernel("Density");

            // Set general variables
            densityShader.SetBuffer(kernel, "_Points", chunk.pointsBuffer);
            densityShader.SetBuffer(kernel, "_Biomes", chunk.biomesBuffer);
            densityShader.SetInt("_NumPointsPerAxis", NUM_POINTS_PER_AXIS);
            densityShader.SetVector("_Center", chunk.position);
            densityShader.SetVector("_Offset", chunk.offset);
            densityShader.SetVector("_BoxCenter", densityBox.transform.position);
            densityShader.SetVector("_BoxScale", densityBox.transform.localScale);
            densityShader.SetMatrix("_SampleMatrix", densityBox.GetSampleMatrix());
            densityShader.SetInt("_BiomeId", densityBox.biomeId);

            // Init values depending on density type
            densityBox.InitComputeShader();

            // Dispatch shader
            densityShader.Dispatch(kernel, numThreadGroups, numThreadGroups, numThreadGroups);
        }
        // Don't release points buffer yet
    }

    private void ComputeMesh(TerrainChunk chunk)
    {
        int kernel = marchingCubesShader.FindKernel("MarchingCubes");

        int numThreads = 8; // must be the same as in Compute Shader
        int numThreadGroups = (CHUNK_SIZE + (numThreads - 1)) / numThreads;
        
        int numTris = (numThreads * numThreads * numThreads) * (numThreadGroups * numThreadGroups * numThreadGroups) * 5;
        int triangleStructSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Triangle));
        ComputeBuffer triangleBuffer = new ComputeBuffer(numTris, triangleStructSize, ComputeBufferType.Append); 
        triangleBuffer.SetCounterValue(0);

        ComputeBuffer triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);

        // Set buffer and dispatch
        marchingCubesShader.SetBuffer(kernel, "_Triangles", triangleBuffer);
        marchingCubesShader.SetBuffer(kernel, "_Points", chunk.pointsBuffer);
        marchingCubesShader.SetBuffer(kernel, "_Biomes", chunk.biomesBuffer);
        marchingCubesShader.SetInt("_ChunkSize", CHUNK_SIZE);
        marchingCubesShader.SetInt("_NumPointsPerAxis", NUM_POINTS_PER_AXIS);
        marchingCubesShader.SetFloat("_SurfaceLevel", threshold);
        marchingCubesShader.SetFloat("_BlendGap", blendGap);
        marchingCubesShader.Dispatch(kernel, numThreadGroups, numThreadGroups, numThreadGroups);

        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int triCount = triCountArray[0];

        triangleBuffer.GetData(m_Triangles);

        // Release buffers
        triangleBuffer.Release();
        triCountBuffer.Release();

        chunk.UpdateMesh(m_Triangles, triCount);
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (enableGizmos)
        {
            foreach (TerrainChunk chunk in m_Chunks.Values)
            {
                Gizmos.color = new Color(1, 1, 1, 0.05f);
                Gizmos.DrawWireCube(chunk.position, new Vector3(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE));
            }
        }
    }
}

